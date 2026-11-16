using System.Text.Json.Serialization;

namespace Genetec.Mcp.SecurityCenter.Authentication;

public static class ClientRegistrationEndpoint
{
    private static readonly HashSet<string> AllowedGrantTypes = ["authorization_code"];

    public static WebApplication MapClientRegistrationEndpoint(this WebApplication app)
    {
        app.MapPost("/register", async (HttpContext context) =>
        {
            ClientRegistrationRequest? body;
            try
            {
                body = await context.Request.ReadFromJsonAsync<ClientRegistrationRequest>();
            }
            catch
            {
                return Results.BadRequest(new ClientRegistrationError
                {
                    Error = "invalid_client_metadata",
                    ErrorDescription = "Invalid JSON body"
                });
            }

            if (body is null)
            {
                return Results.BadRequest(new ClientRegistrationError
                {
                    Error = "invalid_client_metadata",
                    ErrorDescription = "Request body is required"
                });
            }

            var store = context.RequestServices.GetRequiredService<ClientRegistrationStore>();
            var (client, error) = ValidateAndRegister(body, store);

            if (error is not null)
                return Results.BadRequest(error);

            return Results.Json(new
            {
                client_id = client!.ClientId,
                client_secret = client.ClientSecret,
                client_id_issued_at = client.ClientIdIssuedAt,
                client_secret_expires_at = client.ClientSecretExpiresAt,
                redirect_uris = client.RedirectUris,
                grant_types = client.GrantTypes,
                token_endpoint_auth_method = client.TokenEndpointAuthMethod,
                client_name = client.ClientName
            }, statusCode: 201);
        }).AllowAnonymous();

        return app;
    }

    public static (RegisteredClient? Client, ClientRegistrationError? Error) ValidateAndRegister(
        ClientRegistrationRequest request, ClientRegistrationStore store)
    {
        if (request.RedirectUris is null || request.RedirectUris.Count == 0)
        {
            return (null, new ClientRegistrationError
            {
                Error = "invalid_client_metadata",
                ErrorDescription = "redirect_uris is required and must not be empty"
            });
        }

        foreach (var uri in request.RedirectUris)
        {
            if (!OAuthRedirectUri.IsAllowed(uri))
            {
                return (null, new ClientRegistrationError
                {
                    Error = "invalid_client_metadata",
                    ErrorDescription = "redirect_uris must use HTTPS, or HTTP on loopback, without credentials or fragments"
                });
            }
        }

        var requestedGrantTypes = request.GrantTypes is { Count: > 0 } ? request.GrantTypes : ["authorization_code"];
        if (requestedGrantTypes.Contains("client_credentials"))
            return (null, new ClientRegistrationError
            {
                Error = "invalid_client_metadata",
                ErrorDescription = "Dynamically registered clients must use authorization_code with user sign-in."
            });
        var grantTypes = requestedGrantTypes.Where(AllowedGrantTypes.Contains).ToList();
        if (grantTypes.Count == 0)
        {
            return (null, new ClientRegistrationError
            {
                Error = "invalid_client_metadata",
                ErrorDescription = "grant_types must include authorization_code"
            });
        }

        var authMethod = request.TokenEndpointAuthMethod ?? "client_secret_post";
        if (authMethod != "client_secret_post" && authMethod != "none")
        {
            return (null, new ClientRegistrationError
            {
                Error = "invalid_client_metadata",
                ErrorDescription = "token_endpoint_auth_method must be client_secret_post or none"
            });
        }

        var client = store.Register(
            request.RedirectUris,
            grantTypes,
            authMethod,
            request.ClientName);

        return (client, null);
    }
}

public class ClientRegistrationRequest
{
    [JsonPropertyName("redirect_uris")]
    public List<string>? RedirectUris { get; set; }

    [JsonPropertyName("grant_types")]
    public List<string>? GrantTypes { get; set; }

    [JsonPropertyName("token_endpoint_auth_method")]
    public string? TokenEndpointAuthMethod { get; set; }

    [JsonPropertyName("client_name")]
    public string? ClientName { get; set; }
}

public class ClientRegistrationError
{
    [JsonPropertyName("error")]
    public required string Error { get; init; }

    [JsonPropertyName("error_description")]
    public required string ErrorDescription { get; init; }
}
