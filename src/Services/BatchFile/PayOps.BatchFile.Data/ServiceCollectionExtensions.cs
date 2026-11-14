using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayOps.BatchFile.Data.Services;

namespace PayOps.BatchFile.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBatchFileData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<BatchFileDbContext>(options => options.UseSqlServer(connectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddOptions<BatchFileOptions>();
        services.AddScoped<BatchSubmissionService>();
        services.AddScoped<BatchQueries>();
        services.AddScoped<ReferenceDataService>();
        return services;
    }
}
