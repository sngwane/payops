using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PayOps.ServiceDefaults;

public static class ApiDefaults
{
    /// <summary>ProblemDetails (RFC 9457), OpenAPI and health checks shared by all PayOps APIs.</summary>
    public static IHealthChecksBuilder AddPayOpsApiDefaults(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddOpenApi();
        return services.AddHealthChecks();
    }

    /// <summary>Exception/status-code ProblemDetails, authentication, authorization, OpenAPI (Development) and anonymous /health.</summary>
    public static WebApplication UsePayOpsApiDefaults(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();
        if (app.Environment.IsDevelopment())
            app.MapOpenApi();
        app.MapHealthChecks("/health").AllowAnonymous();
        return app;
    }

}
