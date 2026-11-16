using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Validation;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class CredentialOperationsTool
{
    [McpServerTool(
        Name = "credential_operations",
        Title = "Credential Operations",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Manage credential assignments and reporting. " +
        "Use when assigning credentials to cardholders or running activity reports. " +
        "For activity_report, optionally provide time_start and time_end together to limit events. " +
        "Actions: assign_to_cardholder, remove_from_cardholder, activity_report")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Credential operations request")] CredentialOperationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case CredentialOperationsAction.AssignToCardholder:
                if (request.CardholderId is null) throw new ArgumentException("Parameter 'cardholder_id' is required for assign_to_cardholder.");
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for assign_to_cardholder.");
                await client.RequireEntityType(request.Id.Value, "id", "Credential");
                await client.RequireEntityType(request.CardholderId.Value, "cardholder_id", "Cardholder", "Visitor");
                return await client.AddToCollection(request.CardholderId!.ToString(), "Credentials", request.Id!.ToString());

            case CredentialOperationsAction.RemoveFromCardholder:
                if (request.CardholderId is null) throw new ArgumentException("Parameter 'cardholder_id' is required for remove_from_cardholder.");
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for remove_from_cardholder.");
                await client.RequireEntityType(request.Id.Value, "id", "Credential");
                await client.RequireEntityType(request.CardholderId.Value, "cardholder_id", "Cardholder", "Visitor");
                return await client.RemoveFromCollection(request.CardholderId!.ToString(), "Credentials", request.Id!.ToString());

            case CredentialOperationsAction.ActivityReport:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for activity_report.");
                return await client.RunReport("CredentialActivity",
                    ActivityReportQuery.WithTimeRange($"Credentials@{request.Id}", request.TimeStart, request.TimeEnd));

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum CredentialOperationsAction
{
    AssignToCardholder,
    RemoveFromCardholder,
    ActivityReport
}

public sealed record CredentialOperationsRequest(
    [property: Description("Action to perform"), Required] CredentialOperationsAction Action,
    [property: Description("Credential GUID")] Guid? Id = null,
    [property: Description("Cardholder GUID")] Guid? CardholderId = null,
    [property: Description("Activity report start time (ISO 8601); provide with time_end"), Iso8601] string? TimeStart = null,
    [property: Description("Activity report end time (ISO 8601); provide with time_start"), Iso8601] string? TimeEnd = null);
