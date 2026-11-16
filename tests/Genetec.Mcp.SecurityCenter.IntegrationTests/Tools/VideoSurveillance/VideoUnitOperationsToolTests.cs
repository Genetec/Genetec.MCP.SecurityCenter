using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.VideoSurveillance;

public class VideoUnitOperationsToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task RelatedEntityTargets_RejectWrongEntityTypeAndPreserveEntities()
    {
        var primaryId = await GetFirstVideoUnitIdAsync();
        await IntegrationTestHelpers.AssertWrongRelatedEntityTypeAsync(sc, primaryId, "VideoUnit", relatedId => VideoUnitOperationsTool.Execute(sc.Manager, new VideoUnitOperationsRequest(VideoUnitOperationsAction.Move, Id: primaryId, TargetArchiverId: relatedId)), "target_archiver_id", "UserGroup");
    }

    [Theory]
    [InlineData(VideoUnitOperationsAction.Move)]
    [InlineData(VideoUnitOperationsAction.Reconnect)]
    [InlineData(VideoUnitOperationsAction.Reboot)]
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity(VideoUnitOperationsAction action)
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => VideoUnitOperationsTool.Execute(sc.Manager, new VideoUnitOperationsRequest(action, Id: id, TargetArchiverId: Guid.NewGuid())), "id", "UserGroup");
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Reconnect_FirstVideoUnit_ReturnsRawSdkResult()
    {
        RequireSecurityCenter();
        var unitId = await GetFirstVideoUnitIdAsync();

        var result = await VideoUnitOperationsTool.Execute(sc.Manager,
            new VideoUnitOperationsRequest(VideoUnitOperationsAction.Reconnect, Id: unitId));

        AssertValidSdkResult(result);
    }

    [Fact]
    public async Task Move_FirstVideoUnit_ToCurrentArchiver_ReturnsRawSdkResult()
    {
        RequireSecurityCenter();
        var unitId = await GetFirstVideoUnitIdAsync();
        var archiverId = await GetVideoUnitOwnerRoleIdAsync(unitId);

        var result = await VideoUnitOperationsTool.Execute(sc.Manager,
            new VideoUnitOperationsRequest(VideoUnitOperationsAction.Move,
                Id: unitId, TargetArchiverId: archiverId));

        AssertValidSdkResult(result);
    }

    [Theory]
    [InlineData(VideoUnitOperationsAction.Move)]
    [InlineData(VideoUnitOperationsAction.Reconnect)]
    [InlineData(VideoUnitOperationsAction.Reboot)]
    [InlineData(VideoUnitOperationsAction.DeleteGhostCamera)]
    public async Task UnknownEntityIds_ReachSecurityCenter(VideoUnitOperationsAction action)
    {
        RequireSecurityCenter();

        var exception = await Record.ExceptionAsync(() =>
            VideoUnitOperationsTool.Execute(sc.Manager, CreateUnknownEntityRequest(action)));

        Assert.IsNotType<ArgumentException>(exception);
    }

    private void RequireSecurityCenter()
        => Assert.True(sc.IsAvailable,
            "Security Center integration configuration is required; configure appsettings.test.json or SC_HOST.");

    private async Task<Guid> GetFirstVideoUnitIdAsync()
    {
        var json = await sc.Manager.GetClient().ListEntities("VideoUnit", 1, 1);
        var id = ScIntegrationFixture.ExtractFirstGuid(json);
        Assert.True(id.HasValue, "No video units are configured for integration tests.");
        return id.Value;
    }

    private async Task<Guid> GetVideoUnitOwnerRoleIdAsync(Guid unitId)
    {
        var json = await sc.Manager.GetClient().GetEntityTrimmed(unitId.ToString());
        using var document = JsonDocument.Parse(json);
        var ownerRoleText = document.RootElement.TryGetProperty("OwnerRole", out var ownerRole)
            ? ownerRole.GetString()
            : null;
        Assert.True(Guid.TryParse(ownerRoleText, out var ownerRoleId),
            "The video unit did not expose a valid owner archiver role.");
        return ownerRoleId;
    }

    private static void AssertValidSdkResult(object result)
    {
        var json = Assert.IsType<string>(result);
        Assert.NotEmpty(json);
        using var document = JsonDocument.Parse(json);
        Assert.NotEqual(JsonValueKind.Undefined, document.RootElement.ValueKind);
    }

    private static VideoUnitOperationsRequest CreateUnknownEntityRequest(VideoUnitOperationsAction action)
    {
        var id = Guid.NewGuid();

        return action switch
        {
            VideoUnitOperationsAction.Move => new VideoUnitOperationsRequest(
                action, Id: id, TargetArchiverId: Guid.NewGuid()),
            VideoUnitOperationsAction.DeleteGhostCamera => new VideoUnitOperationsRequest(
                action, CameraId: Guid.NewGuid()),
            _ => new VideoUnitOperationsRequest(action, Id: id)
        };
    }
}
