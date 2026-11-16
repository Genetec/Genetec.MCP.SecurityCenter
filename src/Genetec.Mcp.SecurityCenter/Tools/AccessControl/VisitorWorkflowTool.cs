using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Validation;
using ModelContextProtocol.Server;
using static Genetec.Mcp.SecurityCenter.Client.InputValidation;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class VisitorWorkflowTool
{
    [McpServerTool(
        Name = "visitor_workflow",
        Title = "Visitor Workflow",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Manage visitor check-in/check-out, escort assignments, search by status, and activity reports. " +
        "For activity_report, optionally provide time_start and time_end together to limit events. " +
        "Actions: check_in, check_out, assign_escorts, search_by_status, activity_report")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Visitor workflow request")] VisitorWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case VisitorWorkflowAction.CheckIn:
                if (request.Id is null)
                    throw new ArgumentException("parameter 'id' is required for check_in.");
                await client.RequireEntityType(request.Id.Value, "id", "Visitor");
                return await client.ExecuteEntityMethod(request.Id!.ToString(), "Status.Activate");

            case VisitorWorkflowAction.CheckOut:
                if (request.Id is null)
                    throw new ArgumentException("parameter 'id' is required for check_out.");
                await client.RequireEntityType(request.Id.Value, "id", "Visitor");
                return await client.ExecuteEntityMethod(request.Id!.ToString(), "Checkout");

            case VisitorWorkflowAction.AssignEscorts:
                if (request.Id is null)
                    throw new ArgumentException("parameter 'id' is required for assign_escorts.");
                if (request.EscortId is null)
                    throw new ArgumentException("parameter 'escort_id' is required for assign_escorts.");
                var escort2 = request.Escort2Id?.ToString() ?? Guid.Empty.ToString();
                await client.RequireEntityType(request.Id.Value, "id", "Visitor");
                await client.RequireEntityType(request.EscortId.Value, "escort_id", "Cardholder");
                if (request.Escort2Id is { } secondEscort && secondEscort != Guid.Empty)
                    await client.RequireEntityType(secondEscort, "escort2_id", "Cardholder");
                return await client.ExecuteEntityMethod(request.Id!.ToString(), "AssignEscorts",
                    $"{request.EscortId},{escort2}");

            case VisitorWorkflowAction.SearchByStatus:
                if (request.Status is null)
                    throw new ArgumentException("parameter 'status' is required for search_by_status.");
                return await client.RunReport("Visitor", $"VisitorStates@{Esc(request.Status.ToString())}");

            case VisitorWorkflowAction.ActivityReport:
                if (request.Id is null)
                    throw new ArgumentException("parameter 'id' is required for activity_report.");
                return await ActivityReport(client, request.Id!.ToString(), request.TimeStart, request.TimeEnd);

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }

    private static async Task<string> ActivityReport(SecurityCenterRestClient client, string id, string? timeStart, string? timeEnd)
    {
        var query = ActivityReportQuery.WithTimeRange($"Visitor={id},Events*", timeStart, timeEnd);
        return await client.RunReport("VisitorActivity", query);
    }
}

public enum VisitorWorkflowAction
{
    CheckIn,
    CheckOut,
    AssignEscorts,
    SearchByStatus,
    ActivityReport
}

public enum VisitorState { Active, Archived }

public sealed record VisitorWorkflowRequest(
    [property: Description("Action to perform"), Required] VisitorWorkflowAction Action,
    [property: Description("Visitor GUID")] Guid? Id = null,
    [Description("Visitor state filter (active or archived)")] VisitorState? Status = null,
    [property: Description("Time range start (ISO 8601)"), Iso8601] string? TimeStart = null,
    [property: Description("Time range end (ISO 8601)"), Iso8601] string? TimeEnd = null,
    [property: Description("Primary escort cardholder GUID")] Guid? EscortId = null,
    [property: Description("Optional second escort cardholder GUID")] Guid? Escort2Id = null);
