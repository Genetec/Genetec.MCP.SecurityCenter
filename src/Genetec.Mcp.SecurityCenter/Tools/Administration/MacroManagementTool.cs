using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Tools;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class MacroManagementTool
{
    [McpServerTool(
        Name = "macro_management",
        Title = "Macro Management",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true),
     Description("Manage macros: get, list, create, update, delete, execute. " +
                "" +
                "Use items for CRUD: one item is single, multiple items are batch.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Macro management request")] MacroManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case MacroAction.List:
                return await client.ListEntities("Macro", request.PageSize, request.Page);

            case MacroAction.Get:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'get'.", "id");
                return await client.GetEntityTrimmed(request.Id.Value, "Macro");

            case MacroAction.Create:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                var names = ManagementBatchItemHelper.GetCreateNames(items);
                if (items.Any(item => item.Body is not null))
                    throw new ArgumentException("Parameter 'items.body' is only valid for update.", "items.body");
                var creates = items.Select((item, index) => new SecurityCenterRestClient.BatchCreateItem(
                    EntityNameSanitizer.Sanitize(names[index]))).ToList();
                return await CreateToolHelper.Execute(client, "Macro", creates);
            }

            case MacroAction.Update:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                return await ManagementToolHelper.Update<MacroUpdateProperties>(client, items, "Macro",
                    body => body with { Name = ManagementToolHelper.SanitizeUpdateName(body.Name) },
                    name => new MacroUpdateProperties(Name: name));
            }

            case MacroAction.Delete:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                return await DeleteToolHelper.Execute(client, ManagementToolHelper.DeleteIds<MacroUpdateProperties>(items), "Macro");
            }

            case MacroAction.Execute:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'execute'.", "id");
                await client.RequireEntityType(request.Id.Value, "id", "Macro");
                return await client.ExecuteEntityMethod(request.Id!.ToString(), "Execute");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}", "action");
        }
    }


}

public enum MacroAction
{
    Get,
    List,
    Create,
    Update,
    Delete,
    Execute
}

public sealed record MacroManagementItem(
    [property: Description("Macro name for create or rename"), MinLength(1)] string? Name = null,
    [property: Description("Macro GUID for update or delete")] Guid? Id = null,
    [property: Description("Typed Macro properties for update")] MacroUpdateProperties? Body = null) : IManagementCrudItem<MacroUpdateProperties>;

public sealed record MacroManagementRequest(
    [property: Description("Action to perform"), Required] MacroAction Action,
    [property: Description("Macro GUID for get or execute")] Guid? Id = null,
    [property: Description("One or more items for create, update, or delete"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<MacroManagementItem>? Items = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1);

public sealed record MacroUpdateProperties(
    [Description("Entity name")] string? Name = null,
    [Description("Macro source code")] string? Code = null,
    [Description("Macro language")] string? Language = null,
    [Description("CodeDom language")] string? CodeDomLanguage = null,
    [Description("Execution integrity mode")] string? ExecutionIntegrity = null,
    [Description("Macro type")] string? MacroType = null,
    [Description("Macro XML metadata")] string? XmlInfo = null,
    [Description("Whether the macro is a draft")] bool? IsDraft = null,
    [Description("Macro loading strategy")] string? LoadingStrategy = null,
    [Description("Whether only one instance may run")] bool? MustBeSingleInstance = null);
