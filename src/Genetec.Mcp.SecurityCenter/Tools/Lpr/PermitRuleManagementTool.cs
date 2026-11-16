using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Lpr;

[McpServerToolType]
public class PermitRuleManagementTool
{
    [McpServerTool(
        Name = "permit_rule_management",
        Title = "Permit Rule Management",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true),
     Description("Manage permit rules: get, list, and update. " +
                 "The Security Center Web SDK does not support creating or deleting permit-rule entities.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Permit rule management request")] PermitRuleManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        switch (request.Action)
        {
            case PermitRuleAction.List:
                return await client.ListEntities("PermitRule", request.PageSize, request.Page);

            case PermitRuleAction.Get:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for get.");
                return await client.GetEntityTrimmed(request.Id.Value, "PermitRule");

            case PermitRuleAction.Update:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                return await ManagementToolHelper.Update<PermitRuleUpdateProperties>(client, items, "PermitRule",
                    body => body with { Name = ManagementToolHelper.SanitizeUpdateName(body.Name) },
                    name => new PermitRuleUpdateProperties(Name: name));
            }

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum PermitRuleAction { Get, List, Update }

public sealed record PermitRuleManagementItem(
    [property: Description("PermitRule name for rename"), MinLength(1)] string? Name = null,
    [property: Description("PermitRule GUID for update")] Guid? Id = null,
    [property: Description("Typed PermitRule properties for update")] PermitRuleUpdateProperties? Body = null) : IManagementCrudItem<PermitRuleUpdateProperties>;

public sealed record PermitRuleManagementRequest(
    [property: Description("Action to perform"), Required] PermitRuleAction Action,
    [property: Description("Permit rule GUID for get")] Guid? Id = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1,
    [property: Description("One or more items to update"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<PermitRuleManagementItem>? Items = null);

public sealed record PermitRuleUpdateProperties(
    [Description("Entity name")] string? Name = null,
    [Description("Permit rule color")] string? Color = null);
