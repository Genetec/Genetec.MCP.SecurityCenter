using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Utilities;

public class SystemExecutionToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(ExecutionAction.TriggerOutput)]
    [InlineData(ExecutionAction.ExecuteActionPlan)]
    public async Task WrongTargetType_IsRejectedWithoutChangingEntity(ExecutionAction action)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var id = Guid.Parse("00000000-0000-0000-0000-000000000007");
        var before = await sc.Manager.GetClient().GetEntityFields(id.ToString(), "Guid", "Name");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => SystemExecutionTool.Execute(sc.Manager,
            new SystemExecutionRequest(action, OutputId: id, OutputBehaviorId: id, ActionPlanId: id)));
        Assert.Equal(action == ExecutionAction.TriggerOutput ? "output_id" : "action_plan_id", error.ParamName);
        Assert.Equal(before, await sc.Manager.GetClient().GetEntityFields(id.ToString(), "Guid", "Name"));
    }

    // ── Integration ─────────────────────────────────────────────────
    // These operations cause physical side effects and require device-specific GUIDs.
    // Random GUIDs exercise the full Web SDK path. Security Center may reject the
    // entities, but it must not reject the action because its method signature is wrong.

    [Fact]
    public async Task TriggerOutput_InvalidOutputId_ThrowsScError()
    {
        var ex = await Record.ExceptionAsync(() =>
            SystemExecutionTool.Execute(sc.Manager,
                new SystemExecutionRequest(ExecutionAction.TriggerOutput,
                    OutputId: Guid.NewGuid(),
                    OutputBehaviorId: Guid.NewGuid())));

        Assert.DoesNotContain("No method matches", ex?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendCustomAction_InvalidIds_ThrowsScError()
    {
        var ex = await Record.ExceptionAsync(() =>
            SystemExecutionTool.Execute(sc.Manager,
                new SystemExecutionRequest(ExecutionAction.SendCustomAction,
                    CustomActionId: Guid.NewGuid(),
                    EntityId: Guid.NewGuid(),
                    Payload: "mcp integration test")));

        Assert.DoesNotContain("No method matches", ex?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteActionPlan_InvalidActionPlanId_ThrowsScError()
    {
        var ex = await Record.ExceptionAsync(() =>
            SystemExecutionTool.Execute(sc.Manager,
                new SystemExecutionRequest(ExecutionAction.ExecuteActionPlan, ActionPlanId: Guid.NewGuid())));

        Assert.DoesNotContain("No method matches", ex?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
