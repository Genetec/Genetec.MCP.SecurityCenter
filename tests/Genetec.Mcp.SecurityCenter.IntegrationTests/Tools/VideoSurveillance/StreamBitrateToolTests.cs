using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.VideoSurveillance;

public class StreamBitrateToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task RelatedEntityTargets_RejectWrongEntityTypeAndPreserveEntities()
    {
        var cameraId = await IntegrationTestHelpers.GetFirstVideoEncoderCameraIdAsync(sc);
        using var camera = IntegrationTestHelpers.ParseJson(await sc.Manager.GetClient().GetEntity(cameraId.ToString()));
        var primaryId = IntegrationTestHelpers.GetResult(camera.RootElement).GetProperty("Streams")[0].GetGuid();
        await IntegrationTestHelpers.AssertWrongRelatedEntityTypeAsync(sc, primaryId, "Stream", relatedId => StreamBitrateTool.Execute(sc.Manager, new StreamBitrateRequest(primaryId, 512, relatedId)), "schedule_id", "UserGroup");
    }

    [Fact]
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity()
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => StreamBitrateTool.Execute(sc.Manager, new StreamBitrateRequest(id, 512)), "id", "UserGroup");
    }

    [Fact]
    public async Task Execute_FirstConfiguredStream_PersistsBitRate()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        Guid? cameraId = await IntegrationTestHelpers.GetFirstVideoEncoderCameraIdAsync(sc);
        Assert.NotNull(cameraId);

        var cameraJson = await sc.Manager.GetClient().GetEntity(cameraId!.Value.ToString());
        using var cameraDoc = JsonDocument.Parse(cameraJson);
        var streamIds = cameraDoc.RootElement.GetProperty("Streams")
            .EnumerateArray()
            .Select(value => value.GetGuid())
            .ToArray();

        Guid? streamId = null;
        int? existingBitRate = null;
        foreach (var candidate in streamIds)
        {
            var streamJson = await sc.Manager.GetClient().GetEntity(candidate.ToString());
            using var streamDoc = JsonDocument.Parse(streamJson);
            if (!streamDoc.RootElement.TryGetProperty("VideoCompressions", out var configurations) ||
                configurations.GetArrayLength() == 0)
                continue;

            streamId = candidate;
            existingBitRate = configurations[0].GetProperty("BitRate").GetInt32();
            break;
        }

        Assert.NotNull(streamId);
        Assert.NotNull(existingBitRate);

        await StreamBitrateTool.Execute(sc.Manager,
            new StreamBitrateRequest(streamId!.Value, existingBitRate!.Value));

        var updatedJson = await sc.Manager.GetClient().GetEntity(streamId.Value.ToString());
        using var updatedDoc = JsonDocument.Parse(updatedJson);
        Assert.Equal(existingBitRate.Value,
            updatedDoc.RootElement.GetProperty("VideoCompressions")[0].GetProperty("BitRate").GetInt32());
    }
}
