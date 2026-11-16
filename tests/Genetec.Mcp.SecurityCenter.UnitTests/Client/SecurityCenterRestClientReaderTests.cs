using Genetec.Mcp.SecurityCenter.Authentication;
using System.Net;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Configuration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Client;

public class SecurityCenterRestClientReaderTests
{
    private static readonly Guid AccessPointId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ReaderDeviceId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task ResolveReaderDeviceId_AccessPoint_ReturnsAssociatedDevice()
    {
        var handler = new JsonHandler($"{{\"Guid\":\"{AccessPointId}\",\"EntityType\":\"AccessPoint\",\"AccessPointType\":\"CardReader\",\"Device\":\"{ReaderDeviceId}\"}}", ReaderJson);
        var client = CreateClient(handler);

        var result = await client.ResolveReaderDeviceId(AccessPointId);

        Assert.Equal(ReaderDeviceId, result);
        Assert.Equal(2, handler.Requests.Count);
        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.EndsWith($"/entity/{AccessPointId}", request.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveReaderDeviceId_Device_ReturnsOriginalId()
    {
        var handler = new JsonHandler(ReaderJson);
        var client = CreateClient(handler);

        var result = await client.ResolveReaderDeviceId(ReaderDeviceId);

        Assert.Equal(ReaderDeviceId, result);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ResolveReaderDeviceId_AccessPointWithoutDevice_ThrowsHelpfulError()
    {
        var handler = new JsonHandler($"{{\"Guid\":\"{AccessPointId}\",\"EntityType\":\"AccessPoint\",\"AccessPointType\":\"CardReader\"}}");
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ResolveReaderDeviceId(AccessPointId));

        Assert.Contains("no associated reader device", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveReaderDeviceId_AccessPointWithInvalidDevice_RejectsMalformedMetadata()
    {
        var handler = new JsonHandler($"{{\"Guid\":\"{AccessPointId}\",\"EntityType\":\"AccessPoint\",\"AccessPointType\":\"CardReader\",\"Device\":\"not-a-guid\"}}");
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ResolveReaderDeviceId(AccessPointId));

        Assert.Contains("invalid entity identity or type metadata", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<System.Text.Json.JsonException>(ex.InnerException);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"00000000-0000-0000-0000-000000000000\"")]
    public async Task MissingReaderDevice_ThrowsHelpfulError(string device)
    {
        var handler = new JsonHandler($"{{\"Guid\":\"{AccessPointId}\",\"EntityType\":\"AccessPoint\",\"AccessPointType\":\"CardReader\",\"Device\":{device}}}");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateClient(handler).ResolveReaderDeviceId(AccessPointId));
        Assert.Contains("no associated reader device", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("Device", "123")]
    [InlineData("AccessPointType", "\"UnknownType\"")]
    public async Task MalformedReaderAccessPointMetadata_RejectsBeforeFollowingDevice(string property, string value)
    {
        var handler = new JsonHandler($"{{\"Guid\":\"{AccessPointId}\",\"EntityType\":\"AccessPoint\",\"{property}\":{value}}}");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateClient(handler).ResolveReaderDeviceId(AccessPointId));
        Assert.IsType<System.Text.Json.JsonException>(error.InnerException);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"UnknownType\"")]
    [InlineData("\"\"")]
    public async Task InvalidDeviceType_Rejects(string deviceType)
    {
        var handler = new JsonHandler($"{{\"Guid\":\"{ReaderDeviceId}\",\"EntityType\":\"Device\",\"DeviceType\":{deviceType}}}");
        var error = await Assert.ThrowsAnyAsync<Exception>(() => CreateClient(handler).ResolveReaderDeviceId(ReaderDeviceId));
        if (deviceType == "null")
            Assert.Equal("id", Assert.IsType<ArgumentException>(error).ParamName);
        else
            Assert.IsType<System.Text.Json.JsonException>(Assert.IsType<InvalidOperationException>(error).InnerException);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task EmptyId_RejectsBeforeHttp()
    {
        var handler = new JsonHandler(ReaderJson);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CreateClient(handler).ResolveReaderDeviceId(Guid.Empty));
        Assert.Equal("id", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    private static string ReaderJson => $"{{\"Guid\":\"{ReaderDeviceId}\",\"EntityType\":\"Device\",\"DeviceType\":\"Reader\"}}";

    [Theory]
    [InlineData("Device", "Input")]
    [InlineData("Device", "Output")]
    [InlineData("UserGroup", "Reader")]
    public async Task WrongReaderType_Rejects(string entityType, string deviceType)
    {
        var handler = new JsonHandler($"{{\"Guid\":\"{ReaderDeviceId}\",\"EntityType\":\"{entityType}\",\"DeviceType\":\"{deviceType}\"}}");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CreateClient(handler).ResolveReaderDeviceId(ReaderDeviceId));
        Assert.Equal("id", error.ParamName);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task NonReaderAccessPoint_RejectsBeforeFollowingDevice()
    {
        var handler = new JsonHandler($"{{\"Guid\":\"{AccessPointId}\",\"EntityType\":\"AccessPoint\",\"AccessPointType\":\"Rex\",\"Device\":\"{ReaderDeviceId}\"}}");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CreateClient(handler).ResolveReaderDeviceId(AccessPointId));
        Assert.Equal("id", error.ParamName);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("Device", "Input")]
    [InlineData("UserGroup", "Reader")]
    public async Task LinkedNonReader_Rejects(string entityType, string deviceType)
    {
        var point = $"{{\"Guid\":\"{AccessPointId}\",\"EntityType\":\"AccessPoint\",\"AccessPointType\":\"CardReader\",\"Device\":\"{ReaderDeviceId}\"}}";
        var device = $"{{\"Guid\":\"{ReaderDeviceId}\",\"EntityType\":\"{entityType}\",\"DeviceType\":\"{deviceType}\"}}";
        var handler = new JsonHandler(point, device);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CreateClient(handler).ResolveReaderDeviceId(AccessPointId));
        Assert.Equal("id", error.ParamName);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task MismatchedReaderIdentity_Rejects()
    {
        var handler = new JsonHandler(ReaderJson);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateClient(handler).ResolveReaderDeviceId(AccessPointId));
    }

    private static SecurityCenterRestClient CreateClient(JsonHandler handler)
    {
        var session = new SecurityCenterSession(new ScConnectionContext
        {
            Host = "localhost",
            Port = 4590,
            User = "admin",
            Password = "password",
        });

        return new SecurityCenterRestClient(session, new HttpClient(handler));
    }

    private sealed class JsonHandler(string body, string? linkedBody = null) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(linkedBody is not null && Requests.Count > 1 ? linkedBody : body)
            });
        }
    }
}
