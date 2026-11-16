using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using Genetec.Mcp.SecurityCenter.Audit;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Audit;

public class AuditRequestFilterTests
{
    [Fact]
    public async Task ToolCall_Success_IsAuditedByTypedFilter()
    {
        await using var fixture = CreateFixture();
        var arguments = Arguments("id", "abc-123");

        var nextCalled = false;
        var result = await fixture.CallFilter(async (_, _) =>
        {
            nextCalled = true;
            return new CallToolResult { IsError = false };
        })(fixture.CallContext("camera_get", arguments), CancellationToken.None);

        Assert.True(nextCalled);
        Assert.False(result.IsError);
        var audit = Assert.Single(fixture.Audit.Calls);
        Assert.Equal("__anonymous__", audit.ClientId);
        Assert.Equal("camera_get", audit.Name);
        Assert.Equal(arguments, audit.Arguments);
        Assert.Equal("Success", audit.Outcome);
        Assert.True(audit.DurationMs >= 0);
    }

    [Fact]
    public async Task ToolCall_ErrorResult_IsAuditedAsToolError()
    {
        await using var fixture = CreateFixture();

        var result = await fixture.CallFilter((_, _) => ValueTask.FromResult(
            new CallToolResult { IsError = true }))(fixture.CallContext("door_unlock"), CancellationToken.None);

        Assert.True(result.IsError);
        var audit = Assert.Single(fixture.Audit.Calls);
        Assert.Equal("door_unlock", audit.Name);
        Assert.Equal("ToolError", audit.Outcome);
    }

    [Fact]
    public async Task ToolCall_Exception_IsAuditedAndRethrown()
    {
        await using var fixture = CreateFixture();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.CallFilter((_, _) => ValueTask.FromException<CallToolResult>(
                new InvalidOperationException("boom")))(fixture.CallContext("camera_get"), CancellationToken.None).AsTask());

        Assert.Equal("boom", exception.Message);
        var audit = Assert.Single(fixture.Audit.Exceptions);
        Assert.Equal("camera_get", audit.Name);
        Assert.Equal("boom", audit.Message);
        Assert.True(audit.DurationMs >= 0);
    }

    [Fact]
    public async Task ResourceRead_Success_IsAuditedByTypedFilter()
    {
        await using var fixture = CreateFixture();

        var result = await fixture.ResourceFilter((_, _) => ValueTask.FromResult(
            new ReadResourceResult()))(fixture.ResourceContext("sc://entity/abc-123"), CancellationToken.None);

        Assert.NotNull(result);
        var audit = Assert.Single(fixture.Audit.ResourceReads);
        Assert.Equal("__anonymous__", audit.ClientId);
        Assert.Equal("sc://entity/abc-123", audit.Uri);
        Assert.Equal("Success", audit.Outcome);
        Assert.True(audit.DurationMs >= 0);
    }

    [Fact]
    public async Task ResourceRead_Exception_IsAuditedAndRethrown()
    {
        await using var fixture = CreateFixture();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.ResourceFilter((_, _) => ValueTask.FromException<ReadResourceResult>(
                new InvalidOperationException("resource failure")))(
                fixture.ResourceContext("sc://entity/abc-123"), CancellationToken.None).AsTask());

        Assert.Equal("resource failure", exception.Message);
        var audit = Assert.Single(fixture.Audit.ResourceExceptions);
        Assert.Equal("sc://entity/abc-123", audit.Uri);
        Assert.Equal("resource failure", audit.Message);
        Assert.True(audit.DurationMs >= 0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task WebSdkFailure_AuditsFullErrorDetails(bool resourceRead, bool sdkFailure)
    {
        await using var fixture = CreateFixture();
        var exception = CreateWebSdkFailure(sdkFailure);

        if (resourceRead)
        {
            var thrown = await Assert.ThrowsAnyAsync<Exception>(() =>
                fixture.ResourceFilter((_, _) => ValueTask.FromException<ReadResourceResult>(exception))(
                    fixture.ResourceContext("sc://entity/test"), CancellationToken.None).AsTask());
            Assert.Same(exception, thrown);
            Assert.Equal(exception.Message, Assert.Single(fixture.Audit.ResourceExceptions).Message);
        }
        else
        {
            var thrown = await Assert.ThrowsAnyAsync<Exception>(() =>
                fixture.CallFilter((_, _) => ValueTask.FromException<CallToolResult>(exception))(
                    fixture.CallContext("camera_get"), CancellationToken.None).AsTask());
            Assert.Same(exception, thrown);
            Assert.Equal(exception.Message, Assert.Single(fixture.Audit.Exceptions).Message);
        }

        Assert.Contains("response-secret", exception.Message);
        Assert.Contains("response-secret", exception.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WebSdkFailure_ReturnsFullDetailsAndAuditsOutcome(bool sdkFailure)
    {
        await using var fixture = CreateFixture(errorHandler: true);
        var result = await fixture.CallFilter((_, _) =>
            ValueTask.FromException<CallToolResult>(CreateWebSdkFailure(sdkFailure)))(
                fixture.CallContext("camera_get"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("response-secret", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        Assert.Equal("ToolError", Assert.Single(fixture.Audit.Calls).Outcome);
        Assert.Empty(fixture.Audit.Exceptions);
    }

    private static Exception CreateWebSdkFailure(bool sdkFailure) => sdkFailure
        ? new InvalidOperationException("Security Center error: {\"password\":\"response-secret\"}")
        : new HttpRequestException("HTTP 400: {\"password\":\"response-secret\"}", null, HttpStatusCode.BadRequest);

    [Fact]
    public async Task WebSdkResponseReadFailure_ReturnsOriginalDetailsAndAuditsOutcome()
    {
        await using var fixture = CreateFixture(errorHandler: true);
        var exception = new IOException("malformed-response-secret");

        var result = await fixture.CallFilter((_, _) =>
            ValueTask.FromException<CallToolResult>(exception))(
                fixture.CallContext("camera_get"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("malformed-response-secret", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        Assert.Equal("ToolError", Assert.Single(fixture.Audit.Calls).Outcome);
        Assert.Empty(fixture.Audit.Exceptions);
    }

    [Fact]
    public async Task ResourceRead_SharesToolBudget_AndAuditsRejectionWithoutCallingHandler()
    {
        await using var fixture = CreateFixture(throttle: true);
        await fixture.CallFilter((_, _) => ValueTask.FromResult(new CallToolResult()))(
            fixture.CallContext("camera_get"), CancellationToken.None);
        var handlerCalled = false;

        await Assert.ThrowsAsync<ModelContextProtocol.McpProtocolException>(() =>
            fixture.ResourceFilter((_, _) =>
            {
                handlerCalled = true;
                return ValueTask.FromResult(new ReadResourceResult());
            })(fixture.ResourceContext("sc://entity/abc-123"), CancellationToken.None).AsTask());

        Assert.False(handlerCalled);
        Assert.Empty(fixture.Audit.ResourceReads);
        Assert.Contains("Call budget exceeded", Assert.Single(fixture.Audit.ResourceExceptions).Message);
    }

    [Fact]
    public async Task ResourceRead_AllowedRequest_UsesAndReleasesSharedConcurrencySlot()
    {
        await using var fixture = CreateFixture(throttle: true, callLimit: 2);
        await fixture.ResourceFilter((_, _) => ValueTask.FromResult(new ReadResourceResult()))(
            fixture.ResourceContext("sc://entity/abc-123"), CancellationToken.None);

        using var acquisition = await fixture.Throttle.TryAcquireAsync("__anonymous__", CancellationToken.None);
        Assert.True(acquisition.IsAcquired);
        Assert.Single(fixture.Audit.ResourceReads);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task ThrottleStartupSwitch_AppliesToToolAndResourceFilters(bool enabled, bool resourceRead)
    {
        await using var fixture = CreateFixture(throttle: true, rateLimitingEnabled: enabled);
        Assert.Equal(enabled, fixture.HasThrottle);
        await fixture.CallFilter((_, _) => ValueTask.FromResult(new CallToolResult()))(
            fixture.CallContext("camera_get"), CancellationToken.None);
        var handlerCalled = false;
        CallToolResult? toolResult = null;
        async Task ExecuteSecond()
        {
            if (resourceRead)
                await fixture.ResourceFilter((_, _) =>
                {
                    handlerCalled = true;
                    return ValueTask.FromResult(new ReadResourceResult());
                })(fixture.ResourceContext("sc://entity/test"), CancellationToken.None);
            else
                toolResult = await fixture.CallFilter((_, _) =>
                {
                    handlerCalled = true;
                    return ValueTask.FromResult(new CallToolResult { IsError = false });
                })(fixture.CallContext("camera_get"), CancellationToken.None);
        }

        if (enabled && resourceRead)
            await Assert.ThrowsAsync<ModelContextProtocol.McpProtocolException>(ExecuteSecond);
        else
            await ExecuteSecond();
        Assert.Equal(!enabled, handlerCalled);
        if (!resourceRead)
        {
            Assert.NotNull(toolResult);
            Assert.Equal(enabled, toolResult.IsError);
            if (enabled)
                Assert.Contains("Call budget exceeded", Assert.IsType<TextContentBlock>(Assert.Single(toolResult.Content)).Text);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ThrottleFilters_ReleaseConcurrencyAfterHandlerCompletes(bool resourceRead, bool fails)
    {
        await using var fixture = CreateFixture(throttle: true, callLimit: 3);
        async Task Execute()
        {
            if (resourceRead)
                await fixture.ResourceFilter((_, _) => fails
                    ? ValueTask.FromException<ReadResourceResult>(new InvalidOperationException("test failure"))
                    : ValueTask.FromResult(new ReadResourceResult()))(
                        fixture.ResourceContext("sc://entity/test"), CancellationToken.None);
            else
                await fixture.CallFilter((_, _) => fails
                    ? ValueTask.FromException<CallToolResult>(new InvalidOperationException("test failure"))
                    : ValueTask.FromResult(new CallToolResult()))(
                        fixture.CallContext("camera_get"), CancellationToken.None);
        }
        if (fails)
            await Assert.ThrowsAsync<InvalidOperationException>(Execute);
        else
            await Execute();
        using var acquisition = await fixture.Throttle.TryAcquireAsync("__anonymous__", CancellationToken.None);
        Assert.True(acquisition.IsAcquired);
    }

    private static IDictionary<string, JsonElement> Arguments(params string[] values)
    {
        var json = "{" + string.Join(",", values.Chunk(2).Select(pair =>
            $"{JsonSerializer.Serialize(pair[0])}:{JsonSerializer.Serialize(pair[1])}")) + "}";
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone());
    }

    private static FilterFixture CreateFixture(bool throttle = false, int callLimit = 1, bool errorHandler = false,
        bool rateLimitingEnabled = true)
    {
        var services = new ServiceCollection();
        var audit = new RecordingAuditService();
        services.AddSingleton<IAuditService>(audit);
        if (throttle)
        {
            services.AddSingleton<IOptions<RateLimitingOptions>>(Options.Create(new RateLimitingOptions
            {
                McpWindowCallLimit = callLimit,
                McpMaxConcurrentPerUser = 1,
                Enabled = rateLimitingEnabled
            }));
            if (rateLimitingEnabled)
                services.AddSingleton<ThrottleService>();
        }
        services.AddMcpServer()
            .WithRequestFilters(filters =>
            {
                filters.AddAuditCallFilter();
                filters.AddAuditResourceFilter();
                if (errorHandler)
                    filters.AddErrorHandlerCallFilter();
                if (throttle && rateLimitingEnabled)
                {
                    filters.AddThrottleCallFilter();
                    filters.AddThrottleResourceFilter();
                }
            });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var server = McpServer.Create(
            new TestTransport(), options, NullLoggerFactory.Instance, provider);

        return new FilterFixture(provider, server, options.Filters.Request, audit);
    }

    private sealed class FilterFixture(
        ServiceProvider provider,
        McpServer server,
        McpRequestFilters filters,
        RecordingAuditService audit) : IAsyncDisposable
    {
        public RecordingAuditService Audit { get; } = audit;
        public ThrottleService Throttle => provider.GetRequiredService<ThrottleService>();
        public bool HasThrottle => provider.GetService<ThrottleService>() is not null;

        public McpRequestFilter<CallToolRequestParams, CallToolResult> CallFilter =>
            next => filters.CallToolFilters.Reverse().Aggregate(next, (handler, filter) => filter(handler));

        public McpRequestFilter<ReadResourceRequestParams, ReadResourceResult> ResourceFilter =>
            next => filters.ReadResourceFilters.Reverse().Aggregate(next, (handler, filter) => filter(handler));

        public RequestContext<CallToolRequestParams> CallContext(
            string name, IDictionary<string, JsonElement>? arguments = null) =>
            new(server, new JsonRpcRequest { Id = new RequestId(1), Method = "tools/call" },
                new CallToolRequestParams { Name = name, Arguments = arguments });

        public RequestContext<ReadResourceRequestParams> ResourceContext(string uri) =>
            new(server, new JsonRpcRequest { Id = new RequestId(2), Method = "resources/read" },
                new ReadResourceRequestParams { Uri = uri });

        public async ValueTask DisposeAsync()
        {
            await server.DisposeAsync();
            await provider.DisposeAsync();
        }
    }

    private sealed class RecordingAuditService : IAuditService
    {
        public List<CallAudit> Calls { get; } = [];
        public List<ExceptionAudit> Exceptions { get; } = [];
        public List<ResourceAudit> ResourceReads { get; } = [];
        public List<ResourceExceptionAudit> ResourceExceptions { get; } = [];

        public void LogCall(string clientId, string toolName, IDictionary<string, JsonElement>? arguments,
            string outcome, long durationMs, string? errorMessage = null) =>
            Calls.Add(new(clientId, toolName, arguments, outcome, durationMs));

        public void LogException(string clientId, string toolName, IDictionary<string, JsonElement>? arguments,
            string exceptionMessage, long durationMs) =>
            Exceptions.Add(new(clientId, toolName, exceptionMessage, durationMs));

        public void LogResourceRead(string clientId, string resourceUri, string outcome, long durationMs) =>
            ResourceReads.Add(new(clientId, resourceUri, outcome, durationMs));

        public void LogResourceReadException(string clientId, string resourceUri, string exceptionMessage,
            long durationMs) => ResourceExceptions.Add(new(clientId, resourceUri, exceptionMessage, durationMs));
    }

    private sealed record CallAudit(
        string ClientId,
        string Name,
        IDictionary<string, JsonElement>? Arguments,
        string Outcome,
        long DurationMs);

    private sealed record ExceptionAudit(string ClientId, string Name, string Message, long DurationMs);
    private sealed record ResourceAudit(string ClientId, string Uri, string Outcome, long DurationMs);
    private sealed record ResourceExceptionAudit(string ClientId, string Uri, string Message, long DurationMs);

    private sealed class TestTransport : ITransport
    {
        private readonly Channel<JsonRpcMessage> _messages = Channel.CreateUnbounded<JsonRpcMessage>();

        public string SessionId => "audit-test";
        public ChannelReader<JsonRpcMessage> MessageReader => _messages.Reader;

        public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
