using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Validation;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

public enum AlarmConfigAction
{
    GetRecipients,
    AddRecipient,
    RemoveRecipient,
    GetAttachedEntities,
    AddAttachedEntity,
    RemoveAttachedEntity,
    SetColor,
    SetProcedureUrl,
    SetRecordingTime,
    AddSchedule,
    RemoveSchedule,
    Report
}

public enum AlarmColor
{
    Black,
    Gray,
    White,
    DarkBlue,
    Blue,
    Cyan,
    DarkGreen,
    Green,
    Red,
    DarkRed,
    Orange,
    Yellow,
    Indigo
}

public sealed record AlarmConfigBatchItem(
    [property: Description("Alarm GUID"), Required] Guid Id,
    [property: Description("Action to perform"), Required] AlarmConfigAction Action,
    [property: Description("Recipient GUIDs (one or more) for add_recipient or remove_recipient"), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? RecipientIds = null,
    [property: Description("Attached entity GUID (for add_attached_entity, remove_attached_entity)")] Guid? EntityId = null,
    [property: Description("Schedule GUID (for add_schedule, remove_schedule)")] Guid? ScheduleId = null,
    [Description("AlarmColor enum value (for set_color)")] AlarmColor? Color = null,
    [Description("Procedure URL (for set_procedure_url)")] string? ProcedureUrl = null,
    [Description("Recording time in seconds (for set_recording_time)")] int? RecordingTime = null,
    [property: Description("Activity report start time (ISO 8601); provide with time_end for report"), Iso8601] string? TimeStart = null,
    [property: Description("Activity report end time (ISO 8601); provide with time_start for report"), Iso8601] string? TimeEnd = null);

public sealed record AlarmConfigRequest(
    [property: Description("Alarm configuration operations to perform"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<AlarmConfigBatchItem> Items);

[McpServerToolType]
public class AlarmConfigurationTool
{
    [McpServerTool(
        Name = "alarm_configuration",
        Title = "Alarm Configuration",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Configure alarm recipients, attached entities, color, procedure URL, recording time, " +
        "schedules, and reports. Supply a single item for single-alarm mode or multiple items for batch mode. " +
        "At most 50 total operations are allowed, including individual recipient operations. " +
        "For report, optionally provide time_start and time_end together to limit events.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Alarm configuration request")] AlarmConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        var operationCount = request.Items.Sum(item => (long)ValidateItem(item));
        ResourceLimits.EnsureBatchOperationCount(operationCount, "items");

        if (request.Items.Count == 1)
            return await ExecuteSingleAction(client, request.Items[0]);

        var results = new List<AlarmConfigResultItem>(request.Items.Count);
        int successCount = 0;

        for (int i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            try
            {
                var result = await ExecuteSingleAction(client, item);

                bool isRead = item.Action is AlarmConfigAction.GetRecipients
                    or AlarmConfigAction.GetAttachedEntities
                    or AlarmConfigAction.Report;

                if (isRead)
                    results.Add(new AlarmConfigResultItem(i, item.Id, item.Action, Data: (string)result));
                else if (result is AlarmRecipientBatchResult recipients)
                {
                    var failed = recipients.FailureCount > 0;
                    results.Add(new AlarmConfigResultItem(i, item.Id, item.Action, RecipientResults: recipients,
                        Error: failed ? true : null,
                        Message: failed ? "One or more recipient operations failed." : null));
                    if (failed) continue;
                }
                else
                    results.Add(new AlarmConfigResultItem(i, item.Id, item.Action, Response: (string)result));
                successCount++;
            }
            catch (Exception ex)
            {
                results.Add(new AlarmConfigResultItem(i, item.Id, item.Action, Error: true, Message: ex.Message));
            }
        }

        return new AlarmConfigBatchResult(request.Items.Count, successCount, request.Items.Count - successCount, results);
    }

    private static int ValidateItem(AlarmConfigBatchItem item)
    {
        switch (item.Action)
        {
            case AlarmConfigAction.AddRecipient:
            case AlarmConfigAction.RemoveRecipient:
                if (item.RecipientIds is null or { Count: 0 })
                    throw new ArgumentException("Parameter 'recipient_ids' is required for this action.", "recipient_ids");
                return item.RecipientIds.Count;
            case AlarmConfigAction.AddAttachedEntity:
            case AlarmConfigAction.RemoveAttachedEntity:
                if (item.EntityId is null)
                    throw new ArgumentException("Parameter 'entity_id' is required for this action.", "entity_id");
                break;
            case AlarmConfigAction.SetColor:
                if (item.Color is null)
                    throw new ArgumentException("Parameter 'color' is required for this action.", "color");
                break;
            case AlarmConfigAction.SetProcedureUrl:
                if (item.ProcedureUrl is null)
                    throw new ArgumentException("Parameter 'procedure_url' is required for this action.", "procedure_url");
                break;
            case AlarmConfigAction.SetRecordingTime:
                if (item.RecordingTime is null)
                    throw new ArgumentException("Parameter 'recording_time' is required for this action.", "recording_time");
                break;
            case AlarmConfigAction.AddSchedule:
            case AlarmConfigAction.RemoveSchedule:
                if (item.ScheduleId is null)
                    throw new ArgumentException("Parameter 'schedule_id' is required for this action.", "schedule_id");
                break;
            case AlarmConfigAction.Report:
                ActivityReportQuery.WithTimeRange($"Alarms@{item.Id}", item.TimeStart, item.TimeEnd, "report");
                break;
            case AlarmConfigAction.GetRecipients:
            case AlarmConfigAction.GetAttachedEntities:
                break;
            default:
                throw new ArgumentException($"Unknown action: {item.Action}", "action");
        }
        return 1;
    }

    private static async Task<object> ExecuteSingleAction(SecurityCenterRestClient client, AlarmConfigBatchItem r)
    {
        if (r.Action != AlarmConfigAction.Report)
            await client.RequireEntityType(r.Id, "items", "Alarm");
        if (r.Action is AlarmConfigAction.AddSchedule or AlarmConfigAction.RemoveSchedule)
            await client.RequireEntityType(r.ScheduleId!.Value, "schedule_id", "Schedule");
        switch (r.Action)
        {
            case AlarmConfigAction.GetRecipients:
                return await client.GetEntityField(r.Id.ToString(), "Recipients");

            case AlarmConfigAction.AddRecipient:
                if (r.RecipientIds!.Count == 1)
                {
                    await client.RequireEntityType(r.RecipientIds[0], "recipient_ids", "User", "UserGroup");
                    return await client.AddAlarmRecipient(r.Id.ToString(), r.RecipientIds[0].ToString());
                }
                return await BulkRecipientOp(client, r.Id.ToString(), r.RecipientIds, isAdd: true);

            case AlarmConfigAction.RemoveRecipient:
                if (r.RecipientIds!.Count == 1)
                {
                    await client.RequireEntityType(r.RecipientIds[0], "recipient_ids", "User", "UserGroup");
                    return await client.RemoveAlarmRecipient(r.Id.ToString(), r.RecipientIds[0].ToString());
                }
                return await BulkRecipientOp(client, r.Id.ToString(), r.RecipientIds, isAdd: false);

            case AlarmConfigAction.GetAttachedEntities:
                return await client.GetEntityField(r.Id.ToString(), "AttachedEntities");

            case AlarmConfigAction.AddAttachedEntity:
                return await client.AddAlarmAttachedEntity(r.Id, r.EntityId!.Value);

            case AlarmConfigAction.RemoveAttachedEntity:
                return await client.RemoveAlarmAttachedEntity(r.Id, r.EntityId!.Value);

            case AlarmConfigAction.SetColor:
                return await TrySetAlarmColor(client, r.Id.ToString(), r.Color!.Value);

            case AlarmConfigAction.SetProcedureUrl:
                return await client.SetEntityField(r.Id.ToString(), "AlarmProcedureUrl", r.ProcedureUrl!);

            case AlarmConfigAction.SetRecordingTime:
                return await client.SetAlarmRecordingTime(r.Id, r.RecordingTime!.Value);

            case AlarmConfigAction.AddSchedule:
                return await client.ExecuteEntityMethod(r.Id.ToString(), "AddSchedule", r.ScheduleId?.ToString());

            case AlarmConfigAction.RemoveSchedule:
                return await client.ExecuteEntityMethod(r.Id.ToString(), "RemoveSchedule", r.ScheduleId?.ToString());

            case AlarmConfigAction.Report:
                return await client.RunReport("AlarmActivity",
                    ActivityReportQuery.WithTimeRange($"Alarms@{r.Id}", r.TimeStart, r.TimeEnd, "report"));

            default:
                throw new ArgumentException($"Unknown action: {r.Action}", "action");
        }
    }

    private static async Task<object> BulkRecipientOp(
        SecurityCenterRestClient client,
        string alarmId,
        List<Guid> recipientIds,
        bool isAdd)
    {
        var tasks = recipientIds
            .Select(async rid =>
            {
                try
                {
                    await client.RequireEntityType(rid, "recipient_ids", "User", "UserGroup");
                    var response = isAdd
                        ? await client.AddAlarmRecipient(alarmId, rid.ToString())
                        : await client.RemoveAlarmRecipient(alarmId, rid.ToString());
                    return new AlarmRecipientResultItem(rid, response, false);
                }
                catch (Exception ex)
                {
                    return new AlarmRecipientResultItem(rid, null, true, ex.Message);
                }
            })
            .ToArray();

        var results = await Task.WhenAll(tasks);
        var successCount = results.Count(r => !r.Error);
        return new AlarmRecipientBatchResult(recipientIds.Count, successCount, recipientIds.Count - successCount, results.ToList());
    }

    private static async Task<object> TrySetAlarmColor(
        SecurityCenterRestClient client,
        string alarmId,
        AlarmColor color)
    {
        return await client.SetEntityField(alarmId, "AlarmColor", color.ToString());
    }
}

public sealed record AlarmConfigBatchResult(int RequestedCount, int SuccessCount, int FailureCount, List<AlarmConfigResultItem> Results);

public sealed record AlarmConfigResultItem(
    int Index,
    Guid AlarmId,
    AlarmConfigAction Action,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Data = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Response = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AlarmRecipientBatchResult? RecipientResults = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Error = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Message = null);

public sealed record AlarmRecipientBatchResult(int RequestedCount, int SuccessCount, int FailureCount, List<AlarmRecipientResultItem> Results);

public sealed record AlarmRecipientResultItem(Guid RecipientId, string? Response, bool Error, string? Message = null);
