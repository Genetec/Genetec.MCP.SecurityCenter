using Genetec.Mcp.SecurityCenter.Authentication;
using System.Net;
using System.Text;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Configuration;
using Genetec.Mcp.SecurityCenter.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Client;

public class WebSdkLoggingTests
{
    private const string ResponseBody = """{"Rsp":{"Status":"Ok","Result":{"password":"response-secret"}}}""";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NamedClient_UsesWebSdkLoggingWithoutFactoryQueryOrExceptionLogs(bool failure)
    {
        var logger = new RecordingLogger(LogLevel.Trace);
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(logger));
        services.AddSecurityCenterHttpClient(new ConfigurationBuilder().Build());
        services.AddHttpClient(SecurityCenterSessionManager.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => failure
                ? new ThrowingHandler()
                : new ResponseHandler(HttpStatusCode.OK, ResponseBody));
        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(SecurityCenterSessionManager.HttpClientName);

        var exception = await Record.ExceptionAsync(() =>
            client.GetAsync("https://localhost/WebSdk/query?password=query-secret"));

        Assert.Equal(failure, exception is not null);
        Assert.Empty(logger.Entries);
    }

    [Theory]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Trace)]
    public async Task Responses_LogMetadataAndOnlyTraceIncludesTheBody(LogLevel minimum)
    {
        var logger = new RecordingLogger(minimum);
        using var client = new HttpClient(new ResponseHandler(HttpStatusCode.OK, ResponseBody));

        var result = await Send(client, logger);

        Assert.Contains("response-secret", result);
        var summary = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Information);
        Assert.Contains("POST", summary.Message);
        Assert.Contains("https://localhost:4590/WebSdk/query", summary.Message);
        Assert.Contains("200", summary.Message);
        Assert.Contains("ms", summary.Message);
        Assert.DoesNotContain("response-secret", summary.Message);
        Assert.Equal(minimum == LogLevel.Trace, logger.Entries.Any(entry => entry.Message.Contains(ResponseBody)));
        Assert.All(logger.Entries, entry =>
        {
            Assert.DoesNotContain("query-secret", entry.Message);
            Assert.DoesNotContain("request-secret", entry.Message);
            Assert.DoesNotContain("Authorization", entry.Message);
            Assert.DoesNotContain(CreateSession().AuthHeader, entry.Message);
        });
    }

    [Theory]
    [InlineData(false, LogLevel.Information)]
    [InlineData(true, LogLevel.Information)]
    [InlineData(false, LogLevel.Debug)]
    [InlineData(true, LogLevel.Debug)]
    [InlineData(false, LogLevel.Trace)]
    [InlineData(true, LogLevel.Trace)]
    public async Task FailureBodies_AreLoggedAtWarningAndPreservedInOriginalExceptions(bool sdkFailure, LogLevel minimum)
    {
        var body = sdkFailure
            ? """{"Rsp":{"Status":"Fail","Result":{"password":"failure-secret"}}}"""
            : "failure-secret";
        var status = sdkFailure ? HttpStatusCode.OK : HttpStatusCode.Unauthorized;
        var logger = new RecordingLogger(minimum);
        using var client = new HttpClient(new ResponseHandler(status, body));

        var exception = await Record.ExceptionAsync(() => Send(client, logger));

        Assert.NotNull(exception);
        if (sdkFailure)
            Assert.IsType<InvalidOperationException>(exception);
        else
            Assert.Equal(status, Assert.IsType<HttpRequestException>(exception).StatusCode);
        Assert.Contains("failure-secret", exception.Message);
        Assert.Equal(sdkFailure
            ? "Security Center error: {\"password\":\"failure-secret\"}"
            : "HTTP 401: failure-secret", exception.Message);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(body, entry.Message);
        Assert.Same(exception, entry.Exception);
    }

    [Theory]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Trace)]
    public async Task EventLogging_OnlyIncludesCollectedEventsAtTrace(LogLevel minimum)
    {
        const string body = "--GENETECBOUNDARY\nContent-Type: text/json\n\n{\"name\":\"first-event-secret\"}\n--GENETECBOUNDARY\nContent-Type: text/json\n\n{\"name\":\"uncollected-event-secret\"}\n--GENETECBOUNDARY\n";
        var logger = new RecordingLogger(minimum);
        using var client = new HttpClient(new ResponseHandler(HttpStatusCode.OK, body));
        var session = CreateSession();

        var result = await new SecurityCenterRestClient(session, client, logger).StreamEventsAsync(5, 1);

        Assert.Contains("first-event-secret", result);
        Assert.DoesNotContain("uncollected-event-secret", result);
        Assert.Equal(minimum == LogLevel.Trace, logger.Entries.Any(entry => entry.Message.Contains("first-event-secret")));
        Assert.All(logger.Entries, entry => Assert.DoesNotContain("uncollected-event-secret", entry.Message));
        Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Information);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransportFailure_LogsFullDiagnosticsAndPropagatesTheOriginalException(bool streaming)
    {
        var logger = new RecordingLogger(LogLevel.Information);
        using var client = new HttpClient(new ThrowingHandler());

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => streaming
            ? new SecurityCenterRestClient(CreateSession(), client, logger).StreamEventsAsync(5, 1)
            : Send(client, logger));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.DoesNotContain("query-secret", entry.Message);
        Assert.Contains("network-secret", exception.Message);
        Assert.NotNull(exception.InnerException);
        Assert.Equal(HttpRequestError.InvalidResponse, exception.HttpRequestError);
        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
        Assert.Same(exception, entry.Exception);
    }

    [Fact]
    public async Task StreamHttpFailures_LogDiagnosticsAndPreserveHttpStatus()
    {
        var logger = new RecordingLogger(LogLevel.Information);
        using var client = new HttpClient(new ResponseHandler(HttpStatusCode.Unauthorized, "stream-error-body"));
        var session = CreateSession();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            new SecurityCenterRestClient(session, client, logger).StreamEventsAsync(5, 1));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Contains("HTTP 401", exception.Message);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.DoesNotContain("stream-error-body", exception.Message);
        Assert.DoesNotContain("stream-error-body", entry.Message);
        Assert.Same(exception, entry.Exception);
    }

    [Fact]
    public async Task StreamHttpFailure_PropagatesStatusWithoutReadingUnusableBody()
    {
        var logger = new RecordingLogger(LogLevel.Information);
        using var client = new HttpClient(new StreamHandler(HttpStatusCode.Unauthorized));
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            new SecurityCenterRestClient(CreateSession(), client, logger).StreamEventsAsync(5, 1)
                .WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Same(exception, Assert.Single(logger.Entries).Exception);
    }

    [Fact]
    public async Task StreamReadFailures_LogTheFullOriginalException()
    {
        var logger = new RecordingLogger(LogLevel.Information);
        using var client = new HttpClient(new StreamHandler());
        var session = CreateSession();

        var exception = await Assert.ThrowsAsync<HttpIOException>(() =>
            new SecurityCenterRestClient(session, client, logger).StreamEventsAsync(5, 1));

        Assert.Contains("chunk-secret", exception.Message);
        Assert.NotNull(exception.InnerException);
        var entry = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Same(exception, entry.Exception);
    }

    [Fact]
    public async Task RequestCancellation_PropagatesWithoutWrapping()
    {
        var logger = new RecordingLogger(LogLevel.Information);
        using var cancellation = new CancellationTokenSource();
        using var client = new HttpClient(new CancellationHandler(cancellation.Token));
        var session = CreateSession();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SecurityCenterRestClient(session, client, logger).SendAsync("query", "GET", ct: cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Empty(logger.Entries);
    }

    private static Task<string> Send(HttpClient client, ILogger logger)
    {
        var session = CreateSession();
        return new SecurityCenterRestClient(session, client, logger).SendAsync("query?password=query-secret", "POST", "request-secret");
    }

    private static SecurityCenterSession CreateSession() => new(new ScConnectionContext
    {
        Host = "localhost",
        Port = 4590,
        User = "user",
        Password = "auth-secret",
    });

    private sealed class ResponseHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8) });
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException(HttpRequestError.InvalidResponse,
                "Invalid response header: network-secret", new IOException("nested-secret"), HttpStatusCode.BadGateway));
    }

    private sealed class CancellationHandler(CancellationToken token) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new OperationCanceledException(token));
    }

    private sealed class StreamHandler(HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StreamContent(new MalformedStream()) });
    }

    private sealed class MalformedStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw Failure();
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(Failure());
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        private static HttpIOException Failure() => new(HttpRequestError.InvalidResponse,
            "Invalid chunk header: chunk-secret", new IOException("nested-secret"));
    }

    private sealed class RecordingLogger(LogLevel minimum) : ILogger, ILoggerProvider
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];
        public bool IsEnabled(LogLevel logLevel) => logLevel >= minimum;
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) Entries.Add((logLevel, formatter(state, exception), exception));
        }
    }
}
