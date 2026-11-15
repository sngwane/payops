using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PayOps.BatchFile.Data;
using PayOps.BatchFile.Data.Services;
using PayOps.Database.Entities;
using PayOps.Database.Lookups;

namespace PayOps.Tests;

[Collection(SqlServerCollection.Name)]
public class BatchSubmissionTests(SqlServerFixture fixture)
{
    private static byte[] Hash(string text) => SHA256.HashData(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task Accepted_batch_writes_summary_payments_history_contacts_outbox_and_upload_log()
    {
        await using var payOps = fixture.CreatePayOpsContext();
        var (client, _, caller) = await BatchTestData.AddClientWithKeyAsync(payOps);
        var contacts = new BatchNotifications([new BatchContactInfo("Payroll", "payroll@test.local", null)]);
        var batch = BatchTestData.Batch(client.UniqueClientId, notifications: contacts);

        await using var db = fixture.CreateBatchFileContext();
        var result = await BatchTestData.Service(db).SubmitAsync(caller, batch, BatchUploadFormat.Json, Hash("a"));

        Assert.True(result.IsSuccess, result.Error?.Message);
        var id = result.Value!.BatchId;
        await using var check = fixture.CreatePayOpsContext();
        var summary = await check.PaymentSummaries.Include(s => s.PaymentRecords).ThenInclude(r => r.StatusHistory).Include(s => s.Contacts).SingleAsync(s => s.Id == id);
        Assert.Equal((byte)ReconStatusCode.Unreconciled, summary.ReconStatusId);
        Assert.Equal(201.00m, summary.TotalValue);
        Assert.Equal(0m, summary.FeeTotal);
        Assert.All(summary.PaymentRecords, r => Assert.Equal((byte)TransactionStatusCode.Pending, r.TransactionStatusId));
        Assert.All(summary.PaymentRecords, r => Assert.Single(r.StatusHistory));
        Assert.Equal(ContactPurpose.UploadConfirmation, Assert.Single(summary.Contacts).Purpose);
        var message = await check.NotificationMessages.SingleAsync(m => m.PaymentSummaryId == id);
        Assert.Equal(NotificationType.UploadConfirmation, message.Type);
        Assert.Equal(NotificationMessageStatus.Pending, message.Status);
        var upload = await check.BatchUploads.SingleAsync(u => u.PaymentSummaryId == id);
        Assert.True(upload.IsAccepted);
        Assert.Null(upload.ErrorsJson);
    }

    [Fact]
    public async Task Footer_total_may_include_billing_fees_which_are_stored_separately()
    {
        await using var payOps = fixture.CreatePayOpsContext();
        var (client, _, caller) = await BatchTestData.AddClientWithKeyAsync(payOps);
        if (!await payOps.BillingRates.AnyAsync(r => r.ServiceTypeId == (byte)ServiceTypeCode.OneDay && r.EffectiveFrom == new DateOnly(2000, 1, 1)))
        {
            payOps.BillingRates.Add(new BillingRate { ServiceTypeId = (byte)ServiceTypeCode.OneDay, FeePerRecord = 2.50m, EffectiveFrom = new DateOnly(2000, 1, 1) });
            await payOps.SaveChangesAsync();
        }
        await using var db = fixture.CreateBatchFileContext();
        var service = BatchTestData.Service(db);

        var withFees = BatchTestData.Batch(client.UniqueClientId, serviceType: "1 DAY", paymentDate: FixedClock.Today.AddDays(1), footerTotal: 206.00m);
        var accepted = await service.SubmitAsync(caller, withFees, BatchUploadFormat.Json, Hash("fees"));
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal(5.00m, accepted.Value!.FeeTotal);
        Assert.Equal(201.00m, accepted.Value.TotalValue);

        var wrong = BatchTestData.Batch(client.UniqueClientId, serviceType: "ONEDAY", paymentDate: FixedClock.Today.AddDays(1), footerTotal: 205.00m);
        var rejected = await service.SubmitAsync(caller, wrong, BatchUploadFormat.Json, Hash("wrong"));
        Assert.Contains("footer.totalValue", rejected.Error!.Errors!.Keys);
    }

    [Fact]
    public async Task Invalid_batch_returns_field_errors_and_is_logged_without_storing_a_batch()
    {
        await using var payOps = fixture.CreatePayOpsContext();
        var (client, _, caller) = await BatchTestData.AddClientWithKeyAsync(payOps);
        var batch = BatchTestData.Batch(client.UniqueClientId, count: 3);
        var payments = batch.Payments.ToList();
        payments[1] = payments[1] with { AccountNumber = "62-0001", BranchCode = "2506" };
        payments[2] = payments[2] with { Amount = 10.555m };
        batch = batch with { Payments = payments };

        await using var db = fixture.CreateBatchFileContext();
        var result = await BatchTestData.Service(db).SubmitAsync(caller, batch, BatchUploadFormat.Json, Hash("bad"));

        Assert.Equal(BatchErrorKind.Validation, result.Error!.Kind);
        Assert.Contains("payments[1].accountNumber", result.Error.Errors!.Keys);
        Assert.Contains("payments[1].branchCode", result.Error.Errors.Keys);
        Assert.Contains("payments[2].amount", result.Error.Errors.Keys);
        await using var check = fixture.CreatePayOpsContext();
        Assert.False(await check.PaymentSummaries.AnyAsync(s => s.ClientReference == batch.Header.UniquePaymentReference));
        var upload = await check.BatchUploads.SingleAsync(u => u.UniqueReference == batch.Header.UniquePaymentReference);
        Assert.False(upload.IsAccepted);
        Assert.Contains("payments[1].accountNumber", upload.ErrorsJson);
    }

    [Fact]
    public async Task Record_count_service_type_and_payment_date_rules_are_enforced()
    {
        await using var payOps = fixture.CreatePayOpsContext();
        var (client, _, caller) = await BatchTestData.AddClientWithKeyAsync(payOps);
        var holiday = new DateOnly(2026, 11, 26);
        if (!await payOps.PublicHolidays.AnyAsync(h => h.HolidayDate == holiday))
        {
            payOps.PublicHolidays.Add(new PublicHoliday { HolidayDate = holiday, Name = "Test holiday" });
            await payOps.SaveChangesAsync();
        }
        await using var db = fixture.CreateBatchFileContext();
        var service = BatchTestData.Service(db);
        var id = client.UniqueClientId;

        async Task<string[]> Errors(BatchSubmission batch)
            => (await service.ValidateAsync(caller, batch)).Error?.Errors?.Keys.ToArray() ?? [];

        Assert.Empty(await Errors(BatchTestData.Batch(id)));
        Assert.Contains("footer.records", await Errors(BatchTestData.Batch(id) with { Footer = new BatchFooter(FixedClock.Today, 3, 201.00m) }));
        Assert.Contains("header.serviceType", await Errors(BatchTestData.Batch(id, serviceType: "RTL")));
        Assert.Contains("header.paymentDate", await Errors(BatchTestData.Batch(id, paymentDate: FixedClock.Today.AddDays(-1))));
        Assert.Contains("header.paymentDate", await Errors(BatchTestData.Batch(id, serviceType: "1DAY")));
        Assert.Contains("header.paymentDate", await Errors(BatchTestData.Batch(id, serviceType: "TWODAY", paymentDate: FixedClock.Today.AddDays(1))));
        Assert.Empty(await Errors(BatchTestData.Batch(id, serviceType: "2 DAY", paymentDate: FixedClock.Today.AddDays(2))));
        Assert.Contains("header.paymentDate", await Errors(BatchTestData.Batch(id, paymentDate: new DateOnly(2026, 11, 22))));
        Assert.Contains("header.paymentDate", await Errors(BatchTestData.Batch(id, paymentDate: holiday)));
    }

    [Fact]
    public void Two_business_days_skip_weekends_and_holidays()
    {
        var friday = new DateOnly(2026, 11, 20);
        Assert.Equal(new DateOnly(2026, 11, 24), BusinessCalendar.AddBusinessDays(friday, 2, new HashSet<DateOnly>()));
        Assert.Equal(new DateOnly(2026, 11, 25), BusinessCalendar.AddBusinessDays(friday, 2, new HashSet<DateOnly> { new(2026, 11, 23) }));
    }

    [Fact]
    public async Task Key_must_belong_to_the_header_client_but_a_primary_key_may_submit_for_its_secondaries()
    {
        await using var payOps = fixture.CreatePayOpsContext();
        var (client, _, caller) = await BatchTestData.AddClientWithKeyAsync(payOps);
        var (other, _, _) = await BatchTestData.AddClientWithKeyAsync(payOps);
        var (secondary, _, primaryCaller) = await BatchTestData.AddClientWithKeyAsync(payOps, primaryKey: true);
        await using var db = fixture.CreateBatchFileContext();
        var service = BatchTestData.Service(db);

        var mismatch = await service.ValidateAsync(caller, BatchTestData.Batch(other.UniqueClientId));
        Assert.Contains("header.uniqueClientId", mismatch.Error!.Errors!.Keys);
        Assert.True((await service.ValidateAsync(primaryCaller, BatchTestData.Batch(secondary.UniqueClientId))).IsSuccess);
        Assert.False((await service.ValidateAsync(primaryCaller, BatchTestData.Batch(client.UniqueClientId))).IsSuccess);
    }

    [Fact]
    public async Task Duplicate_sequence_is_a_conflict_and_an_identical_retry_returns_the_existing_batch()
    {
        await using var payOps = fixture.CreatePayOpsContext();
        var (client, _, caller) = await BatchTestData.AddClientWithKeyAsync(payOps);
        var batch = BatchTestData.Batch(client.UniqueClientId);
        await using var db = fixture.CreateBatchFileContext();
        var service = BatchTestData.Service(db);

        var first = await service.SubmitAsync(caller, batch, BatchUploadFormat.Json, Hash("same"));
        var retry = await service.SubmitAsync(caller, batch, BatchUploadFormat.Json, Hash("same"));
        var changed = await service.SubmitAsync(caller, batch with { Footer = batch.Footer }, BatchUploadFormat.Json, Hash("changed"));

        Assert.True(first.IsSuccess);
        Assert.Equal(BatchErrorKind.Conflict, retry.Error!.Kind);
        Assert.Equal(first.Value!.BatchId, retry.Error.ExistingBatchId);
        Assert.Contains("already accepted", retry.Error.Message);
        Assert.Contains("SequenceNumber", changed.Error!.Message);
        Assert.Equal(first.Value.BatchId, changed.Error.ExistingBatchId);
    }

    [Fact]
    public async Task Client_reference_may_repeat_like_legacy_and_each_batch_gets_its_own_unique_reference()
    {
        await using var payOps = fixture.CreatePayOpsContext();
        var (client, _, caller) = await BatchTestData.AddClientWithKeyAsync(payOps);
        var (other, _, otherCaller) = await BatchTestData.AddClientWithKeyAsync(payOps);
        await using var db = fixture.CreateBatchFileContext();
        var service = BatchTestData.Service(db);

        var first = await service.SubmitAsync(caller, BatchTestData.Batch(client.UniqueClientId, reference: client.UniqueClientId), BatchUploadFormat.Json, Hash("a"));
        var second = await service.SubmitAsync(caller, BatchTestData.Batch(client.UniqueClientId, reference: client.UniqueClientId), BatchUploadFormat.Json, Hash("b"));
        var otherClient = await service.SubmitAsync(otherCaller, BatchTestData.Batch(other.UniqueClientId, reference: client.UniqueClientId), BatchUploadFormat.Json, Hash("c"));

        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.True(otherClient.IsSuccess, otherClient.Error?.Message);
        Assert.Equal(client.UniqueClientId, second.Value!.ClientReference);
        Assert.NotEqual(first.Value!.UniqueReference, second.Value.UniqueReference);
        Assert.True(Guid.TryParse(second.Value.UniqueReference, out _));
    }

    [Fact]
    public async Task Legacy_xml_is_accepted_with_contacts_and_bad_xml_is_rejected()
    {
        await using var payOps = fixture.CreatePayOpsContext();
        var (client, _, caller) = await BatchTestData.AddClientWithKeyAsync(payOps);
        await using var db = fixture.CreateBatchFileContext();
        var service = BatchTestData.Service(db);
        var xml = BatchTestData.Xml(client.UniqueClientId, TestData.Unique("X"));

        var accepted = await service.SubmitXmlAsync(caller, Encoding.UTF8.GetBytes(xml), Hash(xml));
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        await using var check = fixture.CreatePayOpsContext();
        Assert.Equal(3, await check.BatchContacts.CountAsync(c => c.PaymentSummaryId == accepted.Value!.BatchId));

        var notXml = await service.SubmitXmlAsync(caller, Encoding.UTF8.GetBytes("<BulkPaymentXml><Unknown/></BulkPaymentXml>"), Hash("n"));
        Assert.Contains("xml", notXml.Error!.Errors!.Keys);
        var doctype = await service.SubmitXmlAsync(caller, Encoding.UTF8.GetBytes("<!DOCTYPE x [<!ENTITY e SYSTEM \"file:///etc/passwd\">]><BulkPaymentXml>&e;</BulkPaymentXml>"), Hash("d"));
        Assert.Contains("xml", doctype.Error!.Errors!.Keys);
        var badAmount = await service.SubmitXmlAsync(caller, Encoding.UTF8.GetBytes(BatchTestData.Xml(client.UniqueClientId, TestData.Unique("X"), amount: "12,50")), Hash("b"));
        Assert.Contains("payments[0].amount", badAmount.Error!.Errors!.Keys);
    }

    [Fact]
    public async Task Xml_payments_from_every_payments_section_are_read_utf16_is_accepted_and_repeated_headers_are_rejected()
    {
        await using var payOps = fixture.CreatePayOpsContext();
        var (client, _, caller) = await BatchTestData.AddClientWithKeyAsync(payOps);
        await using var db = fixture.CreateBatchFileContext();
        var service = BatchTestData.Service(db);
        var xml = BatchTestData.Xml(client.UniqueClientId, TestData.Unique("X"));
        var payments = xml[xml.IndexOf("<Payments>", StringComparison.Ordinal)..(xml.IndexOf("</Payments>", StringComparison.Ordinal) + "</Payments>".Length)];

        var twoSections = xml.Replace(payments, payments + payments);
        var mismatch = await service.ValidateXmlAsync(caller, Encoding.UTF8.GetBytes(twoSections));
        Assert.Contains("footer.records", mismatch.Error!.Errors!.Keys);
        var fixedFooter = twoSections.Replace("<Records>1</Records>", "<Records>2</Records>").Replace("<TotalValue>150.25</TotalValue>", "<TotalValue>300.50</TotalValue>");
        var both = await service.ValidateXmlAsync(caller, Encoding.UTF8.GetBytes(fixedFooter));
        Assert.True(both.IsSuccess, both.Error?.Message);
        Assert.Equal(2, both.Value!.Records);

        var header = xml[xml.IndexOf("<HeaderInfo>", StringComparison.Ordinal)..(xml.IndexOf("</HeaderInfo>", StringComparison.Ordinal) + "</HeaderInfo>".Length)];
        var repeated = await service.ValidateXmlAsync(caller, Encoding.UTF8.GetBytes(xml.Replace(header, header + header)));
        Assert.Contains("xml", repeated.Error!.Errors!.Keys);

        var utf16 = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(xml.Replace("utf-8", "utf-16"))).ToArray();
        var accepted = await service.SubmitXmlAsync(caller, utf16, SHA256.HashData(utf16));
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
    }

    [Fact]
    public async Task Trailing_newlines_are_rejected_in_numbers_and_text()
    {
        await using var payOps = fixture.CreatePayOpsContext();
        var (client, _, caller) = await BatchTestData.AddClientWithKeyAsync(payOps);
        var batch = BatchTestData.Batch(client.UniqueClientId);
        batch = batch with { Payments = [batch.Payments[0] with { AccountNumber = "62000000002\n", BankReference = "SAL1\n" }, batch.Payments[1]] };
        await using var db = fixture.CreateBatchFileContext();

        var result = await BatchTestData.Service(db).ValidateAsync(caller, batch);

        Assert.Contains("payments[0].accountNumber", result.Error!.Errors!.Keys);
        Assert.Contains("payments[0].bankReference", result.Error.Errors.Keys);
    }

    [Fact]
    public async Task Batch_of_50000_payments_is_accepted()
    {
        await using var payOps = fixture.CreatePayOpsContext();
        var (client, _, caller) = await BatchTestData.AddClientWithKeyAsync(payOps);
        await using var db = fixture.CreateBatchFileContext();

        var result = await BatchTestData.Service(db).SubmitAsync(caller, BatchTestData.Batch(client.UniqueClientId, count: 50_000, amount: 1.00m),
            BatchUploadFormat.Json, Hash("big"));

        Assert.True(result.IsSuccess, result.Error?.Message);
        await using var check = fixture.CreatePayOpsContext();
        Assert.Equal(50_000, await check.PaymentRecords.CountAsync(r => r.PaymentSummaryId == result.Value!.BatchId));
    }
}
