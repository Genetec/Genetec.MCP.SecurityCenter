using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Validation;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

public enum CredentialStatusAction
{
    Activate,
    ActivateOnDate,
    ActivateDateRange,
    Deactivate,
    DeactivateOnDate,
    MarkStolen,
    MarkLost,
    SetExpirationNever,
    SetExpirationIfUnused,
    SetExpirationUponFirstUse
}

public sealed record CredentialStatusRequest(
    [property: Description("Action to perform"), Required] CredentialStatusAction Action,
    [property: Description("Credential GUIDs"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid> Items,
    [property: Description("Activation date ISO 8601 (for activate_on_date, activate_date_range)"), Iso8601] string? ActivationDate = null,
    [property: Description("Expiration date ISO 8601 (for activate_date_range, deactivate_on_date)"), Iso8601] string? ExpirationDate = null,
    [Description("Days of inactivity before expiration (for set_expiration_if_unused)")] int? UnusedDays = null,
    [Description("Days after first use before expiration (for set_expiration_upon_first_use)")] int? FirstUseDays = null);

[McpServerToolType]
public class CredentialStatusTool
{
    [McpServerTool(
        Name = "credential_status",
        Title = "Credential Status Management",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Activate, deactivate, and mark credentials as lost or stolen. " +
        "Provide a single GUID or multiple GUIDs in items; items.count == 1 runs a single call, " +
        "items.count > 1 runs a batch call in parallel.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Credential status request")] CredentialStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        string method;
        string? args;
        switch (request.Action)
        {
            case CredentialStatusAction.Activate:
                method = "Status.Activate"; args = null; break;
            case CredentialStatusAction.ActivateOnDate:
                if (request.ActivationDate is null) throw new ArgumentException("Parameter 'activation_date' is required for this action.");
                method = "Status.Activate"; args = request.ActivationDate; break;
            case CredentialStatusAction.ActivateDateRange:
                if (request.ActivationDate is null) throw new ArgumentException("Parameter 'activation_date' is required for this action.");
                if (request.ExpirationDate is null) throw new ArgumentException("Parameter 'expiration_date' is required for this action.");
                method = "Status.Activate"; args = $"{request.ActivationDate},{request.ExpirationDate}"; break;
            case CredentialStatusAction.Deactivate:
                method = "Status.Deactivate"; args = null; break;
            case CredentialStatusAction.DeactivateOnDate:
                if (request.ExpirationDate is null) throw new ArgumentException("Parameter 'expiration_date' is required for this action.");
                method = "Status.Deactivate"; args = request.ExpirationDate; break;
            case CredentialStatusAction.MarkStolen:
                method = "Status.Stolen"; args = null; break;
            case CredentialStatusAction.MarkLost:
                method = "Status.Lost"; args = null; break;
            case CredentialStatusAction.SetExpirationNever:
                method = "Status.SetExpirationToNever"; args = null; break;
            case CredentialStatusAction.SetExpirationIfUnused:
                if (request.UnusedDays is null) throw new ArgumentException("Parameter 'unused_days' is required for this action.");
                method = "Status.ExpireWhenNotUsedInDays"; args = request.UnusedDays.Value.ToString(); break;
            case CredentialStatusAction.SetExpirationUponFirstUse:
                if (request.FirstUseDays is null) throw new ArgumentException("Parameter 'first_use_days' is required for this action.");
                method = "Status.ExpireOnFirstUseInDays"; args = request.FirstUseDays.Value.ToString(); break;
            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }

        if (request.Items.Count == 1)
        {
            await client.RequireEntityType(request.Items[0], "items", "Credential");
            return await client.ExecuteEntityMethod(request.Items[0].ToString(), method, args);
        }

        return await client.ExecuteEntityMethodBatch(method,
            request.Items.Select(id => (id, args)).ToList(),
            index => client.RequireEntityType(request.Items[index], "items", "Credential"));
    }
}
