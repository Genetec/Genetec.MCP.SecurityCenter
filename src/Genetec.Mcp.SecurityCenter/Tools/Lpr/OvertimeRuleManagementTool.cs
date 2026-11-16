using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Tools;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Lpr;

[McpServerToolType]
public class OvertimeRuleManagementTool
{
    [McpServerTool(
        Name = "overtime_rule_management",
        Title = "Overtime Rule Management",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true),
     Description("Manage overtime rules: get, list, and update.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Overtime rule management request")] OvertimeRuleManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        switch (request.Action)
        {
            case OvertimeRuleAction.List:
                return await client.ListEntities("OvertimeRule", request.PageSize, request.Page);

            case OvertimeRuleAction.Get:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for get.");
                return await client.GetEntityTrimmed(request.Id.Value, "OvertimeRule");

            case OvertimeRuleAction.Update:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                return await ManagementToolHelper.Update<OvertimeRuleUpdateProperties>(client, items, "OvertimeRule",
                    body => body with { Name = ManagementToolHelper.SanitizeUpdateName(body.Name) },
                    name => new OvertimeRuleUpdateProperties(Name: name));
            }

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }

}

public enum OvertimeRuleAction { Get, List, Update }

public sealed record OvertimeRuleManagementItem(
    [property: Description("OvertimeRule name for rename"), MinLength(1)] string? Name = null,
    [property: Description("OvertimeRule GUID for update")] Guid? Id = null,
    [property: Description("Typed OvertimeRule properties for update")] OvertimeRuleUpdateProperties? Body = null) : IManagementCrudItem<OvertimeRuleUpdateProperties>;

public sealed record OvertimeRuleManagementRequest(
    [property: Description("Action to perform"), Required] OvertimeRuleAction Action,
    [property: Description("Overtime rule GUID for get")] Guid? Id = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1,
    [property: Description("One or more items to update"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<OvertimeRuleManagementItem>? Items = null);

public sealed record OvertimeRuleUpdateProperties(
    [Description("Entity name")] string? Name = null,
    [Description("Vehicle position")] string? CarPosition = null,
    [Description("Rule color")] string? Color = null,
    [Description("Enforcement type")] string? EnforcementType = null,
    [Description("Number of days")] int? NumberOfDays = null,
    [Description("Overtime type")] string? OvertimeType = null);
