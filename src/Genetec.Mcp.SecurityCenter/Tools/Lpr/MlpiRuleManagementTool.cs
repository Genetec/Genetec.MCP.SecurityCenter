using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Tools;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Lpr;

[McpServerToolType]
public class MlpiRuleManagementTool
{
    [McpServerTool(
        Name = "mlpi_rule_management",
        Title = "MLPI Rule Management",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true),
     Description("Manage MLPI rules: get, list, update, and get_locations. MLPI locations are read-only through the WebSDK.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("MLPI rule management request")] MlpiRuleManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        switch (request.Action)
        {
            case MlpiRuleAction.List:
                return await client.ListEntities("LprMlpiRule", request.PageSize, request.Page);

            case MlpiRuleAction.Get:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for get.");
                return await client.GetEntityTrimmed(request.Id.Value, "LprMlpiRule");

            case MlpiRuleAction.Update:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                return await ManagementToolHelper.Update<MlpiRuleUpdateProperties>(client, items, "LprMlpiRule",
                    body => body with { Name = ManagementToolHelper.SanitizeUpdateName(body.Name) },
                    name => new MlpiRuleUpdateProperties(Name: name), validateItem: async index =>
                    {
                        if (items[index].Body?.LprManager is { } managerId)
                            await client.RequireEntityType(managerId, "items.body.lpr_manager", "Role");
                    });
            }

            case MlpiRuleAction.GetLocations:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for get_locations.");
                await client.RequireEntityType(request.Id.Value, "id", "LprMlpiRule");
                return await client.GetEntityField(request.Id!.ToString(), "Locations");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }

}

public enum MlpiRuleAction { Get, List, Update, GetLocations }

public sealed record MlpiRuleManagementItem(
    [property: Description("LprMlpiRule name for rename"), MinLength(1)] string? Name = null,
    [property: Description("LprMlpiRule GUID for update")] Guid? Id = null,
    [property: Description("Typed LprMlpiRule properties for update")] MlpiRuleUpdateProperties? Body = null) : IManagementCrudItem<MlpiRuleUpdateProperties>;

public sealed record MlpiRuleManagementRequest(
    [property: Description("Action to perform"), Required] MlpiRuleAction Action,
    [property: Description("MLPI rule GUID for get or get_locations")] Guid? Id = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1,
    [property: Description("One or more items to update"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<MlpiRuleManagementItem>? Items = null);

public sealed record MlpiRuleUpdateProperties(
    [Description("Entity name")] string? Name = null,
    [Description("LPR Manager role GUID")] Guid? LprManager = null);
