using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Configuration;
using System.Net;
using System.Text.Json;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Client;

public class SecurityCenterRestClientAlarmTests
{
    private sealed class CapturingHandler(string? attachedEntitiesResponse = null) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(request.Method == HttpMethod.Get && attachedEntitiesResponse is not null
                    ? attachedEntitiesResponse : "\"ok\"")
            });
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AttachedEntityMutation_PreservesOtherIdsAndAvoidsDuplicates(bool remove, bool alreadyAttached)
    {
        var alarmId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var retainedId = Guid.NewGuid();
        var original = alreadyAttached ? new[] { retainedId, entityId } : new[] { retainedId };
        var handler = new CapturingHandler(JsonSerializer.Serialize(new { AttachedEntities = original }));
        var client = CreateClient(handler);

        if (remove)
            await client.RemoveAlarmAttachedEntity(alarmId, entityId);
        else
            await client.AddAlarmAttachedEntity(alarmId, entityId);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Contains($"entity={alarmId},AttachedEntities", Uri.UnescapeDataString(handler.Requests[0].RequestUri!.Query));
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        var expected = remove ? new[] { retainedId } : new[] { retainedId, entityId };
        Assert.Contains($"entity={alarmId},AttachedEntities={{{string.Join(@"\,", expected)}}}",
            Uri.UnescapeDataString(handler.Requests[1].RequestUri!.Query));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AttachedEntityMutation_EmptyCollection_IsSupported(bool remove)
    {
        var alarmId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var handler = new CapturingHandler("{\"AttachedEntities\":[]}");
        var client = CreateClient(handler);
        if (remove)
            await client.RemoveAlarmAttachedEntity(alarmId, entityId);
        else
            await client.AddAlarmAttachedEntity(alarmId, entityId);
        var expected = remove ? "{}" : $"{{{entityId}}}";
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains($"AttachedEntities={expected}", Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.Query));
    }

    [Theory]
    [InlineData(false, "{}")]
    [InlineData(true, "{}")]
    [InlineData(false, "{\"AttachedEntities\":null}")]
    [InlineData(true, "{\"AttachedEntities\":null}")]
    [InlineData(false, "{\"AttachedEntities\":123}")]
    [InlineData(true, "{\"AttachedEntities\":123}")]
    [InlineData(false, "{\"AttachedEntities\":[\"not-a-guid\"]}")]
    [InlineData(true, "{\"AttachedEntities\":[\"not-a-guid\"]}")]
    [InlineData(false, "{\"AttachedEntities\":[123]}")]
    [InlineData(true, "{\"AttachedEntities\":[123]}")]
    [InlineData(false, "{\"Rsp\":{\"Status\":\"Partial\",\"Result\":{\"AttachedEntities\":[]}}}")]
    [InlineData(true, "{\"Rsp\":{\"Status\":\"Partial\",\"Result\":{\"AttachedEntities\":[]}}}")]
    public async Task AttachedEntityMutation_InvalidCollection_RejectsBeforeWriting(bool remove, string response)
    {
        var handler = new CapturingHandler(response);
        var client = CreateClient(handler);
        var error = await Assert.ThrowsAnyAsync<Exception>(() => remove
            ? client.RemoveAlarmAttachedEntity(Guid.NewGuid(), Guid.NewGuid())
            : client.AddAlarmAttachedEntity(Guid.NewGuid(), Guid.NewGuid()));
        Assert.True(error is JsonException or InvalidOperationException, $"Unexpected exception type: {error.GetType().Name}");
        Assert.Equal(HttpMethod.Get, Assert.Single(handler.Requests).Method);
    }

    private static SecurityCenterRestClient CreateClient(CapturingHandler handler)
    {
        var session = new SecurityCenterSession(new ScConnectionContext
        {
            Host = "localhost",
            Port = 4590,
            User = "admin",
            Password = "pass",
        });
        return new SecurityCenterRestClient(session, new HttpClient(handler));
    }

    [Fact]
    public async Task AlarmAction_UsesGetMethodOnAlarmEndpoint()
    {
        var handler = new CapturingHandler();
        var client = CreateClient(handler);

        await client.AlarmAction("ForceAcknowledgeAllAlarms()");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
    }

    [Fact]
    public async Task AlarmAction_BuildsCorrectUrl()
    {
        var handler = new CapturingHandler();
        var client = CreateClient(handler);

        await client.AlarmAction("ForwardAlarm(inst-guid,recv-guid)");

        Assert.Contains(
            "alarm?q=ForwardAlarm(inst-guid,recv-guid)",
            Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.ToString()));
    }

    [Fact]
    public async Task ForwardAlarm_UsesDocumentedQuerySyntax()
    {
        var handler = new CapturingHandler();
        var client = CreateClient(handler);
        var recipientIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

        await client.ForwardAlarm(42, recipientIds);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Contains(
            $"alarm?q=ForwardAlarm(42,{{{string.Join(',', recipientIds)}}})",
            Uri.UnescapeDataString(handler.LastRequest.RequestUri!.ToString()));
    }

    [Fact]
    public async Task AddAlarmRecipient_UsesEntityRecipientsAddSyntax()
    {
        var handler = new CapturingHandler();
        var client = CreateClient(handler);

        await client.AddAlarmRecipient("alarm-guid", "recipient-guid");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Contains(
            "entity?q=entity=alarm-guid,Recipients.Add(recipient-guid,0)",
            Uri.UnescapeDataString(handler.LastRequest.RequestUri!.ToString()));
        Assert.Contains("%3D", handler.LastRequest.RequestUri.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveAlarmRecipient_UsesEntityRecipientsRemoveSyntax()
    {
        var handler = new CapturingHandler();
        var client = CreateClient(handler);

        await client.RemoveAlarmRecipient("alarm-guid", "recipient-guid");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Contains(
            "entity?q=entity=alarm-guid,Recipients.Remove(recipient-guid)",
            Uri.UnescapeDataString(handler.LastRequest.RequestUri!.ToString()));
        Assert.Contains("%3D", handler.LastRequest.RequestUri.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StripEntityNoise_RemovesPrivilegesField()
    {
        var json = """{"Name":"TestAlarm","Guid":"abc","Privileges":[{"Id":1},{"Id":2}],"AlarmColor":"#FF0000"}""";
        var result = SecurityCenterRestClient.StripEntityNoise(json);
        var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.TryGetProperty("Privileges", out _));
        Assert.Equal("TestAlarm", doc.RootElement.GetProperty("Name").GetString());
        Assert.Equal("#FF0000", doc.RootElement.GetProperty("AlarmColor").GetString());
    }

    [Fact]
    public void StripEntityNoise_RemovesChildPrivilegesField()
    {
        var json = """{"Name":"Alarm","ChildPrivileges":[{"X":1}],"Description":"desc"}""";
        var result = SecurityCenterRestClient.StripEntityNoise(json);
        var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.TryGetProperty("ChildPrivileges", out _));
        Assert.Equal("desc", doc.RootElement.GetProperty("Description").GetString());
    }

    [Fact]
    public void StripEntityNoise_StripsPrivilegesNestedInObject()
    {
        var json = """{"Outer":{"Name":"inner","Privileges":[1,2,3]}}""";
        var result = SecurityCenterRestClient.StripEntityNoise(json);
        var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("Outer").TryGetProperty("Privileges", out _));
        Assert.Equal("inner", doc.RootElement.GetProperty("Outer").GetProperty("Name").GetString());
    }

    [Fact]
    public void StripEntityNoise_PreservesNonPrivilegeArrays()
    {
        var json = """{"Name":"Alarm","Recipients":["guid1","guid2"]}""";
        var result = SecurityCenterRestClient.StripEntityNoise(json);
        var doc = JsonDocument.Parse(result);
        Assert.Equal(2, doc.RootElement.GetProperty("Recipients").GetArrayLength());
    }

    [Fact]
    public void StripEntityNoise_InvalidJson_ReturnsOriginal()
    {
        var raw = "not-json";
        var result = SecurityCenterRestClient.StripEntityNoise(raw);
        Assert.Equal(raw, result);
    }
}
