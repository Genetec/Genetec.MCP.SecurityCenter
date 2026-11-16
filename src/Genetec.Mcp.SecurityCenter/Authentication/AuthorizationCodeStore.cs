using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace Genetec.Mcp.SecurityCenter.Authentication;

public class AuthorizationCodeStore : IDisposable
{
    private readonly MemoryCache _codes;
    private readonly TimeProvider _timeProvider;
    private readonly object _redemptionLock = new();

    public AuthorizationCodeStore() : this(new MemoryCache(new MemoryCacheOptions()), TimeProvider.System) { }

    internal AuthorizationCodeStore(MemoryCache codes, TimeProvider timeProvider)
    {
        _codes = codes;
        _timeProvider = timeProvider;
    }

    public AuthorizationCode Create(
        string clientId, string oauthClientId, string redirectUri,
        string codeChallenge, string codeChallengeMethod, string scope,
        string scContext)
    {
        var code = new AuthorizationCode
        {
            Code = Base64UrlEncode(RandomNumberGenerator.GetBytes(32)),
            ClientId = clientId,
            OAuthClientId = oauthClientId,
            RedirectUri = redirectUri,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod,
            Scope = scope,
            ScContext = scContext,
            ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(5)
        };
        _codes.Set(code.Code, code, new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = new DateTimeOffset(code.ExpiresAt)
        });
        return code;
    }

    public AuthorizationCode? Redeem(string code)
    {
        lock (_redemptionLock)
        {
            if (!_codes.TryGetValue(code, out AuthorizationCode? authCode))
                return null;
            _codes.Remove(code);
            return authCode!.ExpiresAt > _timeProvider.GetUtcNow().UtcDateTime ? authCode : null;
        }
    }

    public void Dispose() => _codes.Dispose();

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public class AuthorizationCode
{
    public required string Code { get; init; }
    public required string ClientId { get; init; }
    public required string OAuthClientId { get; init; }
    public required string RedirectUri { get; init; }
    public required string CodeChallenge { get; init; }
    public required string CodeChallengeMethod { get; init; }
    public required string Scope { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required string ScContext { get; init; }
}
