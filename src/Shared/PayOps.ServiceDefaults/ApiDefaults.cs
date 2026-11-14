using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PayOps.ServiceDefaults;

public static class ApiDefaults
{
    /// <summary>ProblemDetails (RFC 9457), OpenAPI and health checks shared by all PayOps APIs.</summary>
    public static IHealthChecksBuilder AddPayOpsApiDefaults(this IServiceCollection services, Action<OpenApiOptions>? configureOpenApi = null)
    {
        services.AddProblemDetails();
        services.AddOpenApi(options => configureOpenApi?.Invoke(options));
        return services.AddHealthChecks();
    }

    /// <summary>Exception/status-code ProblemDetails, authentication, authorization, OpenAPI + Swagger UI at /swagger (Development) and anonymous /health.</summary>
    public static WebApplication UsePayOpsApiDefaults(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/v1.json", app.Environment.ApplicationName);
                options.DocumentTitle = app.Environment.ApplicationName;
            });
        }
        app.MapHealthChecks("/health").AllowAnonymous();
        return app;
    }

}
