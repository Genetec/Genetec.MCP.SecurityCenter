using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Utilities;

public class NetworkManagementToolTests
{
    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_MissingId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            NetworkManagementTool.Execute(TestHelpers.FakeManager(),
                new NetworkManagementRequest(NetworkAction.Get)));
        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetEndpoints_MissingId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            NetworkManagementTool.Execute(TestHelpers.FakeManager(),
                new NetworkManagementRequest(NetworkAction.GetEndpoints)));
        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(NetworkAction.Get)]
    [InlineData(NetworkAction.GetEndpoints)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(NetworkAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var request = new NetworkManagementRequest(action, Id: id);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => NetworkManagementTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal("id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }
}
