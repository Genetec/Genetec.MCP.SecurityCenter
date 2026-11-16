using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Validation;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class ActivityTrailTool
{
    [McpServerTool(
        Name = "activity_trail",
        Title = "Activity Trail",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description("Retrieve activity trails report: shows all activities in the system for a given time range, optionally filtered by instigator")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Activity trail request")] ActivityTrailRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        var query = $"TimeRange.SetTimeRange({request.TimeStart},{request.TimeEnd})";
        if (request.InstigatorId.HasValue)
            query += $",InitiatorEntities@{request.InstigatorId}";
        return await client.RunReport("ActivityTrailsReport", query);
    }
}

public sealed record ActivityTrailRequest(
    [property: Description("Time range start (ISO 8601)"), Required, MinLength(1), Iso8601] string TimeStart,
    [property: Description("Time range end (ISO 8601)"), Required, MinLength(1), Iso8601] string TimeEnd,
    [property: Description("Instigator entity GUID (optional)")] Guid? InstigatorId = null);
