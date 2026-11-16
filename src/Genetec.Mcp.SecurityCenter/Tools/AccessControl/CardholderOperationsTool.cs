using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Validation;
using ModelContextProtocol.Server;
using static Genetec.Mcp.SecurityCenter.Client.InputValidation;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class CardholderOperationsTool
{
    [McpServerTool(
        Name = "cardholder_operations",
        Title = "Cardholder Operations",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Manage cardholder credentials, pictures, search, and reporting. " +
        "Use when assigning credentials, viewing pictures, searching by status or expiration, " +
        "or running activity reports. " +
        "For activity_report, optionally provide time_start and time_end together to limit events. " +
        "Use small result limits; do not fetch all entities automatically. Ask the user before running large queries. " +
        "Actions: get_credentials, add_credential, remove_credential, get_picture, " +
        "search_by_status, search_by_expiration, activity_report")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Cardholder operations request")] CardholderOperationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        async Task<BatchCreateResult> BatchCredentialOp(bool add)
        {
            ResourceLimits.EnsureBatchOperationCount(request.CredentialIds!.Count, "credential_ids");
            var tasks = request.CredentialIds!
                .Select(async (cid, idx) =>
                {
                    try
                    {
                        await client.RequireEntityType(cid, "credential_ids", "Credential");
                        var response = add
                            ? client.AddToCollection(request.Id!.ToString(), "Credentials", cid.ToString())
                            : client.RemoveFromCollection(request.Id!.ToString(), "Credentials", cid.ToString());
                        return new BatchCreateResultItem(idx, true, Guid: cid, Response: await response);
                    }
                    catch (Exception ex)
                    {
                        return new BatchCreateResultItem(idx, false, Guid: cid, Error: ex.Message);
                    }
                })
                .ToArray();

            var results = (await Task.WhenAll(tasks)).ToList();
            var successCount = results.Count(r => r.Success);
            return new BatchCreateResult("batch", request.CredentialIds.Count, successCount,
                request.CredentialIds.Count - successCount, results);
        }

        switch (request.Action)
        {
            case CardholderOperationsAction.GetCredentials:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for get_credentials.");
                await client.RequireEntityType(request.Id.Value, "id", "Cardholder");
                return await client.GetEntityField(request.Id!.ToString(), "Credentials");

            case CardholderOperationsAction.AddCredential:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for add_credential.");
                if (request.CredentialIds is null || request.CredentialIds.Count == 0)
                    throw new ArgumentException("Parameter 'credential_ids' is required for add_credential.");
                await client.RequireEntityType(request.Id.Value, "id", "Cardholder");
                if (request.CredentialIds.Count == 1)
                {
                    await client.RequireEntityType(request.CredentialIds[0], "credential_ids", "Credential");
                    return await client.AddToCollection(request.Id!.ToString(), "Credentials", request.CredentialIds[0].ToString());
                }
                return await BatchCredentialOp(add: true);

            case CardholderOperationsAction.RemoveCredential:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for remove_credential.");
                if (request.CredentialIds is null || request.CredentialIds.Count == 0)
                    throw new ArgumentException("Parameter 'credential_ids' is required for remove_credential.");
                await client.RequireEntityType(request.Id.Value, "id", "Cardholder");
                if (request.CredentialIds.Count == 1)
                {
                    await client.RequireEntityType(request.CredentialIds[0], "credential_ids", "Credential");
                    return await client.RemoveFromCollection(request.Id!.ToString(), "Credentials", request.CredentialIds[0].ToString());
                }
                return await BatchCredentialOp(add: false);

            case CardholderOperationsAction.GetPicture:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for get_picture.");
                await client.RequireEntityType(request.Id.Value, "id", "Cardholder");
                return await client.GetEntityField(request.Id!.ToString(), "Picture");

            case CardholderOperationsAction.SearchByStatus:
                if (request.Status is null)
                    throw new ArgumentException("Parameter 'status' is required for search_by_status.");
                return await client.RunPagedReport("CardholderConfiguration", $"AccessStatus@{Esc(request.Status.ToString())}", request.PageSize, request.Page);

            case CardholderOperationsAction.SearchByExpiration:
            {
                var parts = new List<string>();
                if (request.Status is not null) parts.Add($"AccessStatus@{Esc(request.Status.ToString())}");
                if (request.ExpirationStart is not null && request.ExpirationEnd is not null)
                    parts.Add($"ExpirationTimeRange.SetTimeRange({Esc(request.ExpirationStart)},{Esc(request.ExpirationEnd)})");
                else if (request.ExpirationStart is not null)
                    parts.Add($"ExpirationTimeRange.SetTimeRange({Esc(request.ExpirationStart)})");
                return await client.RunPagedReport("CardholderConfiguration", string.Join(",", parts), request.PageSize, request.Page);
            }

            case CardholderOperationsAction.ActivityReport:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for activity_report.");
                return await client.RunReport("CardholderActivity",
                    ActivityReportQuery.WithTimeRange($"Cardholders@{request.Id}", request.TimeStart, request.TimeEnd));

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum CardholderOperationsAction
{
    GetCredentials,
    AddCredential,
    RemoveCredential,
    GetPicture,
    SearchByStatus,
    SearchByExpiration,
    ActivityReport
}

public sealed record CardholderOperationsRequest(
    [property: Description("Action to perform"), Required] CardholderOperationsAction Action,
    [property: Description("Cardholder GUID")] Guid? Id = null,
    [property: Description("Credential GUIDs — one or more for add_credential or remove_credential"), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? CredentialIds = null,
    [Description("Access status filter")] AccessStatus? Status = null,
    [property: Description("Expiration start (ISO 8601)"), Iso8601] string? ExpirationStart = null,
    [property: Description("Expiration end (ISO 8601)"), Iso8601] string? ExpirationEnd = null,
    [property: Description("Activity report start time (ISO 8601); provide with time_end"), Iso8601] string? TimeStart = null,
    [property: Description("Activity report end time (ISO 8601); provide with time_start"), Iso8601] string? TimeEnd = null,
    [property: Description("Maximum search results per page (default 50, range 1-100); use small pages and ask the user before large queries"), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Search page number (1-based)"), Range(1, int.MaxValue)] int Page = 1);
