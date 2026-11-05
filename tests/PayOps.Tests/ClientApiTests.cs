using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayOps.Client.Api.Authentication;
using PayOps.Client.Data;

namespace PayOps.Tests;

[Collection(SqlServerCollection.Name)]
public class ClientApiTests(SqlServerFixture fixture) : IDisposable
{
    private readonly ClientApiFactory _factory = new(fixture.ConnectionString);

    public void Dispose() => _factory.Dispose();

    private HttpClient Client(params string[] roles)
    {
        var client = _factory.CreateClient();
        if (roles.Length > 0)
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        return client;
    }

    private HttpClient Admin => Client(ClientApiAuth.AdminRole);

    private async Task<PrimaryClientDto> CreatePrimaryAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/primary-clients", new CreatePrimaryClientRequest(TestData.Unique("P"), "Api primary"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var primary = (await response.Content.ReadFromJsonAsync<PrimaryClientDto>())!;
        Assert.Equal($"/api/primary-clients/{primary.Id}", response.Headers.Location!.OriginalString);
        return primary;
    }

    [Fact]
    public async Task Health_is_anonymous()
    {
        var response = await Client().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Requests_need_a_signed_in_user_with_a_role()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().GetAsync("/api/primary-clients")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client("Other").GetAsync("/api/primary-clients")).StatusCode);

        var reader = Client(ClientApiAuth.ReaderRole);
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/primary-clients")).StatusCode);
        var write = await reader.PostAsJsonAsync("/api/primary-clients", new CreatePrimaryClientRequest(TestData.Unique("P"), "Reader"));
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Fact]
    public async Task Client_hierarchy_is_managed_through_the_api()
    {
        var admin = Admin;
        var primary = await CreatePrimaryAsync(admin);

        var created = await admin.PostAsJsonAsync($"/api/primary-clients/{primary.Id}/secondary-clients", new CreateSecondaryClientRequest(TestData.Unique("S"), "Api secondary"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var secondary = (await created.Content.ReadFromJsonAsync<SecondaryClientDto>())!;

        var list = await admin.GetFromJsonAsync<PagedResult<SecondaryClientDto>>($"/api/primary-clients/{primary.Id}/secondary-clients");
        Assert.Equal(secondary.Id, Assert.Single(list!.Items).Id);
        var byUniqueId = await admin.GetFromJsonAsync<SecondaryClientDto>($"/api/secondary-clients/by-unique-id/{secondary.UniqueClientId}");
        Assert.Equal(secondary.Id, byUniqueId!.Id);

        var updated = await admin.PutAsJsonAsync($"/api/secondary-clients/{secondary.Id}", new UpdateSecondaryClientRequest("Renamed", secondary.RowVersion));
        Assert.Equal("Renamed", (await updated.Content.ReadFromJsonAsync<SecondaryClientDto>())!.Name);

        var blocked = await admin.PostAsync($"/api/primary-clients/{primary.Id}/deactivate", null);
        await AssertProblemAsync(blocked, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Errors_are_problem_details()
    {
        var admin = Admin;
        await AssertProblemAsync(await admin.GetAsync("/api/primary-clients/999999"), HttpStatusCode.NotFound);

        var invalid = await admin.PostAsJsonAsync("/api/primary-clients", new CreatePrimaryClientRequest("BAD ID", ""));
        var validation = await AssertProblemAsync<HttpValidationProblemDetails>(invalid, HttpStatusCode.BadRequest);
        Assert.Contains("uniqueClientId", validation.Errors.Keys);

        var primary = await CreatePrimaryAsync(admin);
        var stale = new UpdatePrimaryClientRequest("Stale", false, null, Convert.ToBase64String(new byte[8]));
        await AssertProblemAsync(await admin.PutAsJsonAsync($"/api/primary-clients/{primary.Id}", stale), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Api_key_and_bank_account_endpoints_work()
    {
        var admin = Admin;
        var primary = await CreatePrimaryAsync(admin);

        var keyResponse = await admin.PostAsJsonAsync($"/api/primary-clients/{primary.Id}/api-keys", new CreateApiKeyRequest("Integration"));
        Assert.Equal(HttpStatusCode.Created, keyResponse.StatusCode);
        var key = (await keyResponse.Content.ReadFromJsonAsync<CreatedClientApiKeyDto>())!;
        Assert.False(string.IsNullOrEmpty(key.Key));

        var listed = await admin.GetStringAsync($"/api/primary-clients/{primary.Id}/api-keys");
        Assert.DoesNotContain(key.Key, listed);
        Assert.Contains(key.ApiKey.KeyPrefix, listed);

        var revoked = await admin.PostAsync($"/api/api-keys/{key.ApiKey.Id}/revoke", null);
        Assert.NotNull((await revoked.Content.ReadFromJsonAsync<ClientApiKeyDto>())!.RevokedDate);

        var account = await admin.PostAsJsonAsync($"/api/primary-clients/{primary.Id}/bank-accounts", new CreateBankAccountRequest(1, "Main", "250655", "62000000001"));
        Assert.Equal(HttpStatusCode.Created, account.StatusCode);
        var accounts = await admin.GetFromJsonAsync<List<ClientBankAccountDto>>($"/api/primary-clients/{primary.Id}/bank-accounts");
        Assert.Single(accounts!);
    }

    private static Task<ProblemDetails> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
        => AssertProblemAsync<ProblemDetails>(response, status);

    private static async Task<T> AssertProblemAsync<T>(HttpResponseMessage response, HttpStatusCode status) where T : ProblemDetails
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = (await response.Content.ReadFromJsonAsync<T>())!;
        Assert.Equal((int)status, problem.Status);
        return problem;
    }
}

internal sealed class ClientApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:PayOps", connectionString);
        builder.UseSetting("Authentication:Enabled", "true");
        builder.UseSetting("Authentication:TenantId", "00000000-0000-0000-0000-000000000000");
        builder.UseSetting("Authentication:Audience", "api://payops-client-api");
        builder.ConfigureTestServices(services => services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null));
    }
}

/// <summary>Signs the request in with the comma-separated roles in the <see cref="RolesHeader"/> header; anonymous without it.</summary>
internal sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RolesHeader = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RolesHeader, out var roles))
            return Task.FromResult(AuthenticateResult.NoResult());
        var claims = roles.ToString().Split(',').Select(r => new Claim(ClaimTypes.Role, r)).Append(new Claim(ClaimTypes.Name, "test"));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
