using System.Net;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.VideoSurveillance;

public class VideoUnitOperationsToolTests
{
    [Fact]
    public async Task RelatedEntityTargets_RejectWrongEntityType()
    {
        await TestHelpers.AssertWrongRelatedEntityTypeAsync("VideoUnit", (manager, primaryId, relatedId) => VideoUnitOperationsTool.Execute(manager, new VideoUnitOperationsRequest(VideoUnitOperationsAction.Move, Id: primaryId, TargetArchiverId: relatedId)), "target_archiver_id", "UserGroup");
    }

    [Theory]
    [InlineData(VideoUnitOperationsAction.Move)]
    [InlineData(VideoUnitOperationsAction.Reconnect)]
    [InlineData(VideoUnitOperationsAction.Reboot)]
    public async Task CurrentEntityTargets_RejectWrongEntityType(VideoUnitOperationsAction action)
    {
        await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => VideoUnitOperationsTool.Execute(manager, new VideoUnitOperationsRequest(action, Id: id, TargetArchiverId: Guid.NewGuid())), "id", "UserGroup");
    }

    private static readonly Guid UnitId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid TargetArchiverId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid CameraId = Guid.Parse("99999999-8888-7777-6666-555555555555");

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            });
        }
    }

    // ── Surface ─────────────────────────────────────────────────────

    [Fact]
    public void SupportedActions_ContainAllVideoUnitOperations()
    {
        Assert.Equal(
            [
                VideoUnitOperationsAction.Move,
                VideoUnitOperationsAction.Reconnect,
                VideoUnitOperationsAction.Reboot,
                VideoUnitOperationsAction.DeleteGhostCamera
            ],
            Enum.GetValues<VideoUnitOperationsAction>());
    }

    // ── Validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData(VideoUnitOperationsAction.Move)]
    [InlineData(VideoUnitOperationsAction.Reconnect)]
    [InlineData(VideoUnitOperationsAction.Reboot)]
    public async Task ActionRequiringVideoUnitId_NullId_ThrowsArgumentException(VideoUnitOperationsAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            VideoUnitOperationsTool.Execute(TestHelpers.FakeManager(),
                new VideoUnitOperationsRequest(action, Id: null)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Move_NullTargetArchiverId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            VideoUnitOperationsTool.Execute(TestHelpers.FakeManager(),
                new VideoUnitOperationsRequest(VideoUnitOperationsAction.Move, Id: Guid.NewGuid(), TargetArchiverId: null)));

        Assert.Contains("target_archiver_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteGhostCamera_NullCameraId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            VideoUnitOperationsTool.Execute(TestHelpers.FakeManager(),
                new VideoUnitOperationsRequest(VideoUnitOperationsAction.DeleteGhostCamera, CameraId: null)));

        Assert.Contains("camera_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownAction_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            VideoUnitOperationsTool.Execute(TestHelpers.FakeManager(),
                new VideoUnitOperationsRequest((VideoUnitOperationsAction)999)));

        Assert.Contains("Unknown action", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(VideoUnitOperationsAction.Move, "MoveVideoUnit(11111111-2222-3333-4444-555555555555,aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee)")]
    [InlineData(VideoUnitOperationsAction.Reconnect, "ReconnectVideoUnit(11111111-2222-3333-4444-555555555555)")]
    [InlineData(VideoUnitOperationsAction.Reboot, "RebootVideoUnit(11111111-2222-3333-4444-555555555555)")]
    [InlineData(VideoUnitOperationsAction.DeleteGhostCamera, "DeleteGhostCamera(99999999-8888-7777-6666-555555555555)")]
    public async Task Action_UsesDocumentedWebSdkActionRoute(
        VideoUnitOperationsAction action, string expectedAction)
    {
        var handler = new CapturingHandler();
        var metadata = new EntityMetadataTestHandler(handler);
        metadata.AddEntity(UnitId, "VideoUnit");
        metadata.AddEntity(TargetArchiverId, "Role");
        var request = action switch
        {
            VideoUnitOperationsAction.Move => new VideoUnitOperationsRequest(
                action, Id: UnitId, TargetArchiverId: TargetArchiverId),
            VideoUnitOperationsAction.DeleteGhostCamera => new VideoUnitOperationsRequest(
                action, CameraId: CameraId),
            _ => new VideoUnitOperationsRequest(action, Id: UnitId)
        };

        var result = await VideoUnitOperationsTool.Execute(TestHelpers.FakeManager(metadata), request);

        Assert.Equal("[]", result);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Contains($"action?q={expectedAction}",
            Uri.UnescapeDataString(handler.LastRequest.RequestUri!.ToString()), StringComparison.Ordinal);
    }

}
