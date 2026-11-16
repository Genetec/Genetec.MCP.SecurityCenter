using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class AlarmWorkflowToolTests
{
    [Fact]
    public async Task Forward_RejectsWrongRecipientType()
    {
        await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => AlarmWorkflowTool.Execute(manager, new AlarmWorkflowRequest(AlarmWorkflowAction.Forward, InstanceId: 1, RecipientIds: [id])), "recipient_ids", "Partition");
    }

    [Theory]
    [InlineData(AlarmWorkflowAction.Trigger)]
    [InlineData(AlarmWorkflowAction.Acknowledge)]
    [InlineData(AlarmWorkflowAction.ForceAcknowledge)]
    [InlineData(AlarmWorkflowAction.Investigate)]
    [InlineData(AlarmWorkflowAction.EditContext)]
    public async Task CurrentEntityTargets_RejectWrongEntityType(AlarmWorkflowAction action)
    {
        await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => AlarmWorkflowTool.Execute(manager, new AlarmWorkflowRequest(action, Id: id, InstanceId: 1, Context: "Test context")), "id", "UserGroup");
    }

    private static readonly Guid AlarmId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid SourceEntityId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid RecipientId = Guid.Parse("12345678-1234-1234-1234-123456789012");

    [Theory]
    [InlineData(AlarmWorkflowAction.Trigger, "alarm?q=TriggerAlarm(11111111-2222-3333-4444-555555555555,aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee)")]
    [InlineData(AlarmWorkflowAction.Acknowledge, "alarm?q=AcknowledgeAlarm(42,11111111-2222-3333-4444-555555555555,Ack)")]
    [InlineData(AlarmWorkflowAction.GetActive, "activealarms")]
    [InlineData(AlarmWorkflowAction.ForceAcknowledge, "alarm?q=ForceAcknowledgeAlarm(42,11111111-2222-3333-4444-555555555555)")]
    [InlineData(AlarmWorkflowAction.ForceAcknowledgeAll, "alarm?q=ForceAcknowledgeAllAlarms()")]
    [InlineData(AlarmWorkflowAction.Investigate, "alarm?q=InvestigateAlarm(42,11111111-2222-3333-4444-555555555555)")]
    [InlineData(AlarmWorkflowAction.Forward, "alarm?q=ForwardAlarm(42,{12345678-1234-1234-1234-123456789012})")]
    [InlineData(AlarmWorkflowAction.ClearSourceCondition, "alarm?q=ClearSourceCondition(aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee,7)")]
    [InlineData(AlarmWorkflowAction.EditContext, "alarm?q=EditAlarmContext(42,11111111-2222-3333-4444-555555555555,Updated context)")]
    public async Task Actions_UseExpectedWebSdkRequest(
        AlarmWorkflowAction action, string expectedRequest)
    {
        var handler = new ToolTestHandler();
        var metadata = new EntityMetadataTestHandler(handler);
        metadata.AddEntity(AlarmId, "Alarm");
        metadata.AddEntity(RecipientId, "User");
        var request = action switch
        {
            AlarmWorkflowAction.Trigger => new AlarmWorkflowRequest(
                action, Id: AlarmId, SourceEntityId: SourceEntityId),
            AlarmWorkflowAction.Acknowledge or
            AlarmWorkflowAction.ForceAcknowledge or
            AlarmWorkflowAction.Investigate => new AlarmWorkflowRequest(
                action, Id: AlarmId, InstanceId: 42),
            AlarmWorkflowAction.GetActive or
            AlarmWorkflowAction.ForceAcknowledgeAll => new AlarmWorkflowRequest(action),
            AlarmWorkflowAction.Forward => new AlarmWorkflowRequest(
                action, InstanceId: 42, RecipientIds: [RecipientId]),
            AlarmWorkflowAction.ClearSourceCondition => new AlarmWorkflowRequest(
                action, SourceEntityId: SourceEntityId, ConditionId: 7),
            AlarmWorkflowAction.EditContext => new AlarmWorkflowRequest(
                action, Id: AlarmId, InstanceId: 42, Context: "Updated context"),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

        await AlarmWorkflowTool.Execute(TestHelpers.FakeManager(metadata), request);

        var capturedRequest = Assert.Single(handler.Requests);
        var uri = Uri.UnescapeDataString(capturedRequest.ToString());
        Assert.Contains(expectedRequest, uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Trigger_WithoutSourceEntity_UsesAlarmOnlySignature()
    {
        var handler = new ToolTestHandler();
        var metadata = new EntityMetadataTestHandler(handler);
        metadata.AddEntity(AlarmId, "Alarm");
        metadata.AddEntity(RecipientId, "User");

        await AlarmWorkflowTool.Execute(TestHelpers.FakeManager(metadata),
            new AlarmWorkflowRequest(AlarmWorkflowAction.Trigger, Id: AlarmId));

        var capturedRequest = Assert.Single(handler.Requests);
        var uri = Uri.UnescapeDataString(capturedRequest.ToString());
        Assert.Contains(
            $"alarm?q=TriggerAlarm({AlarmId})",
            uri,
            StringComparison.Ordinal);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Trigger_MissingId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmWorkflowTool.Execute(TestHelpers.FakeManager(),
                new AlarmWorkflowRequest(
                    AlarmWorkflowAction.Trigger, SourceEntityId: SourceEntityId)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AlarmWorkflowAction.Acknowledge)]
    [InlineData(AlarmWorkflowAction.ForceAcknowledge)]
    [InlineData(AlarmWorkflowAction.Investigate)]
    public async Task InstanceActions_MissingInstanceId_ThrowsArgumentException(
        AlarmWorkflowAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmWorkflowTool.Execute(TestHelpers.FakeManager(),
                new AlarmWorkflowRequest(action, Id: AlarmId)));

        Assert.Contains("instance_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AlarmWorkflowAction.Acknowledge)]
    [InlineData(AlarmWorkflowAction.ForceAcknowledge)]
    [InlineData(AlarmWorkflowAction.Investigate)]
    public async Task InstanceActions_MissingAlarmId_ThrowsArgumentException(
        AlarmWorkflowAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmWorkflowTool.Execute(TestHelpers.FakeManager(),
                new AlarmWorkflowRequest(action, InstanceId: 42)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AlarmWorkflowAction.Forward)]
    public async Task Forward_MissingInstanceId_ThrowsArgumentException(
        AlarmWorkflowAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmWorkflowTool.Execute(TestHelpers.FakeManager(),
                new AlarmWorkflowRequest(action, RecipientIds: [RecipientId])));

        Assert.Contains("instance_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Forward_MissingRecipients_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmWorkflowTool.Execute(TestHelpers.FakeManager(),
                new AlarmWorkflowRequest(AlarmWorkflowAction.Forward, InstanceId: 42)));

        Assert.Contains("recipient_ids", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Forward_EmptyRecipients_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmWorkflowTool.Execute(TestHelpers.FakeManager(),
                new AlarmWorkflowRequest(
                    AlarmWorkflowAction.Forward, InstanceId: 42, RecipientIds: [])));

        Assert.Contains("recipient_ids", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClearSourceCondition_MissingSourceEntityId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmWorkflowTool.Execute(TestHelpers.FakeManager(),
                new AlarmWorkflowRequest(
                    AlarmWorkflowAction.ClearSourceCondition, ConditionId: 1)));

        Assert.Contains("source_entity_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClearSourceCondition_MissingConditionId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmWorkflowTool.Execute(TestHelpers.FakeManager(),
                new AlarmWorkflowRequest(
                    AlarmWorkflowAction.ClearSourceCondition,
                    SourceEntityId: SourceEntityId)));

        Assert.Contains("condition_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EditContext_MissingAlarmId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmWorkflowTool.Execute(TestHelpers.FakeManager(),
                new AlarmWorkflowRequest(
                    AlarmWorkflowAction.EditContext,
                    InstanceId: 42,
                    Context: "context")));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EditContext_MissingInstanceId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmWorkflowTool.Execute(TestHelpers.FakeManager(),
                new AlarmWorkflowRequest(
                    AlarmWorkflowAction.EditContext,
                    Id: AlarmId,
                    Context: "context")));

        Assert.Contains("instance_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EditContext_MissingContext_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmWorkflowTool.Execute(TestHelpers.FakeManager(),
                new AlarmWorkflowRequest(
                    AlarmWorkflowAction.EditContext,
                    Id: AlarmId,
                    InstanceId: 42)));

        Assert.Contains("context", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownAction_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmWorkflowTool.Execute(TestHelpers.FakeManager(),
                new AlarmWorkflowRequest((AlarmWorkflowAction)999)));

        Assert.Contains("Unknown action", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
