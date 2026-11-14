using PayOps.ServiceDefaults;

namespace PayOps.Client.Api.Authentication;

public static class ClientApiAuth
{
    public const string AdminRole = PayOpsRoles.ClientAdmin;
    public const string ReaderRole = PayOpsRoles.Reader;
    public const string ReadPolicy = "ClientRead";
    public const string WritePolicy = "ClientWrite";

    public static IServiceCollection AddClientApiAuth(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddPayOpsEntraAuthentication(configuration, environment);
        services.AddAuthorizationBuilder()
            .AddPolicy(ReadPolicy, policy => policy.RequireRole(AdminRole, ReaderRole))
            .AddPolicy(WritePolicy, policy => policy.RequireRole(AdminRole));
        return services;
    }
}
