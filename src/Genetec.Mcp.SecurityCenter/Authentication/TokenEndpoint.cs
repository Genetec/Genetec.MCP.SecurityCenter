using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Genetec.Mcp.SecurityCenter.Authentication;

public static class TokenEndpoint
{
    internal const string RateLimitPolicy = "token";
    private const string RefreshTokenType = "refresh";
    private const string OAuthClientIdClaim = "oauth_client_id";
    private const string ClientAuthMethodClaim = "client_auth_method";
    private const string ClientSecretHashClaim = "client_secret_hash";

    internal sealed record RefreshClientAuthentication(string Method, string? SecretHash = null);

    public static WebApplication MapTokenEndpoint(this WebApplication app)
    {
        app.MapPost("/token", async (HttpContext context) =>
        {
            var authOptions = context.RequestServices
                .GetRequiredService<IOptions<AuthenticationOptions>>().Value;

            var form = await context.Request.ReadFormAsync();
            var grantType = form["grant_type"].ToString();

            switch (grantType)
            {
                case "authorization_code":
                    await HandleAuthorizationCode(context, authOptions, form);
                    break;
                case "refresh_token":
                    await HandleRefreshToken(context, authOptions, form);
                    break;
                default:
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new { error = "unsupported_grant_type" });
                    break;
            }
        }).AllowAnonymous().RequireRateLimiting(RateLimitPolicy);

        return app;
    }

    private static async Task HandleAuthorizationCode(
        HttpContext context, AuthenticationOptions authOptions, IFormCollection form)
    {
        var code = form["code"].ToString();
        var clientId = form["client_id"].ToString();
        var redirectUri = form["redirect_uri"].ToString();
        var codeVerifier = form["code_verifier"].ToString();

        var clientAuthentication = await AuthenticateAuthorizationCodeClient(context, form);
        if (clientAuthentication is null)
            return;

        var store = context.RequestServices.GetRequiredService<AuthorizationCodeStore>();
        var authCode = store.Redeem(code);

        if (authCode is null || authCode.OAuthClientId != clientId || authCode.RedirectUri != redirectUri)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "invalid_grant" });
            return;
        }

        if (!ValidatePkce(codeVerifier, authCode.CodeChallenge, authCode.CodeChallengeMethod))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "invalid_grant" });
            return;
        }

        await WriteTokenResponse(context, authOptions, authCode.ClientId, authCode.Scope,
            includeRefreshToken: true, scContext: authCode.ScContext, oauthClientId: authCode.OAuthClientId,
            clientAuthentication: clientAuthentication);
    }

    private static async Task HandleRefreshToken(
        HttpContext context, AuthenticationOptions authOptions, IFormCollection form)
    {
        var refreshToken = form["refresh_token"].ToString();
        if (string.IsNullOrEmpty(refreshToken))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "invalid_request", error_description = "refresh_token is required" });
            return;
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.SigningKey));
        var tokenHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        ClaimsPrincipal principal;
        try
        {
            principal = tokenHandler.ValidateToken(refreshToken, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = authOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = authOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "invalid_grant", error_description = "Refresh token is invalid or expired" });
            return;
        }

        if (principal.FindFirstValue("token_type") != RefreshTokenType)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "invalid_grant", error_description = "Token is not a refresh token" });
            return;
        }

        var clientId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var oauthClientId = principal.FindFirstValue(OAuthClientIdClaim);
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(oauthClientId))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "invalid_grant" });
            return;
        }

        var requestedClientId = form["client_id"].ToString();
        if (string.IsNullOrEmpty(requestedClientId))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "invalid_request", error_description = "client_id is required" });
            return;
        }
        if (!string.Equals(oauthClientId, requestedClientId, StringComparison.Ordinal))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "invalid_grant" });
            return;
        }
        // Refresh credentials are bound into the signed token at sign-in. Registration
        // entries expire after five minutes and must not limit the refresh lifetime.
        var authMethod = principal.FindFirstValue(ClientAuthMethodClaim);
        var secretHash = principal.FindFirstValue(ClientSecretHashClaim);
        if ((authMethod != "none" && authMethod != "client_secret_post") ||
            (authMethod == "client_secret_post" && string.IsNullOrEmpty(secretHash)) ||
            (authMethod == "none" && secretHash is not null))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "invalid_grant" });
            return;
        }
        if (authMethod == "client_secret_post" &&
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(secretHash!),
                Encoding.UTF8.GetBytes(HashClientSecret(form["client_secret"].ToString()))))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "invalid_client" });
            return;
        }

        var scope = principal.FindFirstValue("scope") ?? "";
        var scContext = principal.FindFirstValue("sc_ctx");

        await WriteTokenResponse(context, authOptions, clientId, scope,
            includeRefreshToken: true, scContext: scContext, oauthClientId: oauthClientId,
            clientAuthentication: new RefreshClientAuthentication(authMethod!, secretHash));
    }

    private static async Task<RefreshClientAuthentication?> AuthenticateAuthorizationCodeClient(
        HttpContext context, IFormCollection form)
    {
        var clientId = form["client_id"].ToString();
        if (string.IsNullOrEmpty(clientId))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "invalid_request", error_description = "client_id is required" });
            return null;
        }

        var registrationStore = context.RequestServices.GetRequiredService<ClientRegistrationStore>();
        if (!registrationStore.TryGet(clientId, out var registeredClient))
        {
            // Dynamic IDs must never fall back to unauthenticated public-client behavior.
            if (!clientId.StartsWith("dyn-", StringComparison.Ordinal))
                return new RefreshClientAuthentication("none");
        }
        else
        {
            if (!registeredClient!.GrantTypes.Contains("authorization_code"))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "unauthorized_client" });
                return null;
            }

            if (registeredClient!.TokenEndpointAuthMethod == "none")
                return new RefreshClientAuthentication("none");
            if (registeredClient.TokenEndpointAuthMethod == "client_secret_post")
            {
                var expected = Encoding.UTF8.GetBytes(registeredClient.ClientSecret);
                var actual = Encoding.UTF8.GetBytes(form["client_secret"].ToString());
                if (CryptographicOperations.FixedTimeEquals(expected, actual))
                    return new RefreshClientAuthentication("client_secret_post", HashClientSecret(registeredClient.ClientSecret));
            }
        }

        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new { error = "invalid_client" });
        return null;
    }

    private static string HashClientSecret(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    internal static async Task WriteTokenResponse(
        HttpContext context, AuthenticationOptions authOptions,
        string clientId, string scope,
        bool includeRefreshToken = false,
        string? scContext = null,
        string? oauthClientId = null,
        RefreshClientAuthentication? clientAuthentication = null)
    {
        if (includeRefreshToken && string.IsNullOrEmpty(oauthClientId))
            throw new ArgumentException("An OAuth client ID is required when issuing a refresh token.", nameof(oauthClientId));
        if (includeRefreshToken && clientAuthentication is null)
            throw new ArgumentException("Client authentication is required when issuing a refresh token.", nameof(clientAuthentication));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var expiresIn = authOptions.TokenLifetimeMinutes * 60;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, clientId),
            new(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        if (!string.IsNullOrEmpty(scope))
            claims.Add(new Claim("scope", scope));

        if (!string.IsNullOrEmpty(scContext))
            claims.Add(new Claim("sc_ctx", scContext));

        var token = new JwtSecurityToken(
            issuer: authOptions.Issuer,
            audience: authOptions.Audience,
            claims: claims,
            expires: now.AddMinutes(authOptions.TokenLifetimeMinutes),
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        var response = new Dictionary<string, object?>
        {
            ["access_token"] = accessToken,
            ["token_type"] = "Bearer",
            ["expires_in"] = expiresIn,
            ["scope"] = string.IsNullOrEmpty(scope) ? null : scope
        };

        if (includeRefreshToken)
            response["refresh_token"] = GenerateRefreshToken(authOptions, clientId, oauthClientId!, scope, now,
                clientAuthentication!, scContext);

        await context.Response.WriteAsJsonAsync(response);
    }

    private static string GenerateRefreshToken(
        AuthenticationOptions authOptions, string clientId, string oauthClientId, string scope, DateTime now,
        RefreshClientAuthentication clientAuthentication,
        string? scContext = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, clientId),
            new(OAuthClientIdClaim, oauthClientId),
            new(ClientAuthMethodClaim, clientAuthentication.Method),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("token_type", RefreshTokenType)
        };

        if (clientAuthentication.SecretHash is not null)
            claims.Add(new Claim(ClientSecretHashClaim, clientAuthentication.SecretHash));

        if (!string.IsNullOrEmpty(scope))
            claims.Add(new Claim("scope", scope));

        if (!string.IsNullOrEmpty(scContext))
            claims.Add(new Claim("sc_ctx", scContext));

        var token = new JwtSecurityToken(
            issuer: authOptions.Issuer,
            audience: authOptions.Audience,
            claims: claims,
            expires: now.AddDays(authOptions.RefreshTokenLifetimeDays),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    internal static bool IsValidPkceChallenge(string challenge, string method) =>
        method == "S256" && challenge.Length == 43 &&
        challenge.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    internal static bool ValidatePkce(
        string codeVerifier, string codeChallenge, string codeChallengeMethod)
    {
        if (!IsValidPkceChallenge(codeChallenge, codeChallengeMethod) ||
            codeVerifier.Length is < 43 or > 128 ||
            !codeVerifier.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~'))
            return false;

        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var computed = Base64UrlEncode(hash);
        return computed == codeChallenge;
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
