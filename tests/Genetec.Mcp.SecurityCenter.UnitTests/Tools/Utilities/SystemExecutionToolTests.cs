using System.Net;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Utilities;

public class SystemExecutionToolTests
{
    [Theory]
    [InlineData(ExecutionAction.TriggerOutput)]
    [InlineData(ExecutionAction.ExecuteActionPlan)]
    public async Task EmptyTypedTarget_IsRejected(ExecutionAction action)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => SystemExecutionTool.Execute(
            TestHelpers.FakeManager(), new SystemExecutionRequest(action,
                OutputId: Guid.Empty, OutputBehaviorId: Guid.NewGuid(), ActionPlanId: Guid.Empty)));
        Assert.Equal(action == ExecutionAction.TriggerOutput ? "output_id" : "action_plan_id", error.ParamName);
    }

    private static readonly Guid OutputId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OutputBehaviorId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid ActionPlanId = Guid.Parse("12345678-1234-1234-1234-123456789012");
    private static readonly Guid CustomActionId = Guid.Parse("87654321-4321-4321-4321-210987654321");
    private static readonly Guid EntityId = Guid.Parse("fedcba98-7654-3210-fedc-ba9876543210");

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var response = "[]";
            var query = Uri.UnescapeDataString(request.RequestUri!.Query);
            if (query.Contains("Guid,EntityType", StringComparison.Ordinal))
            {
                var (id, type) = query.Contains(OutputId.ToString(), StringComparison.Ordinal)
                    ? (OutputId, "Device")
                    : query.Contains(OutputBehaviorId.ToString(), StringComparison.Ordinal)
                        ? (OutputBehaviorId, "OutputBehavior")
                        : (ActionPlanId, "ActionPlan");
                response = System.Text.Json.JsonSerializer.Serialize(new { Guid = id, EntityType = type });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response)
            });
        }
    }
    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task TriggerOutput_MissingOutputId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemExecutionTool.Execute(TestHelpers.FakeManager(),
                new SystemExecutionRequest(ExecutionAction.TriggerOutput)));
        Assert.Contains("output_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TriggerOutput_MissingOutputBehaviorId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemExecutionTool.Execute(TestHelpers.FakeManager(),
                new SystemExecutionRequest(ExecutionAction.TriggerOutput, OutputId: OutputId)));

        Assert.Contains("output_behavior_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteActionPlan_MissingActionPlanId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemExecutionTool.Execute(TestHelpers.FakeManager(),
                new SystemExecutionRequest(ExecutionAction.ExecuteActionPlan)));
        Assert.Contains("action_plan_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendCustomAction_MissingCustomActionId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemExecutionTool.Execute(TestHelpers.FakeManager(),
                new SystemExecutionRequest(ExecutionAction.SendCustomAction,
                    EntityId: Guid.NewGuid())));
        Assert.Contains("custom_action_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendCustomAction_MissingEntityId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemExecutionTool.Execute(TestHelpers.FakeManager(),
                new SystemExecutionRequest(ExecutionAction.SendCustomAction,
                    CustomActionId: Guid.NewGuid())));
        Assert.Contains("entity_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task SendCustomAction_MissingPayload_ThrowsArgumentException(string? payload)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemExecutionTool.Execute(TestHelpers.FakeManager(),
                new SystemExecutionRequest(ExecutionAction.SendCustomAction,
                    EntityId: EntityId,
                    CustomActionId: CustomActionId,
                    Payload: payload)));

        Assert.Contains("payload", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownAction_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemExecutionTool.Execute(TestHelpers.FakeManager(),
                new SystemExecutionRequest((ExecutionAction)999)));

        Assert.Contains("Unknown action", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Web SDK route and query construction ────────────────────────

    [Fact]
    public async Task TriggerOutput_UsesOutputBehaviorGuidInWebSdkActionQuery()
    {
        var handler = new CapturingHandler();

        var result = await SystemExecutionTool.Execute(
            TestHelpers.FakeManager(handler),
            new SystemExecutionRequest(ExecutionAction.TriggerOutput,
                OutputId: OutputId,
                OutputBehaviorId: OutputBehaviorId));

        Assert.Equal("[]", result);
        Assert.Contains($"action?q=TriggerOutput({OutputId},{OutputBehaviorId})",
            Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.AbsoluteUri), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendCustomAction_UsesRecipientCollectionAndEscapedPayloadInWebSdkActionQuery()
    {
        var handler = new CapturingHandler();
        const string payload = "hello, (from) MCP";

        var result = await SystemExecutionTool.Execute(
            TestHelpers.FakeManager(handler),
            new SystemExecutionRequest(ExecutionAction.SendCustomAction,
                EntityId: EntityId,
                CustomActionId: CustomActionId,
                Payload: payload));

        Assert.Equal("[]", result);
        var query = Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.AbsoluteUri);
        Assert.Contains($"action?q=SendCustomAction({CustomActionId},@{{{EntityId}}},hello\\, \\(from\\) MCP)",
            query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteActionPlan_UsesActionPlanGuidInWebSdkActionQuery()
    {
        var handler = new CapturingHandler();

        var result = await SystemExecutionTool.Execute(
            TestHelpers.FakeManager(handler),
            new SystemExecutionRequest(ExecutionAction.ExecuteActionPlan,
                ActionPlanId: ActionPlanId));

        Assert.Equal("[]", result);
        Assert.Contains($"action?q=ExecuteActionPlan({ActionPlanId})",
            Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.AbsoluteUri), StringComparison.Ordinal);
    }
}
