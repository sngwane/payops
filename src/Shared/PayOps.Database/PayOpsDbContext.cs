using PayOps.Database.Configurations;
using Microsoft.EntityFrameworkCore;
using PayOps.Database.Entities;

namespace PayOps.Database;

/// <summary>Full PayOps model. The only context that owns migrations.</summary>
public class PayOpsDbContext(DbContextOptions<PayOpsDbContext> options) : DbContext(options)
{
    public DbSet<BatchStatus> BatchStatuses => Set<BatchStatus>();
    public DbSet<ReconStatus> ReconStatuses => Set<ReconStatus>();
    public DbSet<TransactionStatus> TransactionStatuses => Set<TransactionStatus>();
    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();
    public DbSet<AccountType> AccountTypes => Set<AccountType>();
    public DbSet<ApprovalStatus> ApprovalStatuses => Set<ApprovalStatus>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<PrimaryClient> PrimaryClients => Set<PrimaryClient>();
    public DbSet<SecondaryClient> SecondaryClients => Set<SecondaryClient>();
    public DbSet<ClientApiKey> ClientApiKeys => Set<ClientApiKey>();
    public DbSet<ClientBankAccount> ClientBankAccounts => Set<ClientBankAccount>();
    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<BankClientProfile> BankClientProfiles => Set<BankClientProfile>();
    public DbSet<PaymentBatch> PaymentBatches => Set<PaymentBatch>();
    public DbSet<PaymentSummary> PaymentSummaries => Set<PaymentSummary>();
    public DbSet<PaymentSummaryComment> PaymentSummaryComments => Set<PaymentSummaryComment>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();
    public DbSet<PaymentRejection> PaymentRejections => Set<PaymentRejection>();
    public DbSet<PaymentRecordStatusHistory> PaymentRecordStatusHistories => Set<PaymentRecordStatusHistory>();
    public DbSet<BatchUpload> BatchUploads => Set<BatchUpload>();
    public DbSet<PublicHoliday> PublicHolidays => Set<PublicHoliday>();
    public DbSet<BillingRate> BillingRates => Set<BillingRate>();
    public DbSet<BatchContact> BatchContacts => Set<BatchContact>();
    public DbSet<NotificationMessage> NotificationMessages => Set<NotificationMessage>();
    public DbSet<JobLease> JobLeases => Set<JobLease>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => PayOpsConventions.Apply(configurationBuilder);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PayOpsDbContext).Assembly);
        LegacyKeyConfiguration.Apply(modelBuilder);
    }
}
