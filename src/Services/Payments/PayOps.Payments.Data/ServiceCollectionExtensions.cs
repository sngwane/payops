using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayOps.Payments.Data.Queries;
using PayOps.Payments.Data.Services;

namespace PayOps.Payments.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentsData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<PaymentsDbContext>(options => options.UseSqlServer(connectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<PaymentQueries>();
        services.AddScoped<StaffUserService>();
        services.AddScoped<OpsBatchQueries>();
        services.AddScoped<BatchOperationsService>();
        services.AddScoped<PaidQueries>();
        services.AddScoped<RejectionService>();
        return services;
    }
}
