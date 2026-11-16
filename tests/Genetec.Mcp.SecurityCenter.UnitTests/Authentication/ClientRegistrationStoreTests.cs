using Genetec.Mcp.SecurityCenter.Authentication;
using Microsoft.Extensions.Caching.Memory;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Authentication;

public class ClientRegistrationStoreTests
{
    [Fact]
    public void Register_ReturnsClientWithGeneratedCredentials()
    {
        var store = new ClientRegistrationStore();

        var client = store.Register(
            redirectUris: ["https://example.com/callback"],
            grantTypes: ["authorization_code"],
            tokenEndpointAuthMethod: "client_secret_post",
            clientName: "Test Client");

        Assert.StartsWith("dyn-", client.ClientId);
        Assert.Equal(26, client.ClientId.Length);
        Assert.Equal(43, client.ClientSecret.Length);
        Assert.Equal(["https://example.com/callback"], client.RedirectUris);
        Assert.Equal(["authorization_code"], client.GrantTypes);
        Assert.Equal("client_secret_post", client.TokenEndpointAuthMethod);
        Assert.Equal("Test Client", client.ClientName);
        Assert.True(client.ClientIdIssuedAt > 0);
        Assert.Equal(client.ClientIdIssuedAt + 300, client.ClientSecretExpiresAt);
    }

    [Fact]
    public void Register_GeneratesUniqueIds()
    {
        var store = new ClientRegistrationStore();

        var client1 = store.Register(["https://a.com/cb"], ["authorization_code"], "client_secret_post", null);
        var client2 = store.Register(["https://b.com/cb"], ["authorization_code"], "client_secret_post", null);

        Assert.NotEqual(client1.ClientId, client2.ClientId);
        Assert.NotEqual(client1.ClientSecret, client2.ClientSecret);
    }

    [Fact]
    public void TryGet_ReturnsRegisteredClient()
    {
        var store = new ClientRegistrationStore();
        var registered = store.Register(["https://example.com/cb"], ["authorization_code"], "client_secret_post", null);

        var found = store.TryGet(registered.ClientId, out var client);

        Assert.True(found);
        Assert.NotNull(client);
        Assert.Equal(registered.ClientId, client.ClientId);
        Assert.Equal(registered.ClientSecret, client.ClientSecret);
    }

    [Fact]
    public void TryGet_UnknownClientId_ReturnsFalse()
    {
        var store = new ClientRegistrationStore();

        var found = store.TryGet("dyn-nonexistent", out var client);

        Assert.False(found);
        Assert.Null(client);
    }

    [Fact]
    public void Register_NullClientName_StoredAsNull()
    {
        var store = new ClientRegistrationStore();

        var client = store.Register(["https://example.com/cb"], ["authorization_code"], "client_secret_post", null);

        Assert.Null(client.ClientName);
    }

    [Fact]
    public void UnusedRegistrations_AreRemovedByExpirationCleanup()
    {
        var clock = new TestHelpers.TestClock();
        var cache = new MemoryCache(new MemoryCacheOptions { Clock = clock });
        using var store = new ClientRegistrationStore(cache, clock);
        for (var i = 0; i < 100; i++)
            store.Register(["https://example.com/cb"], ["authorization_code"], "client_secret_post", null);
        Assert.Equal(100, cache.Count);
        clock.Advance(TimeSpan.FromMinutes(5));

        cache.Compact(0);

        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Registration_ExpiresAfterFiveMinutes()
    {
        var clock = new TestHelpers.TestClock();
        using var store = new ClientRegistrationStore(new MemoryCache(new MemoryCacheOptions { Clock = clock }), clock);
        var registered = store.Register(["http://localhost/cb"], ["authorization_code"], "none", null);

        clock.Advance(TimeSpan.FromMinutes(6));

        Assert.False(store.TryGet(registered.ClientId, out var expired));
        Assert.Null(expired);
    }

    [Fact]
    public void NewStore_DoesNotRestoreDynamicRegistration()
    {
        using var original = new ClientRegistrationStore();
        var registered = original.Register(["http://localhost/cb"], ["authorization_code"], "none", null);
        using var restarted = new ClientRegistrationStore();

        Assert.False(restarted.TryGet(registered.ClientId, out _));
    }
}
