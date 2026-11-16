using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using System.Net;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class IntrusionUnitManagementToolTests
{
    private static readonly Guid UnitId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (Uri.UnescapeDataString(request.RequestUri!.Query).Contains("Guid,EntityType", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"Guid\":\"{UnitId}\",\"EntityType\":\"IntrusionUnit\"}}")
                });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"IntrusionAreas\":[\"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\"]}")
            });
        }
    }

    [Fact]
    public async Task GetArea_UsesIntrusionAreasFieldAndPreservesAreaIds()
    {
        var handler = new CapturingHandler();

        var result = await IntrusionUnitManagementTool.Execute(
            TestHelpers.FakeManager(handler),
            new IntrusionUnitManagementRequest(IntrusionUnitAction.GetArea, Id: UnitId));

        Assert.Equal("{\"IntrusionAreas\":[\"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\"]}", result);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal($"https://localhost:4590/WebSdk/entity?q=entity={UnitId},IntrusionAreas",
            Uri.UnescapeDataString(handler.LastRequest.RequestUri!.ToString()));
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Move_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            IntrusionUnitManagementTool.Execute(TestHelpers.FakeManager(),
                new IntrusionUnitManagementRequest(IntrusionUnitAction.Move, Id: null, TargetUnitId: Guid.NewGuid())));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Move_NullTargetUnitId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            IntrusionUnitManagementTool.Execute(TestHelpers.FakeManager(),
                new IntrusionUnitManagementRequest(IntrusionUnitAction.Move, Id: Guid.NewGuid(), TargetUnitId: null)));

        Assert.Contains("target_unit_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_NullBody_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            IntrusionUnitManagementTool.Execute(TestHelpers.FakeManager(),
                new IntrusionUnitManagementRequest(IntrusionUnitAction.Update, Items: [new IntrusionUnitManagementItem(Id: Guid.NewGuid(), Body: null)])));

        Assert.Contains("body", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(IntrusionUnitAction.Get)]
    [InlineData(IntrusionUnitAction.Update)]
    [InlineData(IntrusionUnitAction.Delete)]
    [InlineData(IntrusionUnitAction.GetArea)]
    [InlineData(IntrusionUnitAction.Move)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(IntrusionUnitAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var request = (action is IntrusionUnitAction.Update or IntrusionUnitAction.Delete ? new IntrusionUnitManagementRequest(action, Items: [new IntrusionUnitManagementItem(Id: id, Body: action == IntrusionUnitAction.Update ? new IntrusionUnitUpdateProperties(Name: "Must not update") : null)]) : new IntrusionUnitManagementRequest(action, Id: id, TargetUnitId: Guid.NewGuid()));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => IntrusionUnitManagementTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal(action is IntrusionUnitAction.Update or IntrusionUnitAction.Delete ? "items" : "id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Fact]
    public async Task Move_WrongDestinationType_RejectsBeforeAction()
    {
        var id = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "IntrusionUnit");
        handler.AddEntity(targetId, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => IntrusionUnitManagementTool.Execute(TestHelpers.FakeManager(handler),
            new IntrusionUnitManagementRequest(IntrusionUnitAction.Move, Id: id, TargetUnitId: targetId)));
        Assert.Equal("target_unit_id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Theory]
    [InlineData(IntrusionUnitAction.Get)]
    [InlineData(IntrusionUnitAction.Update)]
    [InlineData(IntrusionUnitAction.GetArea)]
    public async Task TargetActions_MissingId_RejectsRequiredTarget(IntrusionUnitAction action)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => IntrusionUnitManagementTool.Execute(TestHelpers.FakeManager(),
            new IntrusionUnitManagementRequest(action)));
        Assert.Equal(action is IntrusionUnitAction.Update or IntrusionUnitAction.Delete ? "items" : "id", error.ParamName);
    }
}
