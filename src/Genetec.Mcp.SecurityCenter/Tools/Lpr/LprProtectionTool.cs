using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Validation;
using ModelContextProtocol.Server;
using static Genetec.Mcp.SecurityCenter.Client.InputValidation;

namespace Genetec.Mcp.SecurityCenter.Tools.Lpr;

[McpServerToolType]
public class LprProtectionTool
{
    [McpServerTool(
        Name = "lpr_protection",
        Title = "LPR Data Protection",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description("Protect or unprotect LPR hits and reads to prevent automatic deletion. " +
                 "Actions: protect_hit, unprotect_hit require hit_id, source_id, and hit_timestamp_utc. " +
                 "protect_read, unprotect_read require read_id, source_id, and read_timestamp_utc.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("LPR protection request")] LprProtectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        switch (request.Action)
        {
            case LprProtectionAction.ProtectHit:
                if (request.HitId is null) throw new ArgumentException("Parameter 'hit_id' is required for this action.");
                if (request.SourceId is null) throw new ArgumentException("Parameter 'source_id' is required for this action.");
                if (request.HitTimestampUtc is null) throw new ArgumentException("Parameter 'hit_timestamp_utc' is required for this action.");
                return await client.ExecuteAction(
                    $"ProtectHit({request.HitId},{request.SourceId},{Esc(request.HitTimestampUtc)})");

            case LprProtectionAction.UnprotectHit:
                if (request.HitId is null) throw new ArgumentException("Parameter 'hit_id' is required for this action.");
                if (request.SourceId is null) throw new ArgumentException("Parameter 'source_id' is required for this action.");
                if (request.HitTimestampUtc is null) throw new ArgumentException("Parameter 'hit_timestamp_utc' is required for this action.");
                return await client.ExecuteAction(
                    $"UnprotectHit({request.HitId},{request.SourceId},{Esc(request.HitTimestampUtc)})");

            case LprProtectionAction.ProtectRead:
                if (request.ReadId is null) throw new ArgumentException("Parameter 'read_id' is required for this action.");
                if (request.SourceId is null) throw new ArgumentException("Parameter 'source_id' is required for this action.");
                if (request.ReadTimestampUtc is null) throw new ArgumentException("Parameter 'read_timestamp_utc' is required for this action.");
                return await client.ExecuteAction(
                    $"ProtectRead({request.ReadId},{request.SourceId},{Esc(request.ReadTimestampUtc)})");

            case LprProtectionAction.UnprotectRead:
                if (request.ReadId is null) throw new ArgumentException("Parameter 'read_id' is required for this action.");
                if (request.SourceId is null) throw new ArgumentException("Parameter 'source_id' is required for this action.");
                if (request.ReadTimestampUtc is null) throw new ArgumentException("Parameter 'read_timestamp_utc' is required for this action.");
                return await client.ExecuteAction(
                    $"UnprotectRead({request.ReadId},{request.SourceId},{Esc(request.ReadTimestampUtc)})");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum LprProtectionAction { ProtectHit, UnprotectHit, ProtectRead, UnprotectRead }

public sealed record LprProtectionRequest(
    [property: Description("Action to perform"), Required] LprProtectionAction Action,
    [property: Description("LPR hit GUID (required for protect_hit, unprotect_hit)")] Guid? HitId = null,
    [property: Description("LPR read GUID (required for protect_read, unprotect_read)")] Guid? ReadId = null,
    [property: Description("LPR source GUID (required for all actions)")] Guid? SourceId = null,
    [property: Description("Hit timestamp in UTC, ISO 8601 (required for protect_hit, unprotect_hit)"), Iso8601] string? HitTimestampUtc = null,
    [property: Description("Read timestamp in UTC, ISO 8601 (required for protect_read, unprotect_read)"), Iso8601] string? ReadTimestampUtc = null);
