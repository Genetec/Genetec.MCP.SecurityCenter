using System.Net;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Configuration;
using Genetec.Mcp.SecurityCenter.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Authentication;

public class TokenThrottleTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TokenRequests_ThrottleStartupSwitch_PreservesGrantValidation(bool enabled)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimiting:Enabled"] = enabled.ToString(),
            ["RateLimiting:TokenWindowAttemptLimit"] = "1",
            ["RateLimiting:TokenWindowSeconds"] = "60"
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.Configure<AuthenticationOptions>(_ => { });
        builder.Services.AddSingleton<ClientRegistrationStore>();
        builder.Services.AddSingleton<AuthorizationCodeStore>();
        if (enabled)
        {
            builder.Services.AddThrottling(builder.Configuration);
            builder.Services.AddAuthorizationThrottling();
        }
        else
            builder.Services.AddRateLimitingOptions(builder.Configuration);
        await using var app = builder.Build();
        app.UseRouting();
        if (enabled)
            app.UseRateLimiter();
        app.MapTokenEndpoint();
        app.MapAuthorizeEndpoint();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };

        using var invalidForm = new FormUrlEncodedContent(new Dictionary<string, string>());
        using var first = await client.PostAsync("/token", invalidForm);
        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);

        foreach (var grant in new[] { "client_credentials", "authorization_code", "refresh_token" })
        {
            using var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = grant });
            using var response = await client.PostAsync("/token", form);
            Assert.Equal(enabled ? HttpStatusCode.TooManyRequests : HttpStatusCode.BadRequest, response.StatusCode);
            if (enabled)
                Assert.True(response.Headers.RetryAfter?.Delta > TimeSpan.Zero);
            else
                Assert.Null(response.Headers.RetryAfter);
        }

        using var authorizeForm = new FormUrlEncodedContent(new Dictionary<string, string>());
        using var authorize = await client.PostAsync("/authorize", authorizeForm);
        Assert.Equal(HttpStatusCode.BadRequest, authorize.StatusCode);
        var tokenEndpoint = ((Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .Single(endpoint => endpoint is Microsoft.AspNetCore.Routing.RouteEndpoint route && route.RoutePattern.RawText == "/token");
        var metadata = Assert.IsType<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>(
            tokenEndpoint.Metadata.GetMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>());
        Assert.Equal(TokenEndpoint.RateLimitPolicy, metadata.PolicyName);
        await app.StopAsync();
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(30, 0)]
    [InlineData(-1, 60)]
    [InlineData(30, -1)]
    public void InvalidTokenLimits_FailValidation(int attempts, int seconds)
    {
        var result = new RateLimitingOptionsValidator().Validate(null, new RateLimitingOptions
        {
            TokenWindowAttemptLimit = attempts,
            TokenWindowSeconds = seconds
        });
        Assert.True(result.Failed);
    }
}
