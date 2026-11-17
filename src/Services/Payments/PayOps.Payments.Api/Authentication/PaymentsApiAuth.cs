using PayOps.ServiceDefaults;

namespace PayOps.Payments.Api.Authentication;

public static class PaymentsApiAuth
{
    public const string ReadPolicy = "PaymentsRead";
    public const string WritePolicy = "PaymentsWrite";

    public static IServiceCollection AddPaymentsApiAuth(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddPayOpsEntraAuthentication(configuration, environment);
        services.AddAuthorizationBuilder()
            .AddPolicy(ReadPolicy, policy => policy.RequireRole(PayOpsRoles.Reader, PayOpsRoles.Operator, PayOpsRoles.Approver, PayOpsRoles.ClientAdmin))
            .AddPolicy(WritePolicy, policy => policy.RequireRole(PayOpsRoles.Operator));
        services.AddHttpContextAccessor();
        services.AddScoped<CurrentStaff>();
        return services;
    }
}
