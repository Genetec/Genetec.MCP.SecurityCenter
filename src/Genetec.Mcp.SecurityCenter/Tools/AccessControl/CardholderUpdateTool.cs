using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class CardholderUpdateTool
{
    [McpServerTool(
        Name = "cardholder_update",
        Title = "Update Cardholder",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "Update one or more cardholders in Security Center. " +
        "Provide a single item to update one cardholder, or multiple items for a batch update.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Cardholder update request")] CardholderUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        if (request.Items.Count == 1)
        {
            var item = request.Items[0];
            await client.RequireEntityType(item.Id, "items", "Cardholder");
            return await client.UpdateEntity(item.Id.ToString(), item);
        }

        var pairs = request.Items
            .Select(i => (i.Id, (object)i))
            .ToList();
        return await client.UpdateEntitiesBatch(pairs, "Cardholder");
    }
}

public sealed record CardholderUpdateRequest(
    [property: Description("One or more cardholders to update"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<CardholderUpdateItem> Items);

public sealed record CardholderUpdateItem(
    [property: Description("Cardholder GUID"), Required] Guid Id,
    [Description("Cardholder name")] string? Name = null,
    [Description("First name")] string? FirstName = null,
    [Description("Last name")] string? LastName = null,
    [Description("Email address")] string? EmailAddress = null);
