using Microsoft.EntityFrameworkCore;
using PayOps.Database;
using PayOps.Payments.Data;
using Testcontainers.MsSql;

namespace PayOps.Tests;

/// <summary>Starts a throwaway SQL Server 2022 container and applies all PayOps migrations once per test run.</summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "PayOps"
        };
        ConnectionString = builder.ConnectionString;
        await using var db = CreatePayOpsContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public PayOpsDbContext CreatePayOpsContext()
        => new(new DbContextOptionsBuilder<PayOpsDbContext>().UseSqlServer(ConnectionString).Options);

    public PaymentsDbContext CreatePaymentsContext()
        => new(new DbContextOptionsBuilder<PaymentsDbContext>().UseSqlServer(ConnectionString).Options);
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
