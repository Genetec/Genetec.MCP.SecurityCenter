namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

public class AreaDeleteToolTests
{
    [Fact]
    public async Task Delete_SingleWrongEntityType_RejectsBeforeDeleting()
    {
        var id = Guid.NewGuid();
        var handler = new Tools.EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");

        var error = await Assert.ThrowsAsync<ArgumentException>(() => AreaDeleteTool.Execute(
            TestHelpers.FakeManager(handler), new AreaDeleteRequest([id])));

        Assert.Equal("items", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task Delete_MixedBatch_OnlyDeletesArea()
    {
        var areaId = Guid.NewGuid();
        var userGroupId = Guid.NewGuid();
        var handler = new Tools.EntityMetadataTestHandler();
        handler.AddEntity(areaId, "Area");
        handler.AddEntity(userGroupId, "UserGroup");

        var result = await AreaDeleteTool.Execute(TestHelpers.FakeManager(handler),
            new AreaDeleteRequest([areaId, userGroupId]));
        using var document = System.Text.Json.JsonDocument.Parse(Assert.IsType<string>(result));
        var root = document.RootElement;

        Assert.Equal(2, root.GetProperty("requested_count").GetInt32());
        Assert.Equal(1, root.GetProperty("success_count").GetInt32());
        Assert.Equal(1, root.GetProperty("failure_count").GetInt32());
        Assert.Equal(areaId.ToString(), handler.Requests.Single(request => request.Method == HttpMethod.Delete).Uri.Segments[^1]);
        Assert.DoesNotContain(handler.Requests, request => request.Method == HttpMethod.Delete && request.Uri.ToString().EndsWith(userGroupId.ToString(), StringComparison.Ordinal));
    }
}
