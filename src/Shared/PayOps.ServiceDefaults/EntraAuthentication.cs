using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PayOps.ServiceDefaults;

public sealed class EntraOptions
{
    public const string SectionName = "Authentication";

    /// <summary>Only allowed to be false in the Development environment.</summary>
    public bool Enabled { get; set; } = true;
    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    public string TenantId { get; set; } = string.Empty;

    /// <summary>The API's Entra application ID URI or client ID (the token's "aud").</summary>
    public string Audience { get; set; } = string.Empty;
}

public static class EntraAuthentication
{
    /// <summary>
    /// Entra ID JWT bearer authentication as the default scheme. With Authentication:Enabled=false (Development only)
    /// every request is signed in with all PayOps roles.
    /// </summary>
    public static AuthenticationBuilder AddPayOpsEntraAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var options = configuration.GetSection(EntraOptions.SectionName).Get<EntraOptions>() ?? new EntraOptions();
        if (!options.Enabled)
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("Authentication can only be disabled in the Development environment.");
            return services.AddAuthentication(DevelopmentAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthHandler>(DevelopmentAuthHandler.SchemeName, null);
        }

        if (string.IsNullOrWhiteSpace(options.TenantId) || string.IsNullOrWhiteSpace(options.Audience))
            throw new InvalidOperationException("Authentication:TenantId and Authentication:Audience must be set when authentication is enabled.");
        return services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.Authority = $"{options.Instance.TrimEnd('/')}/{options.TenantId}/v2.0";
                jwt.Audience = options.Audience;
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters.RoleClaimType = "roles";
                jwt.TokenValidationParameters.NameClaimType = "name";
            });
    }
}

/// <summary>Development only: signs every request in with all PayOps roles.</summary>
public sealed class DevelopmentAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Development";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "developer") };
        claims.AddRange(PayOpsRoles.All.Select(role => new Claim(ClaimTypes.Role, role)));
        var identity = new ClaimsIdentity(claims, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
