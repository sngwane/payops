using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PayOps.BatchFile.Api;
using PayOps.BatchFile.Data;
using PayOps.ServiceDefaults;

namespace PayOps.Tests;

[Collection(SqlServerCollection.Name)]
public class BatchFileApiTests(SqlServerFixture fixture) : IDisposable
{
    private readonly BatchFileApiFactory _factory = new(fixture.ConnectionString);

    public void Dispose() => _factory.Dispose();

    private HttpClient WithKey(string key)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", key);
        return client;
    }

    private HttpClient Staff(params string[] roles)
    {
        var client = _factory.CreateClient();
        if (roles.Length > 0)
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        return client;
    }

    [Fact]
    public async Task Batch_endpoints_need_a_valid_api_key()
    {
        Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/batches")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await WithKey("po_not-a-real-key").GetAsync("/api/batches")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Staff(PayOpsRoles.Operator).GetAsync("/api/batches")).StatusCode);
    }

    [Fact]
    public async Task Client_submits_json_and_xml_then_reads_its_batches_and_a_retry_returns_409_with_the_batch_id()
    {
        await using var db = fixture.CreatePayOpsContext();
        var (client, key, _) = await BatchTestData.AddClientWithKeyAsync(db);
        var (_, otherKey, _) = await BatchTestData.AddClientWithKeyAsync(db);
        var http = WithKey(key);
        var batch = BatchTestData.Batch(client.UniqueClientId);

        var created = await http.PostAsJsonAsync("/api/batches", batch);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var accepted = (await created.Content.ReadFromJsonAsync<BatchAcceptedDto>())!;
        Assert.Equal($"/api/batches/{accepted.BatchId}", created.Headers.Location!.OriginalString);

        var retry = await http.PostAsJsonAsync("/api/batches", batch);
        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
        var problem = (await retry.Content.ReadFromJsonAsync<ProblemDetails>())!;
        Assert.Equal(accepted.BatchId, ((System.Text.Json.JsonElement)problem.Extensions["batchId"]!).GetInt32());

        var detail = (await http.GetFromJsonAsync<BatchDetailDto>($"/api/batches/{accepted.BatchId}"))!;
        Assert.Equal(2, detail.Payments.Items.Count);
        Assert.Equal(HttpStatusCode.NotFound, (await WithKey(otherKey).GetAsync($"/api/batches/{accepted.BatchId}")).StatusCode);
        Assert.Empty((await http.GetFromJsonAsync<List<RejectedPaymentDto>>($"/api/batches/{accepted.BatchId}/rejections"))!);
        var list = (await http.GetFromJsonAsync<PagedResult<BatchSummaryDto>>("/api/batches"))!;
        Assert.Contains(list.Items, b => b.Id == accepted.BatchId);

        var xml = new StringContent(BatchTestData.Xml(client.UniqueClientId, TestData.Unique("X")), Encoding.UTF8, "application/xml");
        Assert.Equal(HttpStatusCode.Created, (await http.PostAsync("/api/batches/xml", xml)).StatusCode);
    }

    [Fact]
    public async Task Validate_and_bad_requests_return_problem_details()
    {
        await using var db = fixture.CreatePayOpsContext();
        var (client, key, _) = await BatchTestData.AddClientWithKeyAsync(db);
        var http = WithKey(key);

        var ok = await http.PostAsJsonAsync("/api/batches/validate", BatchTestData.Batch(client.UniqueClientId));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.True((await ok.Content.ReadFromJsonAsync<BatchValidationDto>())!.IsValid);

        var invalid = await http.PostAsJsonAsync("/api/batches/validate", BatchTestData.Batch(client.UniqueClientId, footerTotal: 1m));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("footer.totalValue", (await invalid.Content.ReadFromJsonAsync<HttpValidationProblemDetails>())!.Errors.Keys);

        var malformed = await http.PostAsync("/api/batches", new StringContent("{ \"header\": ", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        var log = (await Staff(PayOpsRoles.Reader).GetFromJsonAsync<PagedResult<BatchUploadDto>>($"/api/uploads?uniqueClientId={client.UniqueClientId}&isAccepted=false"))!;
        Assert.Contains(log.Items, u => u.Format == "Json" && u.Errors is { Count: > 0 });
    }

    [Fact]
    public async Task Staff_endpoints_need_roles_and_operators_maintain_holidays_and_billing_rates()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Staff().GetAsync("/api/uploads")).StatusCode);
        await using (var db = fixture.CreatePayOpsContext())
        {
            var (_, key, _) = await BatchTestData.AddClientWithKeyAsync(db, primaryKey: true);
            Assert.Equal(HttpStatusCode.Unauthorized, (await WithKey(key).GetAsync("/api/uploads")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await WithKey(key).GetAsync("/api/public-holidays")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await Staff(PayOpsRoles.Reader).GetAsync("/api/public-holidays?year=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Staff(PayOpsRoles.Reader).GetAsync("/api/public-holidays?year=10000")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff("Other").GetAsync("/api/uploads")).StatusCode);
        var reader = Staff(PayOpsRoles.Reader);
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/uploads?isAccepted=false")).StatusCode);
        var holiday = new PublicHolidayRequest(new DateOnly(2031, 1, 1), "New Year's Day");
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync("/api/public-holidays", holiday)).StatusCode);

        var ops = Staff(PayOpsRoles.Operator);
        var created = await ops.PostAsJsonAsync("/api/public-holidays", holiday);
        Assert.True(created.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict);
        Assert.Contains((await ops.GetFromJsonAsync<List<PublicHolidayDto>>("/api/public-holidays?year=2031"))!, h => h.HolidayDate == holiday.HolidayDate);

        var rate = await ops.PostAsJsonAsync("/api/billing-rates", new { serviceType = "TwoDay", feePerRecord = 1.25m, effectiveFrom = TestDate() });
        Assert.Equal(HttpStatusCode.Created, rate.StatusCode);
        var dto = (await rate.Content.ReadFromJsonAsync<BillingRateDto>())!;
        Assert.Equal("TwoDay", dto.ServiceType);
        var updated = await ops.PutAsJsonAsync($"/api/billing-rates/{dto.Id}", new UpdateBillingRateRequest(1.50m, dto.EffectiveFrom));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var invalid = await ops.PostAsJsonAsync("/api/billing-rates", new { serviceType = "TwoDay", feePerRecord = -1m, effectiveFrom = TestDate() });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private static string TestDate() => new DateOnly(1990, 1, 1).AddDays(Random.Shared.Next(0, 3000)).ToString("yyyy-MM-dd");
}

internal sealed class BatchFileApiFactory(string connectionString) : WebApplicationFactory<BatchFileApiMarker>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:PayOps", connectionString);
        builder.UseSetting("Authentication:Enabled", "true");
        builder.UseSetting("Authentication:TenantId", "00000000-0000-0000-0000-000000000000");
        builder.UseSetting("Authentication:Audience", "api://payops-batchfile-api");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(new FixedClock());
            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
        });
    }
}
