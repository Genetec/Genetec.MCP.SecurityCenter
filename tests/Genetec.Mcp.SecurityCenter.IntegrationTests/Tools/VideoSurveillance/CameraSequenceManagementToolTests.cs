using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.VideoSurveillance;

public class CameraSequenceManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task List_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await CameraSequenceManagementTool.Execute(
            sc.Manager,
            new CameraSequenceManagementRequest(CameraSequenceAction.List));

        Assert.NotEmpty(Assert.IsType<string>(result));
    }

    [Fact]
    public async Task Get_FirstSequence_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var id = await GetFirstSequenceIdAsync();

        var result = await CameraSequenceManagementTool.Execute(sc.Manager,
            new CameraSequenceManagementRequest(CameraSequenceAction.Get, Id: id));

        Assert.NotEmpty(Assert.IsType<string>(result));
    }

    [Fact]
    public async Task GetCameras_FirstSequence_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var id = await GetFirstSequenceIdAsync();

        var result = await CameraSequenceManagementTool.Execute(sc.Manager,
            new CameraSequenceManagementRequest(CameraSequenceAction.GetCameras, Id: id));

        Assert.NotEmpty(Assert.IsType<string>(result));
    }

    [Fact]
    public async Task Update_FirstSequence_ReturnsOkAndRestoresName()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var id = await GetFirstSequenceIdAsync();
        var originalName = ExtractName(await CameraSequenceManagementTool.Execute(sc.Manager,
            new CameraSequenceManagementRequest(CameraSequenceAction.Get, Id: id)));
        Assert.False(string.IsNullOrWhiteSpace(originalName),
            $"Camera sequence '{id}' did not return a name.");

        var newName = $"{originalName}_mcp_test";
        try
        {
            var result = await CameraSequenceManagementTool.Execute(sc.Manager,
                new CameraSequenceManagementRequest(CameraSequenceAction.Update, Items: [new CameraSequenceManagementItem(Name: newName, Id: id)]));

            Assert.NotEmpty(Assert.IsType<string>(result));
        }
        finally
        {
            await CameraSequenceManagementTool.Execute(sc.Manager,
                new CameraSequenceManagementRequest(CameraSequenceAction.Update, Items: [new CameraSequenceManagementItem(Name: originalName, Id: id)]));
        }
    }

    private async Task<Guid> GetFirstSequenceIdAsync()
    {
        var result = await CameraSequenceManagementTool.Execute(sc.Manager,
            new CameraSequenceManagementRequest(CameraSequenceAction.List));
        var id = IntegrationTestHelpers.ExtractFirstGuid(Assert.IsType<string>(result));
        Assert.True(id.HasValue,
            "No camera sequences are configured for integration tests.");
        return id.Value;
    }

    private static string ExtractName(object result)
    {
        var json = Assert.IsType<string>(result);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("Name", out var name)
            ? name.GetString() ?? string.Empty
            : string.Empty;
    }

    [Theory]
    [InlineData(CameraSequenceAction.Get)]
    [InlineData(CameraSequenceAction.Update)]
    [InlineData(CameraSequenceAction.GetCameras)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(CameraSequenceAction action)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var request = (action is CameraSequenceAction.Update ? new CameraSequenceManagementRequest(action, Items: [new CameraSequenceManagementItem(Id: id, Body: action == CameraSequenceAction.Update ? new SequenceUpdateProperties(Name: "Must not update") : null)]) : new CameraSequenceManagementRequest(action, Id: id));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CameraSequenceManagementTool.Execute(sc.Manager, request));
        Assert.Equal(action is CameraSequenceAction.Update ? "items" : "id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
