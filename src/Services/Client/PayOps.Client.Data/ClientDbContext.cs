using Microsoft.EntityFrameworkCore;
using PayOps.Database;
using PayOps.Database.Configurations;
using PayOps.Database.Entities;

namespace PayOps.Client.Data;

/// <summary>
/// Client API view of the shared PayOps database. Maps only the client tables and never owns migrations
/// (schema changes go through <see cref="PayOpsDbContext"/>).
/// </summary>
public class ClientDbContext(DbContextOptions<ClientDbContext> options) : DbContext(options)
{
    private static readonly HashSet<Type> IncludedConfigurations =
    [
        typeof(AccountTypeConfiguration), typeof(PrimaryClientConfiguration), typeof(SecondaryClientConfiguration),
        typeof(ClientApiKeyConfiguration), typeof(ClientBankAccountConfiguration)
    ];

    public DbSet<PrimaryClient> PrimaryClients => Set<PrimaryClient>();
    public DbSet<SecondaryClient> SecondaryClients => Set<SecondaryClient>();
    public DbSet<ClientApiKey> ClientApiKeys => Set<ClientApiKey>();
    public DbSet<ClientBankAccount> ClientBankAccounts => Set<ClientBankAccount>();
    public DbSet<AccountType> AccountTypes => Set<AccountType>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => PayOpsConventions.Apply(configurationBuilder);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PayOpsDbContext).Assembly, IncludedConfigurations.Contains);
        modelBuilder.Ignore<PaymentSummary>();
    }
}
