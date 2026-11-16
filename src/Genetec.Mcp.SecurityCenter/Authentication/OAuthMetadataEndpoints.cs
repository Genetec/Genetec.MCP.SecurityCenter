using Genetec.Mcp.SecurityCenter.Configuration;
using Microsoft.Extensions.Options;

namespace Genetec.Mcp.SecurityCenter.Authentication;

public static class OAuthMetadataEndpoints
{
    public static WebApplication MapOAuthMetadataEndpoints(this WebApplication app)
    {
        app.MapGet("/.well-known/oauth-protected-resource", (HttpContext context, IOptions<AuthenticationOptions> opts) =>
        {
            var baseUrl = GetBaseUrl(context);
            return Results.Json(new
            {
                resource = $"{baseUrl}{McpServerEndpoints.Mcp}",
                authorization_servers = new[] { baseUrl },
                bearer_methods_supported = new[] { "header" }
            });
        }).AllowAnonymous();

        app.MapGet("/.well-known/oauth-authorization-server", (HttpContext context, IOptions<AuthenticationOptions> opts) =>
        {
            var baseUrl = GetBaseUrl(context);
            return Results.Json(new
            {
                issuer = baseUrl,
                authorization_endpoint = $"{baseUrl}/authorize",
                token_endpoint = $"{baseUrl}/token",
                registration_endpoint = $"{baseUrl}/register",
                token_endpoint_auth_methods_supported = new[] { "client_secret_post", "none" },
                grant_types_supported = new[] { "authorization_code", "refresh_token" },
                response_types_supported = new[] { "code" },
                code_challenge_methods_supported = new[] { "S256" }
            });
        }).AllowAnonymous();

        return app;
    }

    private static string GetBaseUrl(HttpContext context)
    {
        var request = context.Request;
        return $"{request.Scheme}://{request.Host}";
    }
}
