using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayOps.Client.Data.Services;

namespace PayOps.Client.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClientData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ClientDbContext>(options => options.UseSqlServer(connectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<PrimaryClientService>();
        services.AddScoped<SecondaryClientService>();
        services.AddScoped<ClientBankAccountService>();
        services.AddScoped<ClientApiKeyService>();
        return services;
    }
}
