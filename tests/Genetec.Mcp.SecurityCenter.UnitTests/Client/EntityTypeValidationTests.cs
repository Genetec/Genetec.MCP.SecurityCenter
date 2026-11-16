using System.Net;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Client;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Client;

public class EntityTypeValidationTests
{
    private static readonly Guid EntityId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PointId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Theory]
    [InlineData("Door", "\"not-a-guid\"")]
    [InlineData("DoorSides", "123")]
    public async Task RequireEntityType_IgnoresDoorSpecificFields(string property, string value)
    {
        var handler = new CallbackHandler(_ =>
            $"{{\"Guid\":\"{EntityId}\",\"EntityType\":\"Camera\",\"{property}\":{value}}}");

        await CreateClient(handler).RequireEntityType(EntityId, "id", "Camera");

        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Guid\":123,\"EntityType\":\"Door\"}")]
    [InlineData("{\"Guid\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"EntityType\":null}")]
    [InlineData("{\"Guid\":\"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb\",\"EntityType\":\"Door\"}")]
    [InlineData("[]")]
    public async Task RequireEntityType_InvalidMetadata_FailsClosed(string response)
    {
        var handler = new CallbackHandler(_ => response);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateClient(handler).RequireEntityType(EntityId, "id", "Door"));
        Assert.Single(handler.Requests);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EntityRead_WrongType_RejectsBeforeReturningData(bool trimmed)
    {
        var handler = new CallbackHandler(_ => JsonSerializer.Serialize(new { Guid = EntityId, EntityType = "User", Password = "hidden" }));
        var client = CreateClient(handler);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => trimmed
            ? client.GetEntityTrimmed(EntityId, "Credential") : client.GetEntity(EntityId, "Credential"));
        Assert.Equal("id", error.ParamName);
        Assert.DoesNotContain("hidden", error.Message);
    }

    [Fact]
    public async Task RequireEntityType_EmptyGuid_RejectsBeforeHttp()
    {
        var handler = new CallbackHandler(_ => throw new InvalidOperationException("No request expected."));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CreateClient(handler).RequireEntityType(Guid.Empty, "items", "Camera"));
        Assert.Equal("items", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("User", true, true)]
    [InlineData("AccessPoint", false, true)]
    [InlineData("AccessPoint", true, false)]
    public async Task DoorComponent_WrongTypeOwnerOrSide_Rejects(string type, bool owned, bool listedSide)
    {
        var handler = new CallbackHandler(request => request.RequestUri!.AbsolutePath.EndsWith(PointId.ToString())
            ? JsonSerializer.Serialize(new { Guid = PointId, EntityType = type, Door = owned ? EntityId : PointId })
            : JsonSerializer.Serialize(new { Guid = EntityId, EntityType = "Door", DoorSides = listedSide ? new[] { PointId } : [] }));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CreateClient(handler).GetDoorAccessPoint(EntityId, PointId, "door_side_id", true));
        Assert.Equal("door_side_id", error.ParamName);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task BatchTypeGuard_ValidatesEachItemsOwnIdAndPreservesOrder(bool delete, bool invalidFirst)
    {
        var handler = new CallbackHandler(request =>
        {
            if (request.Method != HttpMethod.Get) return "{}";
            var invalid = Uri.UnescapeDataString(request.RequestUri!.Query).Contains(PointId.ToString());
            return JsonSerializer.Serialize(new
            {
                Guid = invalid ? PointId : EntityId,
                EntityType = invalid ? "UserGroup" : "Camera"
            });
        });
        var ids = invalidFirst ? new[] { PointId, EntityId } : new[] { EntityId, PointId };
        var client = CreateClient(handler);
        var json = delete
            ? await client.DeleteEntitiesBatch(ids, "Camera")
            : await client.UpdateEntitiesBatch(ids.Select(id => (id, "{}")).ToList(), "Camera");

        using var document = JsonDocument.Parse(json);
        var batch = document.RootElement;
        Assert.Equal(1, batch.GetProperty("success_count").GetInt32());
        Assert.Equal(1, batch.GetProperty("failure_count").GetInt32());
        var results = batch.GetProperty("results");
        Assert.Equal(2, results.GetArrayLength());
        for (var index = 0; index < ids.Length; index++)
        {
            Assert.Equal(index, results[index].GetProperty("index").GetInt32());
            Assert.Equal(ids[index], results[index].GetProperty("id").GetGuid());
            Assert.Equal(ids[index] == EntityId, results[index].GetProperty("success").GetBoolean());
        }
        var mutation = Assert.Single(handler.Requests, request => request.Method != HttpMethod.Get);
        var mutationUri = Uri.UnescapeDataString(mutation.RequestUri!.ToString());
        Assert.Contains(EntityId.ToString(), mutationUri);
        Assert.DoesNotContain(PointId.ToString(), mutationUri);
    }

    private static SecurityCenterRestClient CreateClient(HttpMessageHandler handler)
        => new(new SecurityCenterSession(new ScConnectionContext
        { Host = "localhost", Port = 4590, User = "test", Password = "test" }), new HttpClient(handler));

    private sealed class CallbackHandler(Func<HttpRequestMessage, string> response) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response(request)) });
        }
    }
}
