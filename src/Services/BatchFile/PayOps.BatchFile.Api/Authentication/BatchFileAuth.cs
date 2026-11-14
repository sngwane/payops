using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PayOps.BatchFile.Data;
using PayOps.Client.Data;
using PayOps.Client.Data.Services;
using PayOps.ServiceDefaults;

namespace PayOps.BatchFile.Api.Authentication;

public static class BatchFileAuth
{
    public const string ClientPolicy = "BatchClient";
    public const string StaffReadPolicy = "BatchStaffRead";
    public const string StaffWritePolicy = "BatchStaffWrite";

    public static IServiceCollection AddBatchFileAuth(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddPayOpsEntraAuthentication(configuration, environment)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
        services.AddAuthorizationBuilder()
            .AddPolicy(ClientPolicy, policy => policy
                .AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser()
                .RequireClaim(ApiKeyAuthenticationHandler.ClientIdClaim))
            .AddPolicy(StaffReadPolicy, policy => policy.RequireRole(PayOpsRoles.Reader, PayOpsRoles.Operator, PayOpsRoles.ClientAdmin))
            .AddPolicy(StaffWritePolicy, policy => policy.RequireRole(PayOpsRoles.Operator));
        return services;
    }

    /// <summary>The client signed in by <see cref="ApiKeyAuthenticationHandler"/>.</summary>
    public static BatchCaller GetBatchCaller(this ClaimsPrincipal user) => new(
        user.FindFirstValue(ApiKeyAuthenticationHandler.ClientKindClaim) == nameof(ClientKind.Primary),
        int.Parse(user.FindFirstValue(ApiKeyAuthenticationHandler.ClientIdClaim)!, CultureInfo.InvariantCulture),
        user.FindFirstValue(ApiKeyAuthenticationHandler.UniqueClientIdClaim)!,
        int.Parse(user.FindFirstValue(ApiKeyAuthenticationHandler.ApiKeyIdClaim)!, CultureInfo.InvariantCulture));
}

/// <summary>Signs a client's system in with the <c>X-Api-Key</c> header, validated by the Client API's key service.</summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ClientApiKeyService keys)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string ClientKindClaim = "payops:client_kind";
    public const string ClientIdClaim = "payops:client_id";
    public const string UniqueClientIdClaim = "payops:unique_client_id";
    public const string ApiKeyIdClaim = "payops:api_key_id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values) || string.IsNullOrWhiteSpace(values.ToString()))
            return AuthenticateResult.NoResult();
        var owner = await keys.ValidateApiKeyAsync(values.ToString(), Context.RequestAborted);
        if (owner is null)
            return AuthenticateResult.Fail("Invalid, revoked or expired API key.");
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, owner.UniqueClientId),
            new Claim(ClientKindClaim, owner.Kind.ToString()),
            new Claim(ClientIdClaim, owner.ClientId.ToString(CultureInfo.InvariantCulture)),
            new Claim(UniqueClientIdClaim, owner.UniqueClientId),
            new Claim(ApiKeyIdClaim, owner.ApiKeyId.ToString(CultureInfo.InvariantCulture))
        ], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
