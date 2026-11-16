using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.VideoSurveillance;

public class CameraListToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await CameraListTool.Execute(sc.Manager, new CameraListRequest(PageSize: 50, Page: 1));

        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }

    [Fact]
    public async Task List_PageSizeOne_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await CameraListTool.Execute(sc.Manager, new CameraListRequest(PageSize: 1, Page: 1));

        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }
}
