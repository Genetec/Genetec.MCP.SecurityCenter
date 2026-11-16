using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class CardholderCreateTool
{
    [McpServerTool(
        Name = "cardholder_create",
        Title = "Create Cardholder",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Create one or more cardholders in Security Center. " +
        "Provide a single item for a direct create, or multiple items for a batch create.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Cardholder create request")] CardholderCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        if (request.Items.Count == 1)
        {
            var item = request.Items[0];
            return await client.CreateEntity<EntityCreateResult>(
                "Cardholder",
                EntityNameSanitizer.Sanitize(item.Name),
                BuildBody(item));
        }

        var batchItems = request.Items
            .Select(item => new SecurityCenterRestClient.BatchCreateItem(
                EntityNameSanitizer.Sanitize(item.Name),
                BuildBody(item)))
            .ToList();

        var raw = await client.CreateEntitiesBatch("Cardholder", batchItems);
        return CreateToolHelper.BuildBatchResponse(raw, batchItems.Count);
    }

    private static object? BuildBody(CardholderItem item) =>
        item.FirstName is null && item.LastName is null && item.Email is null
            ? null
            : new { item.FirstName, item.LastName, EmailAddress = item.Email };
}

public sealed record CardholderCreateRequest(
    [property: Description("One or more cardholders to create"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<CardholderItem> Items);

public sealed record CardholderItem(
    [property: Description("Cardholder name"), Required, MinLength(1)] string Name,
    [Description("First name")] string? FirstName = null,
    [Description("Last name")] string? LastName = null,
    [Description("Email address")] string? Email = null);
