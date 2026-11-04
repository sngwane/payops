using PayOps.Database;
using PayOps.Database.Entities;
using PayOps.Database.Lookups;

namespace PayOps.Tests;

/// <summary>Creates isolated, uniquely-keyed test data so tests can share one database.</summary>
internal static class TestData
{
    public static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..20];

    public static async Task<AppUser> AddUserAsync(PayOpsDbContext db)
    {
        var name = Unique("u");
        var user = new AppUser { ExternalId = Guid.NewGuid(), Username = name, Email = $"{name}@test.local", FirstName = "Test", LastName = "User" };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public static async Task<SecondaryClient> AddClientAsync(PayOpsDbContext db)
    {
        var primary = new PrimaryClient { UniqueClientId = Unique("P"), Name = "Primary" };
        var secondary = new SecondaryClient { PrimaryClient = primary, UniqueClientId = Unique("S"), Name = "Secondary " + Unique("") };
        db.SecondaryClients.Add(secondary);
        await db.SaveChangesAsync();
        return secondary;
    }

    public static async Task<Bank> AddBankAsync(PayOpsDbContext db)
    {
        var bank = new Bank { Code = Unique("B"), Name = "Test bank" };
        db.Banks.Add(bank);
        await db.SaveChangesAsync();
        return bank;
    }

    public static PaymentSummary NewSummary(int secondaryClientId, long sequenceNumber = 1) => new()
    {
        SecondaryClientId = secondaryClientId,
        ServiceTypeId = (byte)ServiceTypeCode.SDV,
        PaymentDate = new DateOnly(2026, 11, 3),
        SequenceNumber = sequenceNumber,
        UniqueReference = Unique("REF"),
        TotalRecords = 2,
        TotalValue = 300m,
        RejectionAccountName = "Rejections",
        RejectionBranchCode = "250655",
        RejectionAccountNumber = "62000000001"
    };

    public static PaymentRecord NewRecord(decimal amount = 100m, string accountType = "1") => new()
    {
        EmployeeCode = Unique("E"),
        Surname = "Doe",
        FirstName = "Jane",
        BranchCode = "250655",
        AccountNumber = "62000000002",
        AccountType = accountType,
        Amount = amount
    };
}
