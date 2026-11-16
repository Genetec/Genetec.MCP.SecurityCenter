using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.VideoSurveillance;

public class VideoSurveillancePaginationToolTests(ScIntegrationFixture sc, Xunit.Abstractions.ITestOutputHelper output) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData("CameraListTool", 1)]
    [InlineData("CameraListTool", 2)]
    [InlineData("CameraListTool", 100)]
    [InlineData("CameraSequenceManagementTool", 1)]
    [InlineData("CameraSequenceManagementTool", 2)]
    [InlineData("CameraSequenceManagementTool", 100)]
    [InlineData("VideoUnitListTool", 1)]
    [InlineData("VideoUnitListTool", 2)]
    [InlineData("VideoUnitListTool", 100)]
    public Task SmallPages_RespectLimitAndEntityIdentity(string tool, int pageSize)
        => IntegrationTestHelpers.AssertSmallPages(sc, (size, page) => tool switch
        {
            "CameraListTool" => CameraListTool.Execute(sc.Manager, new CameraListRequest(PageSize: size, Page: page)),
            "CameraSequenceManagementTool" => CameraSequenceManagementTool.Execute(sc.Manager, new CameraSequenceManagementRequest(CameraSequenceAction.List, PageSize: size, Page: page)),
            "VideoUnitListTool" => VideoUnitListTool.Execute(sc.Manager, new VideoUnitListRequest(PageSize: size, Page: page)),
            _ => throw new ArgumentException("Unknown pagination test case.", nameof(tool))
        }, pageSize, output.WriteLine);
}
