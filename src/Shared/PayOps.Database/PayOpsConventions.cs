using Microsoft.EntityFrameworkCore;

namespace PayOps.Database;

/// <summary>Model-wide conventions shared by every PayOps DbContext.</summary>
public static class PayOpsConventions
{
    public static void Apply(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<DateTime>().HavePrecision(3);
        configurationBuilder.Properties<string>().AreUnicode();
    }
}
