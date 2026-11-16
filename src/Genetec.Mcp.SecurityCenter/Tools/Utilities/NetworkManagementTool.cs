using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Utilities;

[McpServerToolType]
public class NetworkManagementTool
{
    [McpServerTool(
        Name = "network_management",
        Title = "Network Management",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description("Manage networks: get, list, get_endpoints.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Network management request")] NetworkManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case NetworkAction.List:
                return await client.ListEntities("Network", request.PageSize, request.Page);

            case NetworkAction.Get:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'get'.");
                return await client.GetEntity(request.Id.Value, "Network");

            case NetworkAction.GetEndpoints:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'get_endpoints'.");
                await client.RequireEntityType(request.Id.Value, "id", "Network");
                return await client.GetEntityField(request.Id!.ToString(), "Servers");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum NetworkAction { List, Get, GetEndpoints }

public sealed record NetworkManagementRequest(
    [property: Description("Action to perform"), Required] NetworkAction Action,
    [property: Description("Network GUID")] Guid? Id = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1);
