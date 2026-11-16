using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Utilities;

[McpServerToolType]
public class SystemExecutionTool
{
    [McpServerTool(
        Name = "system_execution",
        Title = "System Execution",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description("Trigger hardware outputs, execute action plans, and send custom actions. " +
                 "Actions: trigger_output, send_custom_action, execute_action_plan.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("System execution request")] SystemExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case ExecutionAction.TriggerOutput:
                if (request.OutputId is null) throw new ArgumentException("Parameter 'output_id' is required for action 'trigger_output'.");
                if (request.OutputBehaviorId is null) throw new ArgumentException("Parameter 'output_behavior_id' is required for action 'trigger_output'.");
                await client.RequireEntityType(request.OutputId.Value, "output_id", "Device");
                await client.RequireEntityType(request.OutputBehaviorId.Value, "output_behavior_id", "OutputBehavior");
                return await client.ExecuteAction($"TriggerOutput({request.OutputId},{request.OutputBehaviorId})");

            case ExecutionAction.SendCustomAction:
                if (request.CustomActionId is null) throw new ArgumentException("Parameter 'custom_action_id' is required for action 'send_custom_action'.");
                if (request.EntityId is null) throw new ArgumentException("Parameter 'entity_id' is required for action 'send_custom_action'.");
                if (string.IsNullOrWhiteSpace(request.Payload)) throw new ArgumentException("Parameter 'payload' is required for action 'send_custom_action'.");
                return await client.ExecuteAction(
                    $"SendCustomAction({request.CustomActionId},@{{{request.EntityId}}},{SecurityCenterRestClient.EscapeQueryValue(request.Payload)})");

            case ExecutionAction.ExecuteActionPlan:
                if (request.ActionPlanId is null) throw new ArgumentException("Parameter 'action_plan_id' is required for action 'execute_action_plan'.");
                await client.RequireEntityType(request.ActionPlanId.Value, "action_plan_id", "ActionPlan");
                return await client.ExecuteAction($"ExecuteActionPlan({request.ActionPlanId})");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum ExecutionAction { TriggerOutput, SendCustomAction, ExecuteActionPlan }

public sealed record SystemExecutionRequest(
    [property: Description("Action to perform"), Required] ExecutionAction Action,
    [property: Description("Target entity GUID (for send_custom_action)")] Guid? EntityId = null,
    [property: Description("Output GUID (for trigger_output)")] Guid? OutputId = null,
    [property: Description("Output behavior GUID (for trigger_output)")] Guid? OutputBehaviorId = null,
    [property: Description("Action plan GUID (for execute_action_plan)")] Guid? ActionPlanId = null,
    [property: Description("Custom action descriptor GUID (for send_custom_action)")] Guid? CustomActionId = null,
    [property: Description("Custom action payload (for send_custom_action)")] string? Payload = null);
