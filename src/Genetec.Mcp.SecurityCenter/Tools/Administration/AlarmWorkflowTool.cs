using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;
using static Genetec.Mcp.SecurityCenter.Client.InputValidation;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class AlarmWorkflowTool
{
    [McpServerTool(
        Name = "alarm_workflow",
        Title = "Alarm Workflow",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Execute alarm workflow operations including triggering, acknowledging, investigating, " +
        "forwarding, and managing active alarms. Actions: trigger, acknowledge, get_active, " +
        "force_acknowledge, force_acknowledge_all, investigate, forward, clear_source_condition, " +
        "edit_context")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Alarm workflow request")] AlarmWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        static string Require(string? v, string p) =>
            v ?? throw new ArgumentException($"Parameter '{p}' is required for this action.", p);

        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case AlarmWorkflowAction.Trigger:
                var triggerQuery = request.SourceEntityId != null
                    ? $"TriggerAlarm({Require(request.Id?.ToString(), "id")},{request.SourceEntityId})"
                    : $"TriggerAlarm({Require(request.Id?.ToString(), "id")})";
                await client.RequireEntityType(request.Id!.Value, "id", "Alarm");
                return await client.AlarmAction(triggerQuery);

            case AlarmWorkflowAction.Acknowledge:
                var acknowledgeQuery = $"AcknowledgeAlarm({Require(request.InstanceId?.ToString(), "instance_id")},{Require(request.Id?.ToString(), "id")},Ack)";
                await client.RequireEntityType(request.Id!.Value, "id", "Alarm");
                return await client.AlarmAction(acknowledgeQuery);

            case AlarmWorkflowAction.GetActive:
                return await client.GetActiveAlarms();

            case AlarmWorkflowAction.ForceAcknowledge:
                var forceQuery = $"ForceAcknowledgeAlarm({Require(request.InstanceId?.ToString(), "instance_id")},{Require(request.Id?.ToString(), "id")})";
                await client.RequireEntityType(request.Id!.Value, "id", "Alarm");
                return await client.AlarmAction(forceQuery);

            case AlarmWorkflowAction.ForceAcknowledgeAll:
                return await client.AlarmAction("ForceAcknowledgeAllAlarms()");

            case AlarmWorkflowAction.Investigate:
                var investigateQuery = $"InvestigateAlarm({Require(request.InstanceId?.ToString(), "instance_id")},{Require(request.Id?.ToString(), "id")})";
                await client.RequireEntityType(request.Id!.Value, "id", "Alarm");
                return await client.AlarmAction(investigateQuery);

            case AlarmWorkflowAction.Forward:
                if (request.InstanceId is null)
                    throw new ArgumentException("Parameter 'instance_id' is required for this action.", "instance_id");
                if (request.RecipientIds is null or { Count: 0 })
                    throw new ArgumentException("Parameter 'recipient_ids' is required for this action.", "recipient_ids");
                foreach (var recipientId in request.RecipientIds)
                    await client.RequireEntityType(recipientId, "recipient_ids", "User", "UserGroup");
                return await client.ForwardAlarm(request.InstanceId.Value, request.RecipientIds);

            case AlarmWorkflowAction.ClearSourceCondition:
                return await client.AlarmAction($"ClearSourceCondition({Require(request.SourceEntityId?.ToString(), "source_entity_id")},{Require(request.ConditionId?.ToString(), "condition_id")})");

            case AlarmWorkflowAction.EditContext:
                var contextQuery = $"EditAlarmContext({Require(request.InstanceId?.ToString(), "instance_id")},{Require(request.Id?.ToString(), "id")},{Esc(Require(request.Context, "context"))})";
                await client.RequireEntityType(request.Id!.Value, "id", "Alarm");
                return await client.AlarmAction(contextQuery);

            default:
                throw new ArgumentException($"Unknown action: {request.Action}", "action");
        }
    }
}

public enum AlarmWorkflowAction
{
    Trigger,
    Acknowledge,
    GetActive,
    ForceAcknowledge,
    ForceAcknowledgeAll,
    Investigate,
    Forward,
    ClearSourceCondition,
    EditContext
}

public sealed record AlarmWorkflowRequest(
    [property: Description("Action to perform"), Required] AlarmWorkflowAction Action,
    [property: Description("Alarm GUID")] Guid? Id = null,
    [property: Description("Numeric alarm instance ID returned by trigger or get_active")] int? InstanceId = null,
    [property: Description("Source entity GUID")] Guid? SourceEntityId = null,
    [property: Description("Source condition ID to clear")] int? ConditionId = null,
    [Description("Context text")] string? Context = null,
    [property: Description("Recipient user GUIDs (one or more) for forward"), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? RecipientIds = null);
