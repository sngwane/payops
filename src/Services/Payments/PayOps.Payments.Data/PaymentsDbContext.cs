using Microsoft.EntityFrameworkCore;
using PayOps.Database;
using PayOps.Database.Configurations;
using PayOps.Database.Entities;

namespace PayOps.Payments.Data;

/// <summary>
/// Payments API view of the shared PayOps database. Maps only the tables this API uses and never owns migrations
/// (schema changes go through <see cref="PayOpsDbContext"/>).
/// </summary>
public class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    private static readonly HashSet<Type> IncludedConfigurations =
    [
        typeof(BatchStatusConfiguration), typeof(ReconStatusConfiguration), typeof(TransactionStatusConfiguration),
        typeof(ServiceTypeConfiguration), typeof(ApprovalStatusConfiguration), typeof(AppUserConfiguration),
        typeof(PrimaryClientConfiguration), typeof(SecondaryClientConfiguration), typeof(BankConfiguration),
        typeof(PaymentBatchConfiguration), typeof(PaymentSummaryConfiguration), typeof(PaymentSummaryCommentConfiguration),
        typeof(PaymentRecordConfiguration), typeof(PaymentRejectionConfiguration), typeof(PaymentRecordStatusHistoryConfiguration),
        typeof(BatchContactConfiguration), typeof(NotificationMessageConfiguration)
    ];

    public DbSet<PaymentBatch> PaymentBatches => Set<PaymentBatch>();
    public DbSet<PaymentSummary> PaymentSummaries => Set<PaymentSummary>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();
    public DbSet<PaymentRejection> PaymentRejections => Set<PaymentRejection>();
    public DbSet<PaymentRecordStatusHistory> PaymentRecordStatusHistories => Set<PaymentRecordStatusHistory>();
    public DbSet<SecondaryClient> SecondaryClients => Set<SecondaryClient>();
    public DbSet<PaymentSummaryComment> PaymentSummaryComments => Set<PaymentSummaryComment>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<NotificationMessage> NotificationMessages => Set<NotificationMessage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => PayOpsConventions.Apply(configurationBuilder);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(PayOpsDbContext).Assembly, IncludedConfigurations.Contains);
}
