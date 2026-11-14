using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PayOps.Database.Entities;
using PayOps.Database.Lookups;

namespace PayOps.Tests;

[Collection(SqlServerCollection.Name)]
public class SchemaTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Migration_creates_expected_tables()
    {
        await using var db = fixture.CreatePayOpsContext();
        var tables = await db.Database
            .SqlQueryRaw<string>("SELECT name AS [Value] FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name <> '__EFMigrationsHistory'")
            .ToListAsync();

        string[] expected =
        [
            "AccountType", "AppUser", "ApprovalStatus", "Bank", "BankClientProfile", "BatchStatus", "ClientApiKey", "ClientBankAccount", "PaymentBatch",
            "PaymentRecord", "PaymentRecordStatusHistory", "PaymentRejection", "PaymentSummary", "PaymentSummaryComment",
            "PaymentSummaryHistory", "PrimaryClient", "ReconStatus", "Role", "SecondaryClient", "ServiceType",
            "TransactionStatus", "UserRole"
        ];
        Assert.Equal(expected.Order(), tables.Order());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Read_committed_snapshot_is_on()
    {
        await using var db = fixture.CreatePayOpsContext();
        var rcsi = await db.Database
            .SqlQueryRaw<bool>("SELECT is_read_committed_snapshot_on AS [Value] FROM sys.databases WHERE name = DB_NAME()")
            .SingleAsync();
        Assert.True(rcsi);
    }

    [Fact]
    public async Task Lookups_are_seeded()
    {
        await using var db = fixture.CreatePayOpsContext();
        var statuses = await db.TransactionStatuses.OrderBy(s => s.Id).Select(s => s.Code).ToListAsync();
        Assert.Equal(["Pending", "Transmitted", "Success", "Rejected", "PendingUnreconciled"], statuses);
        Assert.Equal(5, await db.BatchStatuses.CountAsync());
        Assert.Equal(3, await db.ReconStatuses.CountAsync());
        Assert.Equal(5, await db.ServiceTypes.CountAsync());
        Assert.Equal(["Nominated Account", "Charges Account"], await db.AccountTypes.OrderBy(a => a.Id).Select(a => a.Name).ToListAsync());
        Assert.Equal(3, await db.ApprovalStatuses.CountAsync());
    }

    [Fact]
    public async Task Database_defaults_are_applied()
    {
        await using var db = fixture.CreatePayOpsContext();
        var user = await TestData.AddUserAsync(db);
        var client = await TestData.AddClientAsync(db);
        var bank = await TestData.AddBankAsync(db);
        var batch = new PaymentBatch { BankId = bank.Id, Reference = TestData.Unique("BR"), Name = "Run", RunDate = DateTime.UtcNow, CreatedByUserId = user.Id };
        var summary = TestData.NewSummary(client.Id);
        summary.PaymentBatch = batch;
        var record = TestData.NewRecord();
        summary.PaymentRecords.Add(record);
        db.PaymentSummaries.Add(summary);
        await db.SaveChangesAsync();
        var rejection = new PaymentRejection { PaymentRecordId = record.Id, AccountName = "Rej", BranchCode = "250655", AccountNumber = "1", Amount = record.Amount };
        db.PaymentRejections.Add(rejection);
        await db.SaveChangesAsync();

        Assert.Equal((byte)BatchStatusCode.Created, batch.BatchStatusId);
        Assert.Equal((byte)ApprovalStatusCode.Pending, batch.ApprovalStatusId);
        Assert.Equal((byte)ReconStatusCode.Unreconciled, summary.ReconStatusId);
        Assert.Equal((byte)TransactionStatusCode.Pending, record.TransactionStatusId);
        Assert.Equal((byte)TransactionStatusCode.PendingUnreconciled, rejection.TransactionStatusId);
        Assert.True(record.CreatedDate > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task Summary_must_belong_to_an_existing_secondary_client()
    {
        await using var db = fixture.CreatePayOpsContext();
        db.PaymentSummaries.Add(TestData.NewSummary(secondaryClientId: int.MaxValue));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("FK_PaymentSummary_SecondaryClient", ex.InnerException!.Message);
    }

    [Fact]
    public async Task Secondary_client_sequence_number_is_unique()
    {
        await using var db = fixture.CreatePayOpsContext();
        var client = await TestData.AddClientAsync(db);
        db.PaymentSummaries.Add(TestData.NewSummary(client.Id, 7));
        await db.SaveChangesAsync();
        db.PaymentSummaries.Add(TestData.NewSummary(client.Id, 7));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Maker_cannot_approve_own_bank_run()
    {
        await using var db = fixture.CreatePayOpsContext();
        var user = await TestData.AddUserAsync(db);
        var bank = await TestData.AddBankAsync(db);
        db.PaymentBatches.Add(new PaymentBatch
        {
            BankId = bank.Id, Reference = TestData.Unique("BR"), Name = "Run", RunDate = DateTime.UtcNow,
            CreatedByUserId = user.Id, ApprovedByUserId = user.Id, ApprovalStatusId = (byte)ApprovalStatusCode.Approved
        });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("CK_PaymentBatch_MakerChecker", ex.InnerException!.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Payment_amount_must_be_positive(decimal amount)
    {
        await using var db = fixture.CreatePayOpsContext();
        var client = await TestData.AddClientAsync(db);
        var summary = TestData.NewSummary(client.Id);
        summary.PaymentRecords.Add(TestData.NewRecord(amount));
        db.PaymentSummaries.Add(summary);
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("CK_PaymentRecord_Amount", ex.InnerException!.Message);
    }

    [Fact]
    public async Task Employee_account_type_is_limited_to_10_characters()
    {
        await using var db = fixture.CreatePayOpsContext();
        var client = await TestData.AddClientAsync(db);
        var summary = TestData.NewSummary(client.Id);
        summary.PaymentRecords.Add(TestData.NewRecord(accountType: new string('X', 11)));
        db.PaymentSummaries.Add(summary);
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.IsType<SqlException>(ex.InnerException);
    }

    [Fact]
    public async Task Summary_changes_are_kept_in_temporal_history()
    {
        await using var db = fixture.CreatePayOpsContext();
        var client = await TestData.AddClientAsync(db);
        var summary = TestData.NewSummary(client.Id);
        db.PaymentSummaries.Add(summary);
        await db.SaveChangesAsync();

        summary.ReconStatusId = (byte)ReconStatusCode.Reconciled;
        await db.SaveChangesAsync();

        var versions = await db.PaymentSummaries.TemporalAll().Where(s => s.Id == summary.Id).Select(s => s.ReconStatusId).ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.Contains((byte)ReconStatusCode.Unreconciled, versions);
    }
}
