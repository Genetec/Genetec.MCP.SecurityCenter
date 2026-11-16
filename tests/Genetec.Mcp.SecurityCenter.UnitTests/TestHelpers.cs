using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;

namespace Genetec.Mcp.SecurityCenter.UnitTests;

internal static class TestHelpers
{
    internal static async Task AssertMixedDeleteTypesAsync(
        string expectedType, Func<SecurityCenterSessionManager, List<Guid>, Task<object>> execute,
        bool invalidFirst, string wrongType = "UserGroup")
    {
        var validId = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var ids = invalidFirst ? new List<Guid> { wrongId, validId } : [validId, wrongId];
        var handler = new Tools.EntityMetadataTestHandler();
        handler.AddEntity(validId, expectedType);
        handler.AddEntity(wrongId, wrongType);

        var result = await execute(FakeManager(handler), ids);

        using var document = System.Text.Json.JsonDocument.Parse(Assert.IsType<string>(result));
        var batch = document.RootElement;
        Assert.Equal(2, batch.GetProperty("requested_count").GetInt32());
        Assert.Equal(1, batch.GetProperty("success_count").GetInt32());
        Assert.Equal(1, batch.GetProperty("failure_count").GetInt32());
        var results = batch.GetProperty("results");
        for (var index = 0; index < ids.Count; index++)
        {
            Assert.Equal(index, results[index].GetProperty("index").GetInt32());
            Assert.Equal(ids[index], results[index].GetProperty("id").GetGuid());
            Assert.Equal(ids[index] == validId, results[index].GetProperty("success").GetBoolean());
        }
        var deletion = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Delete);
        Assert.Equal($"/WebSdk/entity/{validId:D}", deletion.Uri.AbsolutePath);
    }

    internal static async Task AssertWrongEntityTypeAsync(
        Func<SecurityCenterSessionManager, Guid, Task<object>> execute,
        string parameterName = "id", string wrongType = "UserGroup")
    {
        var id = Guid.NewGuid();
        var handler = new Tools.EntityMetadataTestHandler();
        handler.AddEntity(id, wrongType);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => execute(FakeManager(handler), id));
        Assert.Equal(parameterName, error.ParamName);
        Assert.Single(handler.Requests);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    internal static async Task AssertWrongRelatedEntityTypeAsync(
        string primaryType,
        Func<SecurityCenterSessionManager, Guid, Guid, Task<object>> execute,
        string parameterName, string wrongType = "UserGroup")
    {
        var primaryId = Guid.NewGuid();
        var relatedId = Guid.NewGuid();
        var handler = new Tools.EntityMetadataTestHandler();
        handler.AddEntity(primaryId, primaryType);
        handler.AddEntity(relatedId, wrongType);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => execute(FakeManager(handler), primaryId, relatedId));
        Assert.Equal(parameterName, error.ParamName);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.EndsWith(",Guid,EntityType", Uri.UnescapeDataString(request.Uri.Query), StringComparison.Ordinal);
        });
    }

    internal sealed class TestClock : TimeProvider, ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
        public void Advance(TimeSpan duration) => UtcNow += duration;
    }

    internal static void AssertRequiredProperty<T>(string propertyName)
    {
        var property = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        Assert.NotNull(property!.GetCustomAttribute<RequiredAttribute>());
    }

    internal static void AssertToolName<T>(string expectedName)
    {
        var method = typeof(T).GetMethod("Execute", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        var attribute = method!.GetCustomAttribute<ModelContextProtocol.Server.McpServerToolAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(expectedName, attribute!.Name);
    }

    internal static SecurityCenterSessionManager FakeManager(HttpMessageHandler? handler = null)
    {
        var manager = BuildManager(handler);
        manager.Connect("test", new ScConnectionContext
        {
            Host = "localhost",
            Port = 4590,
            User = "test",
            Password = "test",
        });
        return manager;
    }

    internal static SecurityCenterSessionManager BuildManager(HttpMessageHandler? handler = null, ConnectionOptions? settings = null)
    {
        var services = new ServiceCollection();
        var clientBuilder = services.AddHttpClient(SecurityCenterSessionManager.HttpClientName);
        clientBuilder.ConfigurePrimaryHttpMessageHandler(() => handler ?? new UnexpectedRequestHandler());
        var sp = services.BuildServiceProvider();
        return new SecurityCenterSessionManager(
            Options.Create(new SecurityCenterOptions { WebSdkSettings = settings }),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp);
    }

    private sealed class UnexpectedRequestHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Unit test attempted an unmocked Security Center request. Supply a fake HTTP handler or use an integration test.");
    }
}
