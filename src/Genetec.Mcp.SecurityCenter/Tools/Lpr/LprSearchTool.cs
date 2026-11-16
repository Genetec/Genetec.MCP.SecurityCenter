using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Validation;
using ModelContextProtocol.Server;
using static Genetec.Mcp.SecurityCenter.Client.InputValidation;

namespace Genetec.Mcp.SecurityCenter.Tools.Lpr;

[McpServerToolType]
public class LprSearchTool
{
    [McpServerTool(
        Name = "lpr_search",
        Title = "LPR Search Reads",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description("Search LPR license plate reads in Security Center by plate number, ALPR sources, and time range. Source selection is required to route the report. Returns at most 100 rows per call. The Web SDK has no page number or continuation token for this report. For more results, split the date range into smaller windows and deduplicate by ReadId because date bounds are inclusive. A window reaching maximum_result_count may be truncated; narrow it further. A date cursor cannot guarantee all reads when too many share the same timestamp.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("LPR search request")] LprSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await client.RunReport("LprRead", BuildQuery(
            QueryCollection("SourceIds", request.SourceIds),
            QueryCollection("Plates", new[] { request.PlateNumber }),
            QueryTimeRange(request.StartDate, request.EndDate),
            QueryValue("MaximumResultCount", request.MaximumResultCount)));
    }
}

public sealed record LprSearchRequest(
    [property: Description("License plate to search"), Required, MinLength(1)] string PlateNumber,
    [property: Description("Inclusive start date (ISO 8601 UTC)"), Required, MinLength(1), Iso8601] string StartDate,
    [property: Description("Inclusive end date (ISO 8601 UTC)"), Required, MinLength(1), Iso8601] string EndDate,
    [property: Description("ALPR unit, patroller, or ALPR device GUIDs to query; required to route the report"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid> SourceIds,
    [property: Description("Maximum report rows per call (1-100, default 50). If the limit is reached, narrow the date range to retrieve more results"), Range(1, 100)] int MaximumResultCount = 50);
