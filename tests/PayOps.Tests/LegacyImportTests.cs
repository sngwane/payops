using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PayOps.Client.Data.Services;
using PayOps.Database;
using PayOps.LegacyImport;
using PayOps.LegacyImport.Legacy;
using Testcontainers.MsSql;

namespace PayOps.Tests;

/// <summary>Own SQL Server container: one legacy BulkPayments copy and one PayOps database per test.</summary>
public sealed class LegacyImportFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task<(string Legacy, string PayOps)> CreateDatabasesAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var legacy = Connection($"BulkPayments_{suffix}");
        var payOps = Connection($"PayOps_{suffix}");
        await using (var master = new SqlConnection(_container.GetConnectionString()))
        {
            await master.OpenAsync();
            await new SqlCommand($"CREATE DATABASE [BulkPayments_{suffix}]", master).ExecuteNonQueryAsync();
        }
        await ExecuteAsync(legacy, LegacyImportTests.LegacySchema);
        await using var db = PayOpsContext(payOps);
        await db.Database.MigrateAsync();
        return (legacy, payOps);
    }

    public static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await new SqlCommand(sql, connection).ExecuteNonQueryAsync();
    }

    public static PayOpsDbContext PayOpsContext(string cs) => new(new DbContextOptionsBuilder<PayOpsDbContext>().UseSqlServer(cs).Options);

    public static LegacyDbContext LegacyContext(string cs) => new(new DbContextOptionsBuilder<LegacyDbContext>().UseSqlServer(cs).Options);

    private string Connection(string database)
        => new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = database }.ConnectionString;
}

public class LegacyImportTests(LegacyImportFixture fixture) : IClassFixture<LegacyImportFixture>
{
    // Subset of the legacy bulkpayments_script.sql columns, same names and types.
    internal const string LegacySchema = """
        CREATE TABLE PrimaryClient (PrimaryClientId int IDENTITY PRIMARY KEY, ClientName varchar(50) NULL, UniqueClientId varchar(50) NULL,
            AutoProcess bit NULL, AccessKey varchar(50) NULL, LogoFilename varchar(500) NULL);
        CREATE TABLE SecondaryClient (SecondaryClientId int IDENTITY PRIMARY KEY, ClientName varchar(50) NULL, UniqueClientId varchar(50) NULL,
            PrimaryClientId int NULL, NotificationXml varchar(max) NULL, AccessKey varchar(50) NULL);
        CREATE TABLE BankAccount (BankAccountId int IDENTITY PRIMARY KEY, AccountNo varchar(50) NULL, BranchCode varchar(6) NOT NULL,
            Description varchar(50) NOT NULL, Balance money NULL, LastStatementDate smalldatetime NULL, GLAccNo char(7) NULL);
        CREATE TABLE PrimaryClientBankAccountType (PrimaryClientId int NULL, BankAccountId int NULL, AccountTypeId int NULL);
        CREATE TABLE Bank (BankId int IDENTITY PRIMARY KEY, Name varchar(100) NULL);
        CREATE TABLE BankClientProfile (BankClientProfileId int IDENTITY PRIMARY KEY, BankID int NOT NULL, ClientProfileNumber varchar(10) NOT NULL,
            ClientIdentifier char(3) NOT NULL, RunningSequence bigint NOT NULL, DailySequence int NOT NULL, LastUpdateDate datetime NULL, Active bit NULL);
        CREATE TABLE BillingRates (DebtorsCode varchar(15) NULL, ServiceTypeId int NULL, Rate decimal(18,2) NULL, DateEffective datetime NOT NULL);
        CREATE TABLE PublicHoliday (PublicHolidayId int IDENTITY PRIMARY KEY, [Date] date NOT NULL);
        CREATE TABLE BankRun (BankRunId int IDENTITY PRIMARY KEY, BankFileName varchar(50) NULL, FileSequenceNumber varchar(24) NULL,
            DateTrn datetime NULL, StatusId int NULL, BankRunReason varchar(max) NULL);
        CREATE TABLE Header (HeaderId int IDENTITY PRIMARY KEY, BatchId varchar(50) NULL, BankRunId int NULL, UniqueClientId varchar(50) NULL,
            ServiceTypeId int NULL, PaymentDate datetime NULL, RejectionAccountName varchar(50) NULL, RejectionBranchCode varchar(50) NULL,
            RejectionAccountNumber varchar(50) NULL, UniquePaymentReference varchar(50) NULL, NotificationXml varchar(max) NULL,
            ReconStatusId int NULL, DateTrn datetime NULL, SequenceNumber bigint NULL, ModifiedDate datetime NULL, Records int NULL, TotalValue money NULL);
        CREATE TABLE Transactions (TransactionId int IDENTITY PRIMARY KEY, BatchId varchar(50) NULL, BankFileReference varchar(50) NULL,
            EmployeeCode varchar(50) NULL, EmployeeSurname varchar(50) NULL, EmployeeFirstname varchar(50) NULL, EmployeeInitials varchar(3) NULL,
            EmployeeIdOrPassport varchar(50) NULL, EmployeeCell varchar(13) NULL, EmployeeEmail varchar(50) NULL, BranchCode varchar(6) NULL,
            AccountNumber varchar(16) NULL, AccountType varchar(10) NULL, Amount money NULL, BankReference varchar(50) NULL, DateTrn datetime NULL,
            ModifiedDate datetime NULL, TransactionStatusId int NULL, RejectionReason varchar(max) NULL);
        CREATE TABLE Rejections (RejectionId int IDENTITY PRIMARY KEY, BankRunId int NULL, BankReference varchar(50) NULL, BankFileReference varchar(50) NULL,
            AccountName varchar(50) NULL, AccountNumber varchar(16) NULL, BranchCode varchar(6) NULL, AccountType varchar(10) NULL, Amount money NULL,
            ServiceTypeId int NULL, TransactionStatusId int NULL, TransactionId int NULL, ModifiedDate datetime NULL, RejectionReason varchar(max) NULL);
        CREATE TABLE HeaderComment (CommentId int IDENTITY PRIMARY KEY, HeaderId int NOT NULL, Comment varchar(max) NULL, CommentDate datetime NOT NULL);
        """;

    private const string SampleData = """
        INSERT Bank (Name) VALUES ('Nedbank');
        INSERT BankClientProfile (BankID, ClientProfileNumber, ClientIdentifier, RunningSequence, DailySequence, Active) VALUES (1, '1234567890', 'ABC', 42, 3, 1);
        INSERT PublicHoliday ([Date]) VALUES ('2026-12-25'), ('2026-12-25'), ('2027-01-01');
        INSERT BillingRates (DebtorsCode, ServiceTypeId, Rate, DateEffective) VALUES ('D1', 1, 2.50, '2024-01-01'), ('D1', 1, 3.00, '2025-01-01');
        INSERT PrimaryClient (ClientName, UniqueClientId, AutoProcess, AccessKey) VALUES
            ('Acme Holdings', 'ACME', 1, 'legacy-acme-access-key-0001'),
            ('Duplicate Acme', 'acme', 0, NULL);
        INSERT SecondaryClient (ClientName, UniqueClientId, PrimaryClientId) VALUES
            ('Acme Payroll', 'ACME01', 1),
            ('Lost Child', 'LOST01', 99);
        INSERT BankAccount (AccountNo, BranchCode, Description, GLAccNo) VALUES ('1001001001', '198765', 'Acme nominated', '1234567'),
            ('2002002002', '198765', 'Acme nominated old', NULL);
        INSERT PrimaryClientBankAccountType (PrimaryClientId, BankAccountId, AccountTypeId) VALUES (1, 1, 1), (1, 2, 1);
        INSERT BankRun (BankFileName, DateTrn, StatusId) VALUES ('NED0001.txt', '2026-10-01', 2);
        INSERT Header (BatchId, BankRunId, UniqueClientId, ServiceTypeId, PaymentDate, UniquePaymentReference, ReconStatusId, DateTrn, SequenceNumber, Records, TotalValue) VALUES
            ('B-1', 1, 'ACME01', 1, '2026-10-01', 'REF-1', 1, '2026-09-30', 1, 2, 300.00),
            ('B-2', NULL, 'GONE99', 3, '2026-10-02', 'REF-1', 2, '2026-10-01', NULL, 1, 50.00);
        INSERT Transactions (BatchId, EmployeeCode, EmployeeSurname, BranchCode, AccountNumber, AccountType, Amount, TransactionStatusId, DateTrn) VALUES
            ('B-1', 'E1', 'Mokoena', '250655', '62000000001', '1', 100.00, 4, '2026-09-30'),
            ('B-1', 'E2', NULL, '250655', '62000000002', '1', 200.00, 3, '2026-09-30'),
            ('B-2', 'E3', 'Dlamini', '250655', '62000000003', '1', 50.00, 1, '2026-10-01'),
            ('B-2', 'E4', 'Zero', '250655', '62000000004', '1', 0, 1, '2026-10-01'),
            ('NOPE', 'E5', 'Nobody', '250655', '62000000005', '1', 10.00, 1, '2026-10-01');
        INSERT Rejections (BankRunId, AccountName, AccountNumber, BranchCode, Amount, ServiceTypeId, TransactionStatusId, TransactionId, RejectionReason) VALUES
            (1, 'Mokoena', '62000000001', '250655', 100.00, 1, 5, 1, 'Account closed');
        INSERT HeaderComment (HeaderId, Comment, CommentDate) VALUES (1, 'Funded late', '2026-10-01');
        """;

    private static async Task<ImportReport> ImportAsync(string legacy, string payOps, bool dryRun = false)
    {
        await using var l = LegacyImportFixture.LegacyContext(legacy);
        await using var p = LegacyImportFixture.PayOpsContext(payOps);
        return await new LegacyImporter(l, p, new ImportOptions(dryRun, ChunkSize: 2), TextWriter.Null).RunAsync();
    }

    [Fact]
    public async Task Import_copies_clients_batches_and_payments_and_reports_bad_rows()
    {
        var (legacy, payOps) = await fixture.CreateDatabasesAsync();
        await LegacyImportFixture.ExecuteAsync(legacy, SampleData);

        var report = await ImportAsync(legacy, payOps);

        await using var db = LegacyImportFixture.PayOpsContext(payOps);
        var acme = await db.PrimaryClients.SingleAsync(p => p.LegacyId == 1);
        Assert.Equal(("ACME", "Acme Holdings", true), (acme.UniqueClientId, acme.Name, acme.AutoProcess));
        Assert.False(await db.PrimaryClients.AnyAsync(p => p.LegacyId == 2));
        Assert.Contains(report.Issues, i => i.LegacyTable == "PrimaryClient" && i.LegacyKey == "2" && i.Action == "Skipped");

        var unmatched = await db.PrimaryClients.SingleAsync(p => p.UniqueClientId == LegacyImporter.UnmatchedPrimaryId);
        Assert.False(unmatched.IsActive);
        var lost = await db.SecondaryClients.SingleAsync(s => s.LegacyId == 2);
        Assert.Equal((unmatched.Id, true, false), (lost.PrimaryClientId, lost.RequiresReview, lost.IsActive));
        var placeholder = await db.SecondaryClients.SingleAsync(s => s.UniqueClientId == "GONE99");
        Assert.Equal((unmatched.Id, true), (placeholder.PrimaryClientId, placeholder.RequiresReview));

        var accounts = await db.ClientBankAccounts.Where(a => a.PrimaryClientId == acme.Id).OrderBy(a => a.LegacyId).ToListAsync();
        Assert.Equal([(1, false), (2, true)], accounts.Select(a => (a.LegacyId!.Value, a.IsActive)));

        var key = await db.ClientApiKeys.SingleAsync(k => k.PrimaryClientId == acme.Id);
        Assert.Equal(ApiKeyHasher.Hash("legacy-acme-access-key-0001"), key.KeyHash);
        Assert.DoesNotContain("acme-access", key.KeyPrefix);

        var batch1 = await db.PaymentSummaries.Include(s => s.PaymentRecords).SingleAsync(s => s.LegacyId == 1);
        Assert.Equal(("REF-1", 300m, 2, "B-1"), (batch1.UniqueReference, batch1.TotalValue, batch1.PaymentRecords.Count, batch1.LegacyBatchId));
        Assert.NotNull(batch1.PaymentBatchId);
        Assert.Contains(batch1.PaymentRecords, r => r.Surname == "(missing)");
        var batch2 = await db.PaymentSummaries.SingleAsync(s => s.LegacyId == 2);
        Assert.Equal(("LEGACY-2", -2L, placeholder.Id), (batch2.UniqueReference, batch2.SequenceNumber, batch2.SecondaryClientId));

        Assert.Equal(3, await db.PaymentRecordStatusHistories.CountAsync());
        Assert.Equal(1, await db.PaymentRejections.CountAsync(r => r.LegacyId == 1));
        Assert.Equal(1, await db.PaymentSummaryComments.CountAsync(c => c.LegacyId == 1));
        Assert.Equal(2, await db.PublicHolidays.CountAsync());
        Assert.Equal(3.00m, (await db.BillingRates.SingleAsync()).FeePerRecord);
        Assert.Equal(1, await db.BankClientProfiles.CountAsync(p => p.LegacyId == 1));

        Assert.Contains(report.Issues, i => i.LegacyTable == "Transactions" && i.LegacyKey == "4" && i.Reason.Contains("greater than zero"));
        Assert.Contains(report.Issues, i => i.LegacyTable == "Transactions" && i.LegacyKey == "5" && i.Reason.Contains("No Header"));
        Assert.False(report.TotalsMatch);
        Assert.Contains(report.BatchDifferences, d => d.HeaderId == 2);

        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await report.WriteAsync(dir);
        Assert.True(File.Exists(Path.Combine(dir, "issues.csv")));
        Assert.Contains("Totals match", await File.ReadAllTextAsync(Path.Combine(dir, "report.md")));
    }

    [Fact]
    public async Task Rerun_updates_in_place_without_duplicates()
    {
        var (legacy, payOps) = await fixture.CreateDatabasesAsync();
        await LegacyImportFixture.ExecuteAsync(legacy, SampleData);
        await ImportAsync(legacy, payOps);
        await LegacyImportFixture.ExecuteAsync(legacy, "UPDATE Transactions SET TransactionStatusId = 3 WHERE TransactionId = 3; UPDATE PrimaryClient SET ClientName = 'Acme Group' WHERE PrimaryClientId = 1;");

        var second = await ImportAsync(legacy, payOps);

        Assert.All(second.Steps, s => Assert.Equal(0, s.Inserted));
        Assert.Equal(1, second.Steps.Single(s => s.Name == "Payments (Transactions)").Updated);
        await using var db = LegacyImportFixture.PayOpsContext(payOps);
        Assert.Equal("Acme Group", (await db.PrimaryClients.SingleAsync(p => p.LegacyId == 1)).Name);
        Assert.Equal(3, await db.PaymentRecords.CountAsync());
        Assert.Equal(4, await db.PaymentRecordStatusHistories.CountAsync());
        Assert.Equal(2, await db.PaymentSummaries.CountAsync());
        Assert.Equal(3, await db.SecondaryClients.CountAsync());
        Assert.Equal(1, await db.ClientApiKeys.CountAsync());
    }

    [Fact]
    public async Task Dry_run_reports_but_keeps_nothing()
    {
        var (legacy, payOps) = await fixture.CreateDatabasesAsync();
        await LegacyImportFixture.ExecuteAsync(legacy, SampleData);

        var report = await ImportAsync(legacy, payOps, dryRun: true);

        Assert.Equal(2, report.Steps.Single(s => s.Name == "Batches (Header)").Inserted);
        Assert.Equal(3, report.Totals.Single(t => t.Item == "Payments").PayOps);
        await using var db = LegacyImportFixture.PayOpsContext(payOps);
        Assert.False(await db.PrimaryClients.AnyAsync());
        Assert.False(await db.PaymentRecords.AnyAsync());
        Assert.False(await db.AppUsers.AnyAsync());
    }

    [Fact]
    public async Task Clean_legacy_data_reconciles_exactly()
    {
        var (legacy, payOps) = await fixture.CreateDatabasesAsync();
        await LegacyImportFixture.ExecuteAsync(legacy, """
            INSERT PrimaryClient (ClientName, UniqueClientId) VALUES ('Beta', 'BETA');
            INSERT SecondaryClient (ClientName, UniqueClientId, PrimaryClientId) VALUES ('Beta Pay', 'BETA01', 1);
            INSERT Header (BatchId, UniqueClientId, ServiceTypeId, PaymentDate, UniquePaymentReference, ReconStatusId, SequenceNumber, Records, TotalValue)
                VALUES ('X1', 'BETA01', 2, '2026-11-02', 'BREF-1', 2, 7, 2, 12.34);
            INSERT Transactions (BatchId, EmployeeCode, EmployeeSurname, BranchCode, AccountNumber, AccountType, Amount, TransactionStatusId)
                VALUES ('X1', 'E1', 'A', '250655', '1', '1', 10.00, 1), ('X1', 'E2', 'B', '250655', '2', '1', 2.34, 1);
            """);

        var report = await ImportAsync(legacy, payOps);

        Assert.True(report.TotalsMatch, report.ToMarkdown());
        Assert.Single(report.Clients, c => c.UniqueClientId == "BETA01" && c.Matches);
    }
}
