using Genetec.Mcp.SecurityCenter.Authentication;
using Microsoft.Extensions.Caching.Memory;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Authentication;

public class AuthorizationCodeStoreTests
{
    [Fact]
    public void Create_StoresScope()
    {
        using var store = new AuthorizationCodeStore();

        var code = store.Create("user1", "client1", "https://cb", "", "", "video_surveillance access_control", "encrypted-context");

        Assert.Equal("video_surveillance access_control", code.Scope);
    }

    [Fact]
    public void Create_EmptyScope_StoredAsEmpty()
    {
        using var store = new AuthorizationCodeStore();

        var code = store.Create("user1", "client1", "https://cb", "", "", "", "encrypted-context");

        Assert.Empty(code.Scope);
    }

    [Fact]
    public void Redeem_ReturnsCodeWithScope()
    {
        using var store = new AuthorizationCodeStore();
        var created = store.Create("user1", "client1", "https://cb", "", "", "lpr", "encrypted-context");

        var redeemed = store.Redeem(created.Code);

        Assert.NotNull(redeemed);
        Assert.Equal("lpr", redeemed!.Scope);
    }

    [Fact]
    public void Redeem_UnknownOrAlreadyRedeemedCode_ReturnsNull()
    {
        using var store = new AuthorizationCodeStore();
        var code = store.Create("u", "c", "https://cb", "", "", "", "context");
        Assert.Same(code, store.Redeem(code.Code));
        Assert.Null(store.Redeem(code.Code));
        Assert.Null(store.Redeem("unknown"));
    }

    [Fact]
    public void Redeem_ConcurrentAttempts_OnlyOneSucceeds()
    {
        using var store = new AuthorizationCodeStore();
        var code = store.Create("u", "c", "https://cb", "", "", "", "context");
        var results = new AuthorizationCode?[32];
        Parallel.For(0, results.Length, i => results[i] = store.Redeem(code.Code));
        Assert.Single(results.Where(result => result is not null));
    }

    [Fact]
    public void UnredeemedCodes_AreRemovedByExpirationCleanup()
    {
        var clock = new TestHelpers.TestClock();
        var cache = new MemoryCache(new MemoryCacheOptions { Clock = clock });
        using var store = new AuthorizationCodeStore(cache, clock);
        store.Create("u", "c", "https://cb", "", "", "", "context");
        clock.Advance(TimeSpan.FromMinutes(5));

        cache.Compact(0);

        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void CodeLookup_DoesNotExtendFiveMinuteLifetime()
    {
        var clock = new TestHelpers.TestClock();
        var cache = new MemoryCache(new MemoryCacheOptions { Clock = clock });
        using var store = new AuthorizationCodeStore(cache, clock);
        var code = store.Create("u", "c", "https://cb", "", "", "", "context");
        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.True(cache.TryGetValue(code.Code, out _));
        clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Null(store.Redeem(code.Code));
    }

}
