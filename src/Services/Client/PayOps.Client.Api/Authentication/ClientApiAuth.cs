using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace PayOps.Client.Api.Authentication;

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

public static class ClientApiAuth
{
    public const string AdminRole = "PayOps.ClientAdmin";
    public const string ReaderRole = "PayOps.Reader";
    public const string ReadPolicy = "ClientRead";
    public const string WritePolicy = "ClientWrite";

    public static IServiceCollection AddClientApiAuth(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var options = configuration.GetSection(EntraOptions.SectionName).Get<EntraOptions>() ?? new EntraOptions();
        if (options.Enabled)
        {
            if (string.IsNullOrWhiteSpace(options.TenantId) || string.IsNullOrWhiteSpace(options.Audience))
                throw new InvalidOperationException("Authentication:TenantId and Authentication:Audience must be set when authentication is enabled.");
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(jwt =>
                {
                    jwt.Authority = $"{options.Instance.TrimEnd('/')}/{options.TenantId}/v2.0";
                    jwt.Audience = options.Audience;
                    jwt.MapInboundClaims = false;
                    jwt.TokenValidationParameters.RoleClaimType = "roles";
                    jwt.TokenValidationParameters.NameClaimType = "name";
                });
        }
        else
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("Authentication can only be disabled in the Development environment.");
            services.AddAuthentication(DevelopmentAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthHandler>(DevelopmentAuthHandler.SchemeName, null);
        }

        services.AddAuthorizationBuilder()
            .AddPolicy(ReadPolicy, policy => policy.RequireRole(AdminRole, ReaderRole))
            .AddPolicy(WritePolicy, policy => policy.RequireRole(AdminRole));
        return services;
    }
}

/// <summary>Development only: signs every request in as a client admin.</summary>
public sealed class DevelopmentAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Development";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "developer"), new Claim(ClaimTypes.Role, ClientApiAuth.AdminRole)], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
