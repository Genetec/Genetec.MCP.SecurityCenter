using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class CardholderGetTool
{
    public sealed record CardholderGetRequest(
        [property: Description("Cardholder GUID"), Required] Guid Id);

    [McpServerTool(
        Name = "cardholder_get",
        Title = "Get Cardholder",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "Retrieve full details of a single cardholder in Security Center by GUID, " +
        "including name, credentials, and configuration.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Cardholder get request")] CardholderGetRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await client.GetEntityTrimmed(request.Id, "Cardholder");
    }
}
