using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PayOps.Database;

/// <summary>Used by the dotnet-ef tools. Set PAYOPS_CONNECTION to target a specific server.</summary>
public class PayOpsDbContextFactory : IDesignTimeDbContextFactory<PayOpsDbContext>
{
    public PayOpsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("PAYOPS_CONNECTION")
            ?? "Server=localhost;Database=PayOps;Integrated Security=true;TrustServerCertificate=true";
        var options = new DbContextOptionsBuilder<PayOpsDbContext>().UseSqlServer(connectionString).Options;
        return new PayOpsDbContext(options);
    }
}
