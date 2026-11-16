using System.Text.Json;
using System.Xml.Linq;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class AlarmWorkflowToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task Forward_RejectsWrongRecipientType()
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => AlarmWorkflowTool.Execute(sc.Manager, new AlarmWorkflowRequest(AlarmWorkflowAction.Forward, InstanceId: 1, RecipientIds: [id])), "recipient_ids", "Partition");
    }

    [Theory]
    [InlineData(AlarmWorkflowAction.Trigger)]
    [InlineData(AlarmWorkflowAction.Acknowledge)]
    [InlineData(AlarmWorkflowAction.ForceAcknowledge)]
    [InlineData(AlarmWorkflowAction.Investigate)]
    [InlineData(AlarmWorkflowAction.EditContext)]
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity(AlarmWorkflowAction action)
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => AlarmWorkflowTool.Execute(sc.Manager, new AlarmWorkflowRequest(action, Id: id, InstanceId: 1, Context: "Test context")), "id", "UserGroup");
    }

    [Fact]
    public async Task Trigger_ReturnsInstanceId()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var alarm = await CreateAlarm();
        var instanceId = await TriggerAlarm(alarm.Guid);

        Assert.True(instanceId > 0);
        await AcknowledgeAndWait(alarm.Guid, instanceId);
    }

    [Fact]
    public async Task Trigger_WithSourceEntity_ReturnsInstanceId()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var alarm = await CreateAlarm();
        var source = await CreateUser();
        var instanceId = await TriggerAlarm(alarm.Guid, source.Guid);

        Assert.True(instanceId > 0);
        await AcknowledgeAndWait(alarm.Guid, instanceId);
    }

    [Fact]
    public async Task Acknowledge_TriggeredAlarm_RemovesItFromActiveAlarms()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var alarm = await CreateAlarm();
        var instanceId = await TriggerAlarm(alarm.Guid);

        await AcknowledgeAndWait(alarm.Guid, instanceId);
    }

    [Fact]
    public async Task GetActive_ReturnsActiveAlarmArray()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = Assert.IsType<string>(await AlarmWorkflowTool.Execute(
            sc.Manager,
            new AlarmWorkflowRequest(AlarmWorkflowAction.GetActive)));

        using var document = IntegrationTestHelpers.ParseJson(result);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
    }

    [Theory]
    [InlineData(AlarmWorkflowAction.ForceAcknowledge)]
    [InlineData(AlarmWorkflowAction.Investigate)]
    public async Task InstanceAction_TriggeredAlarm_Succeeds(AlarmWorkflowAction action)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var alarm = await CreateAlarm();
        var instanceId = await TriggerAlarm(alarm.Guid);

        var result = Assert.IsType<string>(await AlarmWorkflowTool.Execute(
            sc.Manager,
            new AlarmWorkflowRequest(action, Id: alarm.Guid, InstanceId: instanceId)));
        Assert.NotNull(result);

        if (action == AlarmWorkflowAction.Investigate)
            await AcknowledgeAndWait(alarm.Guid, instanceId);
    }

    [Fact]
    public async Task ForceAcknowledgeAll_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await AlarmWorkflowTool.Execute(sc.Manager,
            new AlarmWorkflowRequest(AlarmWorkflowAction.ForceAcknowledgeAll));

        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task Forward_TriggeredAlarm_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var alarm = await CreateAlarm();
        var recipient = await CreateUser();
        var instanceId = await TriggerAlarm(alarm.Guid);

        var result = Assert.IsType<string>(await AlarmWorkflowTool.Execute(
            sc.Manager,
            new AlarmWorkflowRequest(
                AlarmWorkflowAction.Forward,
                InstanceId: instanceId,
                RecipientIds: [recipient.Guid])));
        Assert.NotNull(result);

        await AcknowledgeAndWait(alarm.Guid, instanceId);
    }

    [Fact]
    public async Task ClearSourceCondition_WithSourceAndCondition_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var alarm = await CreateAlarm();
        var source = await CreateUser();
        var instanceId = await TriggerAlarm(alarm.Guid, source.Guid);

        var result = Assert.IsType<string>(await AlarmWorkflowTool.Execute(
            sc.Manager,
            new AlarmWorkflowRequest(
                AlarmWorkflowAction.ClearSourceCondition,
                SourceEntityId: source.Guid,
                ConditionId: 1)));

        Assert.NotNull(result);
        await AcknowledgeAndWait(alarm.Guid, instanceId);
    }

    [Fact]
    public async Task EditContext_LiteralQueryCharacters_ReadsBackFromActiveAlarm()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var alarm = await CreateAlarm();
        var instanceId = await TriggerAlarm(alarm.Guid);

        const string context = @"MCP %2C & # + , (literal) @site\item";
        try
        {
            Assert.IsType<string>(await AlarmWorkflowTool.Execute(sc.Manager,
                new AlarmWorkflowRequest(AlarmWorkflowAction.EditContext,
                    Id: alarm.Guid, InstanceId: instanceId, Context: context)));
            var json = Assert.IsType<string>(await AlarmWorkflowTool.Execute(sc.Manager,
                new AlarmWorkflowRequest(AlarmWorkflowAction.GetActive)));
            using var document = IntegrationTestHelpers.ParseJson(json);
            var active = Assert.Single(IntegrationTestHelpers.GetResult(document.RootElement).EnumerateArray(),
                row => row.GetProperty("InstanceID").GetInt32() == instanceId);
            Assert.Equal(alarm.Guid, active.GetProperty("Guid").GetGuid());
            var content = XDocument.Parse(active.GetProperty("DynamicContent").GetString()!);
            Assert.Equal(context, content.Root!.Element("Context")!.Value);
        }
        finally
        {
            await AcknowledgeAndWait(alarm.Guid, instanceId);
        }
    }

    private async Task<EntityCreateResult> CreateAlarm()
    {
        var result = Assert.IsType<EntityCreateResult>(await AlarmCreateTool.Execute(
            sc.Manager,
            new AlarmCreateRequest([new AlarmItem($"MCP_TestAlarm_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(result.Guid);
        return result;
    }

    private async Task<EntityCreateResult> CreateUser()
    {
        var result = Assert.IsType<EntityCreateResult>(await UserCreateTool.Execute(
            sc.Manager,
            new UserCreateRequest([new UserItem($"MCP_AlarmWorkflowUser_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(result.Guid);
        return result;
    }

    private async Task<int> TriggerAlarm(Guid alarmId, Guid? sourceEntityId = null)
    {
        var result = Assert.IsType<string>(await AlarmWorkflowTool.Execute(
            sc.Manager,
            new AlarmWorkflowRequest(
                AlarmWorkflowAction.Trigger,
                Id: alarmId,
                SourceEntityId: sourceEntityId)));
        return IntegrationTestHelpers.GetAlarmInstanceId(result);
    }

    private async Task AcknowledgeAndWait(Guid alarmId, int instanceId)
    {
        var result = Assert.IsType<string>(await AlarmWorkflowTool.Execute(
            sc.Manager,
            new AlarmWorkflowRequest(
                AlarmWorkflowAction.Acknowledge,
                Id: alarmId,
                InstanceId: instanceId)));
        using var document = IntegrationTestHelpers.ParseJson(result);
        Assert.True(document.RootElement.GetProperty("Acknowledged").GetBoolean());

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var activeResult = Assert.IsType<string>(await AlarmWorkflowTool.Execute(
                sc.Manager,
                new AlarmWorkflowRequest(AlarmWorkflowAction.GetActive)));
            if (!IntegrationTestHelpers.ContainsAlarmInstance(activeResult, instanceId))
                return;

            await Task.Delay(250);
        }

        Assert.Fail($"Alarm instance {instanceId} remained active after acknowledgment.");
    }
}
