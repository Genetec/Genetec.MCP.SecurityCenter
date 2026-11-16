using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class AlarmGetToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Get_CreatedAlarm_ReturnsNameInResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var name = $"MCP_Alarm_{Guid.NewGuid():N}";
        var createResult = (EntityCreateResult)await AlarmCreateTool.Execute(sc.Manager,
            new AlarmCreateRequest([new AlarmItem(name)]));
        Assert.NotEqual(default(Guid), createResult.Guid);
        sc.TrackForCleanup(createResult.Guid!);

        var result = await AlarmGetTool.Execute(sc.Manager, new AlarmGetTool.AlarmGetRequest(createResult.Guid));
        using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(result));
        Assert.Equal(createResult.Guid, document.RootElement.GetProperty("Guid").GetGuid());
        Assert.Equal(name, document.RootElement.GetProperty("Name").GetString());
    }

    [Fact]
    public async Task Get_WrongEntityType_RejectsAndPreservesEntity()
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AlarmGetTool.Execute(sc.Manager,
            new AlarmGetTool.AlarmGetRequest(id)));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
