using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class VisitorCredentialsTool
{
    [McpServerTool(
        Name = "visitor_credentials",
        Title = "Visitor Credentials and Picture",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Manage visitor credentials and pictures. " +
        "Use when assigning credentials or viewing visitor photos. " +
        "Actions: get_credentials, add_credential, remove_credential, get_picture")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Visitor credentials request")] VisitorCredentialsRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case VisitorCredentialsAction.GetCredentials:
                await client.RequireEntityType(request.Id, "id", "Visitor");
                return await client.GetEntityField(request.Id.ToString(), "Credentials");

            case VisitorCredentialsAction.AddCredential:
                if (request.CredentialId == null)
                    throw new ArgumentException("Parameter 'credential_id' is required for add_credential.");
                await client.RequireEntityType(request.Id, "id", "Visitor");
                await client.RequireEntityType(request.CredentialId.Value, "credential_id", "Credential");
                return await client.AddToCollection(request.Id.ToString(), "Credentials", request.CredentialId!.ToString());

            case VisitorCredentialsAction.RemoveCredential:
                if (request.CredentialId == null)
                    throw new ArgumentException("Parameter 'credential_id' is required for remove_credential.");
                await client.RequireEntityType(request.Id, "id", "Visitor");
                await client.RequireEntityType(request.CredentialId.Value, "credential_id", "Credential");
                return await client.RemoveFromCollection(request.Id.ToString(), "Credentials", request.CredentialId!.ToString());

            case VisitorCredentialsAction.GetPicture:
                await client.RequireEntityType(request.Id, "id", "Visitor");
                return await client.GetEntityField(request.Id.ToString(), "Picture");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum VisitorCredentialsAction
{
    GetCredentials,
    AddCredential,
    RemoveCredential,
    GetPicture
}

public sealed record VisitorCredentialsRequest(
    [property: Description("Action to perform"), Required] VisitorCredentialsAction Action,
    [property: Description("Visitor GUID"), Required] Guid Id,
    [property: Description("Credential GUID")] Guid? CredentialId = null);
