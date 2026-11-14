using Microsoft.EntityFrameworkCore;
using PayOps.Database;
using PayOps.Database.Configurations;
using PayOps.Database.Entities;

namespace PayOps.BatchFile.Data;

/// <summary>
/// Batch File API view of the shared PayOps database. Maps only the tables this API uses and never owns migrations
/// (schema changes go through <see cref="PayOpsDbContext"/>).
/// </summary>
public class BatchFileDbContext(DbContextOptions<BatchFileDbContext> options) : DbContext(options)
{
    private static readonly HashSet<Type> IncludedConfigurations =
    [
        typeof(BatchStatusConfiguration), typeof(ReconStatusConfiguration), typeof(TransactionStatusConfiguration),
        typeof(ServiceTypeConfiguration), typeof(ApprovalStatusConfiguration), typeof(AppUserConfiguration),
        typeof(PrimaryClientConfiguration), typeof(SecondaryClientConfiguration), typeof(ClientApiKeyConfiguration), typeof(BankConfiguration),
        typeof(PaymentBatchConfiguration), typeof(PaymentSummaryConfiguration), typeof(PaymentSummaryCommentConfiguration),
        typeof(PaymentRecordConfiguration), typeof(PaymentRejectionConfiguration), typeof(PaymentRecordStatusHistoryConfiguration),
        typeof(BatchUploadConfiguration), typeof(PublicHolidayConfiguration), typeof(BillingRateConfiguration),
        typeof(BatchContactConfiguration), typeof(NotificationMessageConfiguration)
    ];

    public DbSet<SecondaryClient> SecondaryClients => Set<SecondaryClient>();
    public DbSet<PaymentSummary> PaymentSummaries => Set<PaymentSummary>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();
    public DbSet<BatchUpload> BatchUploads => Set<BatchUpload>();
    public DbSet<PublicHoliday> PublicHolidays => Set<PublicHoliday>();
    public DbSet<BillingRate> BillingRates => Set<BillingRate>();
    public DbSet<NotificationMessage> NotificationMessages => Set<NotificationMessage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => PayOpsConventions.Apply(configurationBuilder);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PayOpsDbContext).Assembly, IncludedConfigurations.Contains);
        modelBuilder.Ignore<ClientBankAccount>();
    }
}
