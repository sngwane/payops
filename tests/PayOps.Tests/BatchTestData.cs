using Microsoft.Extensions.Options;
using PayOps.BatchFile.Data;
using PayOps.BatchFile.Data.Services;
using PayOps.Client.Data.Services;
using PayOps.Database;
using PayOps.Database.Entities;

namespace PayOps.Tests;

/// <summary>Fixed clock: Monday 2026-11-16 08:00 UTC (10:00 in Johannesburg).</summary>
internal sealed class FixedClock : TimeProvider
{
    public static readonly DateOnly Today = new(2026, 11, 16);
    public override DateTimeOffset GetUtcNow() => new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);
}

internal static class BatchTestData
{
    public static async Task<(SecondaryClient Client, string Key, BatchCaller Caller)> AddClientWithKeyAsync(PayOpsDbContext db, bool primaryKey = false)
    {
        var secondary = await TestData.AddClientAsync(db);
        var key = ApiKeyHasher.Generate();
        var apiKey = new ClientApiKey
        {
            Name = "test",
            KeyPrefix = ApiKeyHasher.Prefix(key),
            KeyHash = ApiKeyHasher.Hash(key),
            PrimaryClientId = primaryKey ? secondary.PrimaryClientId : null,
            SecondaryClientId = primaryKey ? null : secondary.Id
        };
        db.ClientApiKeys.Add(apiKey);
        await db.SaveChangesAsync();
        var caller = primaryKey
            ? new BatchCaller(true, secondary.PrimaryClientId, secondary.PrimaryClient.UniqueClientId, apiKey.Id)
            : new BatchCaller(false, secondary.Id, secondary.UniqueClientId, apiKey.Id);
        return (secondary, key, caller);
    }

    public static BatchSubmission Batch(string uniqueClientId, int count = 2, string serviceType = "SDV", DateOnly? paymentDate = null,
        decimal amount = 100.50m, decimal? footerTotal = null, long? sequence = null, string? reference = null, BatchNotifications? notifications = null)
    {
        var payments = Enumerable.Range(1, count).Select(i => new BatchPaymentLine(
            $"E{i}", "Doe", "Jane", "J", "8001015009087", "0821234567", "jane@test.local", "250655", "62000000002", "1", amount, $"SAL{i}")).ToList();
        var header = new BatchHeader(uniqueClientId, paymentDate ?? FixedClock.Today, "Rejections", "250655", "62000000001",
            reference ?? TestData.Unique("R"), serviceType, sequence ?? Random.Shared.NextInt64(1, long.MaxValue));
        return new BatchSubmission(header, payments, new BatchFooter(FixedClock.Today, count, footerTotal ?? amount * count), notifications);
    }

    public static string Xml(string uniqueClientId, string reference, string paymentDate = "2026-11-16", string amount = "150.25") => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <BulkPaymentXml>
          <HeaderInfo>
            <UniqueClientId>{uniqueClientId}</UniqueClientId>
            <PaymentDate>{paymentDate}</PaymentDate>
            <RejectionAccountName>Rejections</RejectionAccountName>
            <RejectionBranchCode>250655</RejectionBranchCode>
            <RejectionAccountNumber>62000000001</RejectionAccountNumber>
            <UniquePaymentReference>{reference}</UniquePaymentReference>
            <ServiceType>SDV</ServiceType>
            <SequenceNumber>{Random.Shared.NextInt64(1, long.MaxValue)}</SequenceNumber>
          </HeaderInfo>
          <NotificationInfo>
            <UploadConfirmations><Contact Name="Payroll" Email="payroll@test.local" CellNumber="0821234567" /></UploadConfirmations>
            <RejectionConfirmations><Contact Name="Payroll" Email="payroll@test.local" /></RejectionConfirmations>
            <FundingConfirmations><Contact Name="Finance" Email="finance@test.local" /></FundingConfirmations>
          </NotificationInfo>
          <Payments>
            <Payment>
              <EmployeeCode>E1</EmployeeCode>
              <EmployeeSurname>Doe</EmployeeSurname>
              <EmployeeFirstname>Jane</EmployeeFirstname>
              <EmployeeInitials>J</EmployeeInitials>
              <EmployeeIdOrPassport>8001015009087</EmployeeIdOrPassport>
              <EmployeeCell>0821234567</EmployeeCell>
              <EmployeeEmail>jane@test.local</EmployeeEmail>
              <BranchCode>250655</BranchCode>
              <AccountNumber>62000000002</AccountNumber>
              <AccountType>1</AccountType>
              <Amount>{amount}</Amount>
              <BankReference>SAL1</BankReference>
            </Payment>
          </Payments>
          <FooterInfo>
            <CurrentDate>2026-11-16</CurrentDate>
            <Records>1</Records>
            <TotalValue>{amount}</TotalValue>
          </FooterInfo>
        </BulkPaymentXml>
        """;

    public static BatchSubmissionService Service(BatchFileDbContext db)
        => new(db, new FixedClock(), Options.Create(new BatchFileOptions()));
}
