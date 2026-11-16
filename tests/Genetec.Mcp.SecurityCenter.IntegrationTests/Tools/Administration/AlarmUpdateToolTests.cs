using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class AlarmUpdateToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Update_RenameAlarm_ReturnsStatusOk()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var created = (EntityCreateResult)await AlarmCreateTool.Execute(sc.Manager,
            new AlarmCreateRequest(Items: [new AlarmItem(Name: $"MCP_AlarmUpd_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(created.Guid);

        var newName = $"MCP_AlarmUpd_Renamed_{Guid.NewGuid():N}";
        var result = await AlarmUpdateTool.Execute(sc.Manager,
            new AlarmUpdateRequest(Items: [new AlarmUpdateItem(Id: created.Guid, Name: newName)]));

        Assert.IsType<string>(result);

        await AlarmDeleteTool.Execute(sc.Manager, new AlarmDeleteRequest(Items: [created.Guid]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Update_WrongEntityType_RejectsSingleAndBatchAndPreservesEntity(int count)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var request = new AlarmUpdateRequest(Enumerable.Range(0, count)
            .Select(_ => new AlarmUpdateItem(id, Name: "Must not update")).ToList());
        if (count == 1)
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => AlarmUpdateTool.Execute(sc.Manager, request));
            Assert.Equal("items", error.ParamName);
        }
        else
        {
            using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(await AlarmUpdateTool.Execute(sc.Manager, request)));
            var result = IntegrationTestHelpers.GetResult(document.RootElement);
            Assert.Equal(0, result.GetProperty("success_count").GetInt32());
            Assert.Equal(count, result.GetProperty("failure_count").GetInt32());
        }
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
