using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Configuration;
using Genetec.Mcp.SecurityCenter.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Client;

public class ThrottleStartupTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StartupRegistration_BindsProvidedConfiguration_AndOnlyRegistersEnabledLimiters(bool enabled)
    {
        var supplied = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimiting:Enabled"] = enabled.ToString(),
            ["RateLimiting:McpWindowCallLimit"] = "7"
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        if (enabled)
            services.AddThrottling(supplied);
        else
            services.AddRateLimitingOptions(supplied);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        Assert.Equal(enabled, options.Enabled);
        Assert.Equal(7, options.McpWindowCallLimit);
        Assert.Equal(enabled, provider.GetService<ThrottleService>() is not null);
        Assert.Equal(enabled, provider.GetService<SdkConcurrencyLimiter>() is not null);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OptionsValidation_RemainsEnabled_WhenThrottlingIsDisabled(bool enabled)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimiting:Enabled"] = enabled.ToString(),
            ["RateLimiting:McpWindowCallLimit"] = "0"
        }).Build();
        var services = new ServiceCollection();
        services.AddRateLimitingOptions(configuration);
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<RateLimitingOptions>>().Value);
    }

    [Fact]
    public async Task ConcurrencyPermit_IsReleasedToAWaitingRequest()
    {
        using var throttle = new ThrottleService(Options.Create(new RateLimitingOptions
        {
            McpMaxConcurrentPerUser = 1,
            McpWindowCallLimit = 10
        }));
        using var first = await throttle.TryAcquireAsync("user", CancellationToken.None);
        Assert.True(first.IsAcquired);
        var waiting = throttle.TryAcquireAsync("user", CancellationToken.None);
        Assert.False(waiting.IsCompleted);
        first.Dispose();
        using var second = await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(second.IsAcquired);
    }
}
