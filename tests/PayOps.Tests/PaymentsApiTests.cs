using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayOps.Database.Entities;
using PayOps.Database.Lookups;
using PayOps.Payments.Api;
using PayOps.Payments.Data;
using PayOps.ServiceDefaults;

namespace PayOps.Tests;

[Collection(SqlServerCollection.Name)]
public class PaymentsApiTests(SqlServerFixture fixture) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly PaymentsApiFactory _factory = new(fixture.ConnectionString);

    public void Dispose() => _factory.Dispose();

    private HttpClient Staff(params string[] roles)
    {
        var client = _factory.CreateClient();
        if (roles.Length > 0)
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        return client;
    }

    private HttpClient Operator() => Staff(PayOpsRoles.Operator);

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static Task<T> Get<T>(HttpClient http, string url) => http.GetAsync(url).ContinueWith(t => Read<T>(t.Result)).Unwrap();

    private sealed record Seed(SecondaryClient Client, int OpenId, int SentId, long RejectionId, long PaidTodayId, string CreatorName);

    /// <summary>
    /// One client with an open batch (2 pending payments) and a batch in a bank run: one payment paid "today" just after
    /// midnight SAST (still the previous day in UTC), one paid on 10 Nov, one rejected with a PendingUnreconciled refund.
    /// </summary>
    private async Task<Seed> SeedAsync()
    {
        await using var db = fixture.CreatePayOpsContext();
        var client = await TestData.AddClientAsync(db);
        var maker = await TestData.AddUserAsync(db);
        var bank = await TestData.AddBankAsync(db);
        var run = new PaymentBatch { BankId = bank.Id, Reference = TestData.Unique("BR"), Name = "Run", FileName = "run.txt", RunDate = DateTime.UtcNow, CreatedByUserId = maker.Id };

        var open = TestData.NewSummary(client.Id, 1);
        open.CreatedByUserId = maker.Id;
        open.PaymentRecords.Add(TestData.NewRecord(100m));
        open.PaymentRecords.Add(TestData.NewRecord(200m));

        var sent = TestData.NewSummary(client.Id, 2);
        sent.PaymentBatch = run;
        sent.ReconStatusId = (byte)ReconStatusCode.Reconciled;
        var paidToday = TestData.NewRecord(130m);
        var paidEarlier = TestData.NewRecord(70m);
        var rejected = TestData.NewRecord(170m);
        paidToday.TransactionStatusId = paidEarlier.TransactionStatusId = (byte)TransactionStatusCode.Success;
        rejected.TransactionStatusId = (byte)TransactionStatusCode.Rejected;
        rejected.RejectionReason = "Account closed";
        sent.PaymentRecords.Add(paidToday);
        sent.PaymentRecords.Add(paidEarlier);
        sent.PaymentRecords.Add(rejected);
        db.PaymentSummaries.AddRange(open, sent);
        await db.SaveChangesAsync();

        db.PaymentRecordStatusHistories.AddRange(
            new PaymentRecordStatusHistory { PaymentRecordId = paidToday.Id, TransactionStatusId = (byte)TransactionStatusCode.Success, PaymentBatchId = run.Id, ChangedDate = new DateTime(2026, 11, 15, 22, 30, 0, DateTimeKind.Utc) },
            new PaymentRecordStatusHistory { PaymentRecordId = paidEarlier.Id, TransactionStatusId = (byte)TransactionStatusCode.Success, PaymentBatchId = run.Id, ChangedDate = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc) });
        var rejection = new PaymentRejection
        {
            PaymentRecordId = rejected.Id, PaymentBatchId = null, AccountName = "Rejections", BranchCode = "250655",
            AccountNumber = "62000000001", Amount = 170m, RejectionReason = null
        };
        db.PaymentRejections.Add(rejection);
        await db.SaveChangesAsync();
        return new Seed(client, open.Id, sent.Id, rejection.Id, paidToday.Id, maker.Username);
    }

    [Fact]
    public async Task Readers_read_operators_change_and_api_keys_are_refused()
    {
        var seed = await SeedAsync();
        Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/batches")).StatusCode);
        var withKey = _factory.CreateClient();
        withKey.DefaultRequestHeaders.Add("X-Api-Key", "po_not-a-staff-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await withKey.GetAsync("/api/batches")).StatusCode);

        var reader = Staff(PayOpsRoles.Reader);
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/api/batches/{seed.OpenId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync($"/api/batches/{seed.OpenId}/hold", new HoldRequest("x"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(PayOpsRoles.ClientAdmin).PostAsJsonAsync($"/api/rejections/{seed.RejectionId}/confirm", new ConfirmRejectionRequest("x"))).StatusCode);
    }

    [Fact]
    public async Task Batch_search_detail_payments_and_csv_export()
    {
        var seed = await SeedAsync();
        var http = Staff(PayOpsRoles.Reader);
        var client = seed.Client.UniqueClientId;

        var all = await Get<PagedResult<OpsBatchDto>>(http, $"/api/batches?client={client}");
        Assert.Equal(2, all.TotalCount);
        Assert.Equal(seed.OpenId, Assert.Single((await Get<PagedResult<OpsBatchDto>>(http, $"/api/batches?client={client}&inBankRun=false")).Items).Id);
        Assert.Equal(seed.SentId, Assert.Single((await Get<PagedResult<OpsBatchDto>>(http, $"/api/batches?client={client}&hasRejections=true")).Items).Id);
        Assert.Equal(seed.SentId, Assert.Single((await Get<PagedResult<OpsBatchDto>>(http, $"/api/batches?client={client}&reconStatus=Reconciled")).Items).Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync("/api/batches?paymentDateFrom=2026-11-10&paymentDateTo=2026-11-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync("/api/batches?pageSize=1000")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/api/batches/2147483000")).StatusCode);

        var detail = await Get<OpsBatchDetailDto>(http, $"/api/batches/{seed.SentId}");
        Assert.Equal(1, detail.RejectionCount);
        Assert.NotNull(detail.BankRunReference);
        Assert.Equal(2, detail.PaymentStatuses.Single(s => s.Status == TransactionStatusCode.Success).Count);
        Assert.Equal(170m, detail.PaymentStatuses.Single(s => s.Status == TransactionStatusCode.Rejected).Amount);

        var rejected = await Get<PagedResult<OpsPaymentDto>>(http, $"/api/batches/{seed.SentId}/payments?status=Rejected");
        Assert.Equal("Account closed", Assert.Single(rejected.Items).RejectionReason);
        var payment = await Get<PaymentDetailDto>(http, $"/api/payments/{seed.PaidTodayId}");
        Assert.Equal(TransactionStatusCode.Success, Assert.Single(payment.StatusHistory).Status);

        var csv = await http.GetAsync($"/api/batches?client={client}&format=csv");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        var lines = (await csv.Content.ReadAsStringAsync()).TrimEnd().Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("Id,UniqueReference,ClientReference", lines[0]);
        var paymentsCsv = (await (await http.GetAsync($"/api/batches/{seed.SentId}/payments?format=csv")).Content.ReadAsStringAsync()).TrimEnd().Split('\n');
        Assert.Equal(4, paymentsCsv.Length);
    }

    [Fact]
    public async Task Reconciliation_needs_a_comment_is_locked_in_a_bank_run_and_queues_one_funding_confirmation()
    {
        var seed = await SeedAsync();
        var http = Operator();
        var url = $"/api/batches/{seed.OpenId}/reconciliation";
        var before = await Get<OpsBatchDetailDto>(http, $"/api/batches/{seed.OpenId}");

        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync(url, new SetReconStatusRequest(ReconStatusCode.Reconciled, " "), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync(url, new { status = 9, comment = "x" })).StatusCode);

        var reconciled = await Read<OpsBatchDetailDto>(await http.PostAsJsonAsync(url, new SetReconStatusRequest(ReconStatusCode.Reconciled, "Funds received"), Json));
        Assert.Equal(ReconStatusCode.Reconciled, reconciled.ReconStatus);
        Assert.Equal("test", reconciled.ReconStatusChangedBy);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync(url, new SetReconStatusRequest(ReconStatusCode.Reconciled, "again"), Json)).StatusCode);

        var stale = await http.PostAsJsonAsync(url, new SetReconStatusRequest(ReconStatusCode.Unreconciled, "stale", before.RowVersion), Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync($"/api/batches/{seed.SentId}/reconciliation", new SetReconStatusRequest(ReconStatusCode.Unreconciled, "in a run"), Json)).StatusCode);

        await Read<OpsBatchDetailDto>(await http.PostAsJsonAsync(url, new SetReconStatusRequest(ReconStatusCode.Unreconciled, "Bounced", reconciled.RowVersion), Json));
        await Read<OpsBatchDetailDto>(await http.PostAsJsonAsync(url, new SetReconStatusRequest(ReconStatusCode.Reconciled, "Funds received again"), Json));

        var comments = await Get<List<CommentDto>>(http, $"/api/batches/{seed.OpenId}/comments");
        Assert.Equal(3, comments.Count);
        Assert.Equal("Reconciliation Unreconciled -> Reconciled: Funds received", comments[0].Comment);
        Assert.All(comments, c => Assert.Equal("test", c.CreatedBy));

        await using var db = fixture.CreatePayOpsContext();
        var message = await db.NotificationMessages.SingleAsync(m => m.PaymentSummaryId == seed.OpenId);
        Assert.Equal(NotificationType.FundingConfirmation, message.Type);
        Assert.Equal(1, await db.AppUsers.CountAsync(u => u.Username == "test"));

        var unreconciled = await Get<PagedResult<OpsBatchDto>>(http, "/api/reconciliation/unreconciled?pageSize=200");
        Assert.DoesNotContain(unreconciled.Items, b => b.Id == seed.OpenId || b.Id == seed.SentId);
    }

    [Fact]
    public async Task Hold_and_release_are_recorded_in_the_history()
    {
        var seed = await SeedAsync();
        var http = Operator();

        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync($"/api/batches/{seed.OpenId}/hold", new HoldRequest(""))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync($"/api/batches/{seed.SentId}/hold", new HoldRequest("Too late"))).StatusCode);

        var held = await Read<OpsBatchDetailDto>(await http.PostAsJsonAsync($"/api/batches/{seed.OpenId}/hold", new HoldRequest("Client asked to wait")));
        Assert.True(held.IsOnHold);
        Assert.Equal("Client asked to wait", held.HoldReason);
        Assert.Equal("test", held.HeldBy);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync($"/api/batches/{seed.OpenId}/hold", new HoldRequest("again"))).StatusCode);
        Assert.Equal(seed.OpenId, Assert.Single((await Get<PagedResult<OpsBatchDto>>(http, $"/api/batches?client={seed.Client.UniqueClientId}&onHold=true")).Items).Id);

        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync($"/api/batches/{seed.OpenId}/release", new ReleaseRequest(null))).StatusCode);
        var released = await Read<OpsBatchDetailDto>(await http.PostAsJsonAsync($"/api/batches/{seed.OpenId}/release", new ReleaseRequest("Go ahead")));
        Assert.False(released.IsOnHold);
        Assert.Null(released.HoldReason);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync($"/api/batches/{seed.OpenId}/release", new ReleaseRequest("again"))).StatusCode);

        var history = await Get<List<BatchChangeDto>>(http, $"/api/batches/{seed.OpenId}/history");
        Assert.Equal(seed.CreatorName, Assert.Single(history, c => c.Field == "Created").ChangedBy);
        Assert.Contains(history, c => c is { Field: "OnHold", OldValue: "False", NewValue: "True", ChangedBy: "test" });
        Assert.Contains(history, c => c is { Field: "OnHold", OldValue: "True", NewValue: "False", ChangedBy: "test" });
        Assert.Contains(history, c => c is { Field: "Comment", NewValue: "Put on hold: Client asked to wait" });
        Assert.Contains(history, c => c is { Field: "Comment", NewValue: "Released from hold: Go ahead" });
    }

    [Fact]
    public async Task Paid_away_is_today_in_south_african_time_and_history_takes_a_range()
    {
        var seed = await SeedAsync();
        var http = Staff(PayOpsRoles.Reader);
        var client = seed.Client.UniqueClientId;

        var today = await Get<PaidListDto>(http, $"/api/paid-away?client={client}");
        Assert.Equal(seed.PaidTodayId, Assert.Single(today.Items).PaymentId);
        Assert.Equal(130m, today.TotalAmount);
        Assert.Equal(FixedClock.Today, today.From);

        var range = await Get<PaidListDto>(http, $"/api/paid-history?from=2026-11-01&to=2026-11-16&client={client}");
        Assert.Equal(2, range.TotalCount);
        Assert.Equal(200m, range.TotalAmount);
        Assert.Equal(0, (await Get<PaidListDto>(http, $"/api/paid-history?from=2026-11-01&to=2026-11-09&client={client}")).TotalCount);

        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync("/api/paid-history?from=2026-10-01&to=2026-11-16")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync("/api/paid-history?from=2026-11-16&to=2026-11-01")).StatusCode);
        var csv = (await (await http.GetAsync($"/api/paid-history?from=2026-11-01&to=2026-11-16&client={client}&format=csv")).Content.ReadAsStringAsync()).TrimEnd().Split('\n');
        Assert.Equal(3, csv.Length);
    }

    [Fact]
    public async Task Rejection_refund_account_is_corrected_then_confirmed_once()
    {
        var seed = await SeedAsync();
        var http = Operator();
        var url = $"/api/rejections/{seed.RejectionId}";

        var waiting = await Get<PagedResult<RejectionListItemDto>>(http, $"/api/rejections?status=PendingUnreconciled&client={seed.Client.UniqueClientId}");
        var item = Assert.Single(waiting.Items);
        Assert.Equal("Account closed", item.PaymentRejectionReason);

        Assert.Equal(HttpStatusCode.BadRequest, (await http.PutAsJsonAsync($"{url}/refund-account", new UpdateRefundAccountRequest("Client", "25065", "62000000009\n"))).StatusCode);
        var corrected = await Read<RejectionDetailDto>(await http.PutAsJsonAsync($"{url}/refund-account", new UpdateRefundAccountRequest("Client refunds", "198765", "1234567890")));
        Assert.Equal("1234567890", corrected.AccountNumber);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PutAsJsonAsync($"{url}/refund-account", new UpdateRefundAccountRequest("Client", "\u0661\u0662\u0663\u0664\u0665\u0666", "1"))).StatusCode);
        var audit = Assert.Single(await Get<List<CommentDto>>(http, $"/api/batches/{seed.SentId}/comments"));
        Assert.Equal($"Refund account for rejection {seed.RejectionId} changed from Rejections 250655 62000000001 to Client refunds 198765 1234567890", audit.Comment);
        Assert.Equal("test", audit.CreatedBy);

        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync($"{url}/confirm", new ConfirmRejectionRequest(""))).StatusCode);
        var confirmed = await Read<RejectionDetailDto>(await http.PostAsJsonAsync($"{url}/confirm", new ConfirmRejectionRequest("Checked with client"), Json));
        Assert.Equal(TransactionStatusCode.Pending, confirmed.Status);
        Assert.Equal("test", confirmed.ConfirmedBy);
        var step = Assert.Single(confirmed.Payment.StatusHistory, h => h.RejectionId == seed.RejectionId);
        Assert.Equal(TransactionStatusCode.Pending, step.Status);
        Assert.Equal("Refund confirmed: Checked with client", step.Reason);

        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync($"{url}/confirm", new ConfirmRejectionRequest("again"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PutAsJsonAsync($"{url}/refund-account", new UpdateRefundAccountRequest("X", "198765", "1"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/api/rejections/9000000000")).StatusCode);

        await using var db = fixture.CreatePayOpsContext();
        var message = await db.NotificationMessages.SingleAsync(m => m.DeduplicationKey == $"RejectionConfirmation:{seed.RejectionId}");
        Assert.Equal(seed.SentId, message.PaymentSummaryId);
        Assert.Contains("1234567890", message.PayloadJson);
    }
}

internal sealed class PaymentsApiFactory(string connectionString) : WebApplicationFactory<PaymentsApiMarker>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:PayOps", connectionString);
        builder.UseSetting("Authentication:Enabled", "true");
        builder.UseSetting("Authentication:TenantId", "00000000-0000-0000-0000-000000000000");
        builder.UseSetting("Authentication:Audience", "api://payops-payments-api");
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
