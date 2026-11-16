using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace Genetec.Mcp.SecurityCenter.Authentication;

public class ClientRegistrationStore : IDisposable
{
    private readonly MemoryCache _clients;
    private readonly TimeProvider _timeProvider;

    public ClientRegistrationStore() : this(new MemoryCache(new MemoryCacheOptions()), TimeProvider.System) { }

    internal ClientRegistrationStore(MemoryCache clients, TimeProvider timeProvider)
    {
        _clients = clients;
        _timeProvider = timeProvider;
    }

    public RegisteredClient Register(
        List<string> redirectUris,
        List<string> grantTypes,
        string tokenEndpointAuthMethod,
        string? clientName)
    {
        var issuedAt = _timeProvider.GetUtcNow();
        var expiresAt = issuedAt.AddMinutes(5);
        var client = new RegisteredClient
        {
            ClientId = "dyn-" + Base64UrlEncode(RandomNumberGenerator.GetBytes(16)),
            ClientSecret = Base64UrlEncode(RandomNumberGenerator.GetBytes(32)),
            RedirectUris = [.. redirectUris],
            GrantTypes = [.. grantTypes],
            TokenEndpointAuthMethod = tokenEndpointAuthMethod,
            ClientName = clientName,
            ClientIdIssuedAt = issuedAt.ToUnixTimeSeconds(),
            ClientSecretExpiresAt = expiresAt.ToUnixTimeSeconds()
        };

        _clients.Set(client.ClientId, client, new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = expiresAt
        });
        return client;
    }

    public bool TryGet(string clientId, out RegisteredClient? client)
    {
        return _clients.TryGetValue(clientId, out client);
    }

    public void Dispose() => _clients.Dispose();

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public record RegisteredClient
{
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required List<string> RedirectUris { get; init; }
    public required List<string> GrantTypes { get; init; }
    public required string TokenEndpointAuthMethod { get; init; }
    public string? ClientName { get; init; }
    public required long ClientIdIssuedAt { get; init; }
    public required long ClientSecretExpiresAt { get; init; }
}
