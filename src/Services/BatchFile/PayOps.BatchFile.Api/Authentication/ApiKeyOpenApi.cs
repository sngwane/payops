using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PayOps.BatchFile.Api.Authentication;

public static class ApiKeyOpenApi
{
    private const string SchemeId = "ApiKey";

    /// <summary>Declares the <c>X-Api-Key</c> header so Swagger UI's Authorize box sends it with client requests.</summary>
    public static OpenApiOptions AddApiKeySecurityScheme(this OpenApiOptions options) => options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = ApiKeyAuthenticationHandler.HeaderName,
            Description = "Client API key created through the Client API."
        };
        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(SchemeId, document)] = [] });
        return Task.CompletedTask;
    });
}
