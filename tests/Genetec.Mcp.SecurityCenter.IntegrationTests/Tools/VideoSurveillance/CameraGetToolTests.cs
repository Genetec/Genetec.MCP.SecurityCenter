using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.VideoSurveillance;

public class CameraGetToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Get_FirstCamera_ReturnsRequestedGuidAndListedName()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var listResult = await CameraListTool.Execute(sc.Manager, new CameraListRequest(PageSize: 1, Page: 1));
        var cameraId = ScIntegrationFixture.ExtractFirstGuid((string)listResult);
        Assert.NotNull(cameraId);
        using var listDocument = IntegrationTestHelpers.ParseJson((string)listResult);
        var rows = IntegrationTestHelpers.GetResult(listDocument.RootElement).ValueKind == JsonValueKind.Array
            ? IntegrationTestHelpers.GetResult(listDocument.RootElement) : IntegrationTestHelpers.GetResult(listDocument.RootElement).GetProperty("Body");
        var firstCamera = rows[0];
        var expectedName = firstCamera.ValueKind == JsonValueKind.Array
            ? firstCamera[1].GetString() : firstCamera.GetProperty("Name").GetString();

        var result = await CameraGetTool.Execute(sc.Manager, new CameraGetTool.CameraGetRequest(cameraId!.Value));
        using var entityDocument = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(result));
        Assert.Equal(cameraId.Value, IntegrationTestHelpers.GetResult(entityDocument.RootElement).GetProperty("Guid").GetGuid());
        Assert.Equal(expectedName, IntegrationTestHelpers.GetResult(entityDocument.RootElement).GetProperty("Name").GetString());
    }

    [Fact]
    public async Task Get_WrongEntityType_RejectsAndPreservesEntity()
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CameraGetTool.Execute(sc.Manager,
            new CameraGetTool.CameraGetRequest(id)));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
