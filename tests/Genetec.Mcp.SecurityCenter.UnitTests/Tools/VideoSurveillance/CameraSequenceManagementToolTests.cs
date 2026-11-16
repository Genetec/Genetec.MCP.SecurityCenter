using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.VideoSurveillance;

public class CameraSequenceManagementToolTests
{
    private static readonly Guid SequenceId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Theory]
    [InlineData(CameraSequenceAction.Get, "entity/11111111-2222-3333-4444-555555555555")]
    [InlineData(CameraSequenceAction.List, "report/EntityConfiguration?q=EntityTypes@Sequence,ReturnFields@Name,PageSize=50,Page=1")]
    [InlineData(CameraSequenceAction.Update, "entity=11111111-2222-3333-4444-555555555555,Name=Updated Sequence")]
    [InlineData(CameraSequenceAction.GetCameras, "entity=11111111-2222-3333-4444-555555555555,Cameras")]
    public async Task Actions_UseExpectedWebSdkRequest(
        CameraSequenceAction action, string expectedRequest)
    {
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(SequenceId, "Sequence");
        var request = action switch
        {
            CameraSequenceAction.Get => (action is CameraSequenceAction.Update ? new CameraSequenceManagementRequest(action, Items: [new CameraSequenceManagementItem(Id: SequenceId)]) : new CameraSequenceManagementRequest(action, Id: SequenceId)),
            CameraSequenceAction.List => new CameraSequenceManagementRequest(action),
            CameraSequenceAction.Update => (action is CameraSequenceAction.Update ? new CameraSequenceManagementRequest(action, Items: [new CameraSequenceManagementItem(Name: "Updated Sequence", Id: SequenceId)]) : new CameraSequenceManagementRequest(action, Id: SequenceId)),
            CameraSequenceAction.GetCameras => (action is CameraSequenceAction.Update ? new CameraSequenceManagementRequest(action, Items: [new CameraSequenceManagementItem(Id: SequenceId)]) : new CameraSequenceManagementRequest(action, Id: SequenceId)),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

        await CameraSequenceManagementTool.Execute(TestHelpers.FakeManager(handler), request);

        var capturedRequest = handler.Requests.Last().Uri;
        var uri = Uri.UnescapeDataString(capturedRequest.ToString());
        Assert.Contains(expectedRequest, uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_WithBody_UsesWebSdkEntityQuery()
    {
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(SequenceId, "Sequence");

        await CameraSequenceManagementTool.Execute(TestHelpers.FakeManager(handler),
            new CameraSequenceManagementRequest(CameraSequenceAction.Update, Items: [new CameraSequenceManagementItem(Id: SequenceId, Body: new SequenceUpdateProperties(Name: "Body Sequence"))]));

        var capturedRequest = handler.Requests.Last().Uri;
        var uri = Uri.UnescapeDataString(capturedRequest.ToString());
        Assert.Contains(
            $"entity={SequenceId},Name=Body Sequence",
            uri,
            StringComparison.Ordinal);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData(CameraSequenceAction.Get)]
    [InlineData(CameraSequenceAction.Update)]
    [InlineData(CameraSequenceAction.GetCameras)]
    public async Task IdActions_MissingId_ThrowsArgumentException(CameraSequenceAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraSequenceManagementTool.Execute(TestHelpers.FakeManager(),
                new CameraSequenceManagementRequest(action)));

        Assert.Contains(action is CameraSequenceAction.Update ? "items" : "id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownAction_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraSequenceManagementTool.Execute(TestHelpers.FakeManager(),
                new CameraSequenceManagementRequest((CameraSequenceAction)999)));

        Assert.Contains("Unknown action", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(CameraSequenceAction.Get)]
    [InlineData(CameraSequenceAction.Update)]
    [InlineData(CameraSequenceAction.GetCameras)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(CameraSequenceAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var request = (action is CameraSequenceAction.Update ? new CameraSequenceManagementRequest(action, Items: [new CameraSequenceManagementItem(Id: id, Body: action == CameraSequenceAction.Update ? new SequenceUpdateProperties(Name: "Must not update") : null)]) : new CameraSequenceManagementRequest(action, Id: id));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CameraSequenceManagementTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal(action is CameraSequenceAction.Update ? "items" : "id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }
}
