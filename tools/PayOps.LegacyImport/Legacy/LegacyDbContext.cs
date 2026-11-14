using Microsoft.EntityFrameworkCore;

namespace PayOps.LegacyImport.Legacy;

/// <summary>Read-only view of the legacy BulkPayments database. Saving is blocked so the import can never write to legacy.</summary>
public sealed class LegacyDbContext(DbContextOptions<LegacyDbContext> options) : DbContext(options)
{
    public IQueryable<LegacyPrimaryClient> PrimaryClients => Set<LegacyPrimaryClient>().AsNoTracking();
    public IQueryable<LegacySecondaryClient> SecondaryClients => Set<LegacySecondaryClient>().AsNoTracking();
    public IQueryable<LegacyBankAccount> BankAccounts => Set<LegacyBankAccount>().AsNoTracking();
    public IQueryable<LegacyPrimaryClientBankAccountType> PrimaryClientBankAccountTypes => Set<LegacyPrimaryClientBankAccountType>().AsNoTracking();
    public IQueryable<LegacyBank> Banks => Set<LegacyBank>().AsNoTracking();
    public IQueryable<LegacyBankClientProfile> BankClientProfiles => Set<LegacyBankClientProfile>().AsNoTracking();
    public IQueryable<LegacyBillingRate> BillingRates => Set<LegacyBillingRate>().AsNoTracking();
    public IQueryable<LegacyPublicHoliday> PublicHolidays => Set<LegacyPublicHoliday>().AsNoTracking();
    public IQueryable<LegacyBankRun> BankRuns => Set<LegacyBankRun>().AsNoTracking();
    public IQueryable<LegacyHeader> Headers => Set<LegacyHeader>().AsNoTracking();
    public IQueryable<LegacyTransaction> Transactions => Set<LegacyTransaction>().AsNoTracking();
    public IQueryable<LegacyRejection> Rejections => Set<LegacyRejection>().AsNoTracking();
    public IQueryable<LegacyHeaderComment> HeaderComments => Set<LegacyHeaderComment>().AsNoTracking();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => throw new InvalidOperationException("The legacy database is read-only.");

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("The legacy database is read-only.");

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("dbo");
        b.Entity<LegacyPrimaryClient>().ToTable("PrimaryClient").HasKey(e => e.PrimaryClientId);
        b.Entity<LegacySecondaryClient>().ToTable("SecondaryClient").HasKey(e => e.SecondaryClientId);
        b.Entity<LegacyBankAccount>().ToTable("BankAccount").HasKey(e => e.BankAccountId);
        b.Entity<LegacyPrimaryClientBankAccountType>().ToTable("PrimaryClientBankAccountType").HasNoKey();
        b.Entity<LegacyBank>().ToTable("Bank").HasKey(e => e.BankId);
        b.Entity<LegacyBankClientProfile>().ToTable("BankClientProfile").HasKey(e => e.BankClientProfileId);
        b.Entity<LegacyBillingRate>().ToTable("BillingRates").HasNoKey();
        b.Entity<LegacyBillingRate>().Property(e => e.Rate).HasColumnType("decimal(18,2)");
        b.Entity<LegacyPublicHoliday>().ToTable("PublicHoliday").HasKey(e => e.PublicHolidayId);
        b.Entity<LegacyBankRun>().ToTable("BankRun").HasKey(e => e.BankRunId);
        b.Entity<LegacyHeader>().ToTable("Header").HasKey(e => e.HeaderId);
        b.Entity<LegacyHeader>().Property(e => e.TotalValue).HasColumnType("money");
        b.Entity<LegacyTransaction>().ToTable("Transactions").HasKey(e => e.TransactionId);
        b.Entity<LegacyTransaction>().Property(e => e.Amount).HasColumnType("money");
        b.Entity<LegacyRejection>().ToTable("Rejections").HasKey(e => e.RejectionId);
        b.Entity<LegacyRejection>().Property(e => e.Amount).HasColumnType("money");
        b.Entity<LegacyHeaderComment>().ToTable("HeaderComment").HasKey(e => e.CommentId);
    }
}
