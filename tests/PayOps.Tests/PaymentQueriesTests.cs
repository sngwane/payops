using PayOps.Database.Entities;
using PayOps.Database.Lookups;
using PayOps.Payments.Data.Queries;

namespace PayOps.Tests;

[Collection(SqlServerCollection.Name)]
public class PaymentQueriesTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Client_batch_is_returned_only_to_its_own_client()
    {
        int summaryId;
        string owner, other;
        await using (var db = fixture.CreatePayOpsContext())
        {
            var client = await TestData.AddClientAsync(db);
            var otherClient = await TestData.AddClientAsync(db);
            var summary = TestData.NewSummary(client.Id);
            summary.PaymentRecords.Add(TestData.NewRecord(100m));
            summary.PaymentRecords.Add(TestData.NewRecord(200m));
            db.PaymentSummaries.Add(summary);
            await db.SaveChangesAsync();
            (summaryId, owner, other) = (summary.Id, client.UniqueClientId, otherClient.UniqueClientId);
        }

        await using var payments = fixture.CreatePaymentsContext();
        var queries = new PaymentQueries(payments);

        var batch = await queries.GetClientBatchAsync(owner, summaryId);
        Assert.NotNull(batch);
        Assert.Equal(owner, batch.ClientUniqueId);
        Assert.Equal("Same Day Value", batch.ServiceType);
        Assert.Equal("Unreconciled", batch.ReconStatus);
        Assert.Equal([100m, 200m], batch.Records.Select(r => r.Amount));
        Assert.All(batch.Records, r => Assert.Equal("Pending", r.TransactionStatus));

        Assert.Null(await queries.GetClientBatchAsync(other, summaryId));
    }

    [Fact]
    public async Task Paid_returns_successful_payments_within_the_range()
    {
        var from = new DateTime(2031, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        long paidId;
        await using (var db = fixture.CreatePayOpsContext())
        {
            var client = await TestData.AddClientAsync(db);
            var summary = TestData.NewSummary(client.Id);
            var paid = TestData.NewRecord(150m);
            paid.TransactionStatusId = (byte)TransactionStatusCode.Success;
            var paidEarlier = TestData.NewRecord(160m);
            var transmitted = TestData.NewRecord(170m);
            summary.PaymentRecords.Add(paid);
            summary.PaymentRecords.Add(paidEarlier);
            summary.PaymentRecords.Add(transmitted);
            db.PaymentSummaries.Add(summary);
            await db.SaveChangesAsync();

            db.PaymentRecordStatusHistories.AddRange(
                new PaymentRecordStatusHistory { PaymentRecordId = paid.Id, TransactionStatusId = (byte)TransactionStatusCode.Success, ChangedDate = from.AddHours(9) },
                new PaymentRecordStatusHistory { PaymentRecordId = paidEarlier.Id, TransactionStatusId = (byte)TransactionStatusCode.Success, ChangedDate = from.AddDays(-1) },
                new PaymentRecordStatusHistory { PaymentRecordId = transmitted.Id, TransactionStatusId = (byte)TransactionStatusCode.Transmitted, ChangedDate = from.AddHours(9) });
            await db.SaveChangesAsync();
            paidId = paid.Id;
        }

        await using var payments = fixture.CreatePaymentsContext();
        var result = await new PaymentQueries(payments).GetPaidAsync(from, from.AddDays(1));

        var row = Assert.Single(result);
        Assert.Equal(paidId, row.PaymentRecordId);
        Assert.Equal(150m, row.Amount);
        Assert.Equal(from.AddHours(9), row.PaidDateUtc);
    }

    [Fact]
    public async Task Rejections_are_filtered_by_status()
    {
        long unreconciledId, pendingId;
        await using (var db = fixture.CreatePayOpsContext())
        {
            var client = await TestData.AddClientAsync(db);
            var summary = TestData.NewSummary(client.Id);
            var first = TestData.NewRecord(110m);
            var second = TestData.NewRecord(120m);
            first.TransactionStatusId = second.TransactionStatusId = (byte)TransactionStatusCode.Rejected;
            first.RejectionReason = "Account closed";
            summary.PaymentRecords.Add(first);
            summary.PaymentRecords.Add(second);
            db.PaymentSummaries.Add(summary);
            await db.SaveChangesAsync();

            var unreconciled = new PaymentRejection { PaymentRecordId = first.Id, AccountName = "Rej", BranchCode = "250655", AccountNumber = "62000000001", Amount = 110m };
            var pending = new PaymentRejection { PaymentRecordId = second.Id, AccountName = "Rej", BranchCode = "250655", AccountNumber = "62000000001", Amount = 120m, TransactionStatusId = (byte)TransactionStatusCode.Pending };
            db.PaymentRejections.AddRange(unreconciled, pending);
            await db.SaveChangesAsync();
            (unreconciledId, pendingId) = (unreconciled.Id, pending.Id);
        }

        await using var payments = fixture.CreatePaymentsContext();
        var queries = new PaymentQueries(payments);

        var unreconciledRows = await queries.GetRejectionsAsync(TransactionStatusCode.PendingUnreconciled);
        var row = Assert.Single(unreconciledRows, r => r.Id == unreconciledId);
        Assert.Equal("Account closed", row.PaymentReason);
        Assert.Null(row.RefundReason);
        Assert.Null(row.RefundBatchReference);
        Assert.Null(row.RefundFileName);
        Assert.DoesNotContain(unreconciledRows, r => r.Id == pendingId);

        var pendingRows = await queries.GetRejectionsAsync(TransactionStatusCode.Pending);
        Assert.Contains(pendingRows, r => r.Id == pendingId);
        Assert.DoesNotContain(pendingRows, r => r.Id == unreconciledId);
    }

    [Fact]
    public async Task Rejected_refund_returns_both_runs_and_reasons()
    {
        long refundId;
        string paymentRun, refundRun;
        await using (var db = fixture.CreatePayOpsContext())
        {
            var maker = await TestData.AddUserAsync(db);
            var bank = await TestData.AddBankAsync(db);
            var client = await TestData.AddClientAsync(db);
            var originalBatch = new PaymentBatch { BankId = bank.Id, Reference = TestData.Unique("BR"), Name = "Run A", FileName = "runA.txt", RunDate = DateTime.UtcNow, CreatedByUserId = maker.Id };
            var refundBatch = new PaymentBatch { BankId = bank.Id, Reference = TestData.Unique("BR"), Name = "Run B", FileName = "runB.txt", RunDate = DateTime.UtcNow, CreatedByUserId = maker.Id };
            var summary = TestData.NewSummary(client.Id);
            summary.PaymentBatch = originalBatch;
            var record = TestData.NewRecord(130m);
            record.TransactionStatusId = (byte)TransactionStatusCode.Rejected;
            record.RejectionReason = "Account closed";
            summary.PaymentRecords.Add(record);
            db.PaymentSummaries.Add(summary);
            db.PaymentBatches.Add(refundBatch);
            await db.SaveChangesAsync();

            var refund = new PaymentRejection
            {
                PaymentRecordId = record.Id, PaymentBatch = refundBatch, AccountName = "Rej", BranchCode = "250655",
                AccountNumber = "62000000001", Amount = 130m, TransactionStatusId = (byte)TransactionStatusCode.Rejected,
                RejectionReason = "Invalid refund account"
            };
            db.PaymentRejections.Add(refund);
            await db.SaveChangesAsync();
            (refundId, paymentRun, refundRun) = (refund.Id, originalBatch.Reference, refundBatch.Reference);
        }

        await using var payments = fixture.CreatePaymentsContext();
        var rows = await new PaymentQueries(payments).GetRejectionsAsync(TransactionStatusCode.Rejected);

        var row = Assert.Single(rows, r => r.Id == refundId);
        Assert.Equal(paymentRun, row.PaymentBatchReference);
        Assert.Equal("runA.txt", row.PaymentFileName);
        Assert.Equal(refundRun, row.RefundBatchReference);
        Assert.Equal("runB.txt", row.RefundFileName);
        Assert.Equal("Account closed", row.PaymentReason);
        Assert.Equal("Invalid refund account", row.RefundReason);
    }
}
