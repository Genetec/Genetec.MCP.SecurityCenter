using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;
using static Genetec.Mcp.SecurityCenter.Client.InputValidation;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

public enum CredentialSearchAction
{
    Search26Bit,
    SearchHidH10306,
    SearchCsn,
    SearchLicensePlate,
    SearchCustom,
    FindUnassigned
}

public sealed record CredentialSearchRequest(
    [property: Description("Action to perform"), Required] CredentialSearchAction Action,
    [Description("Facility code (for search_26bit, search_hid_h10306)")] int? FacilityCode = null,
    [Description("Card number (for search_26bit, search_hid_h10306)")] int? CardNumber = null,
    [property: Description("Card serial number as a decimal 32-bit value (for search_csn)"), Range(long.MinValue, long.MaxValue)] long? Csn = null,
    [Description("License plate string (for search_license_plate)")] string? PlateNumber = null,
    [property: Description("Custom card format GUID (for search_custom)")] Guid? CustomFormatId = null,
    [Description("Custom credential field values keyed by field name (for search_custom)")] Dictionary<string, string>? CustomValues = null,
    [property: Description("Max results per page (1–100, default 50). Use small pages; ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number (1-based; not used by find_unused)"), Range(1, int.MaxValue)] int Page = 1);

[McpServerToolType]
public class CredentialSearchTool
{
    [McpServerTool(
        Name = "credential_search",
        Title = "Search Credentials",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "Search for credentials by format-specific values or assignment. " +
        "Use when looking up a credential by card number, facility code, CSN, license plate, " +
        "custom format, or finding unassigned or unused credentials. Use small pages and fetch only what is needed. Do not fetch all credentials automatically; ask the user for permission before running large queries.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Credential search request")] CredentialSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case CredentialSearchAction.Search26Bit:
                if (request.FacilityCode is null || request.CardNumber is null)
                    throw new ArgumentException("Parameters 'facility_code' and 'card_number' are required for this action.");
                return await client.RunPagedReport("CredentialConfiguration",
                    $"UniqueIds@Genetec.Sdk.Credentials.WiegandStandardCredentialFormat({request.FacilityCode},{request.CardNumber})", request.PageSize, request.Page);

            case CredentialSearchAction.SearchHidH10306:
                if (request.FacilityCode is null || request.CardNumber is null)
                    throw new ArgumentException("Parameters 'facility_code' and 'card_number' are required for this action.");
                return await client.RunPagedReport("CredentialConfiguration",
                    $"UniqueIds@Genetec.Sdk.Credentials.WiegandH10306CredentialFormat({request.FacilityCode},{request.CardNumber})", request.PageSize, request.Page);

            case CredentialSearchAction.SearchCsn:
                if (request.Csn is null)
                    throw new ArgumentException("Parameter 'csn' is required for this action.");
                return await client.RunPagedReport("CredentialConfiguration",
                    $"UniqueIds@Genetec.Sdk.Credentials.WiegandCsn32CredentialFormat({request.Csn})", request.PageSize, request.Page);

            case CredentialSearchAction.SearchLicensePlate:
                if (request.PlateNumber is null)
                    throw new ArgumentException("Parameter 'plate_number' is required for this action.");
                return await client.RunPagedReport("CredentialConfiguration",
                    $"UniqueIds@Genetec.Sdk.Credentials.LicensePlateCredentialFormat({Esc(request.PlateNumber)})", request.PageSize, request.Page);

            case CredentialSearchAction.SearchCustom:
                if (request.CustomFormatId is null)
                    throw new ArgumentException("Parameter 'custom_format_id' is required for search_custom.");
                return await client.SearchCredentialsByCustomFormat(request.CustomFormatId!.ToString(), request.CustomValues, request.PageSize, request.Page);

            case CredentialSearchAction.FindUnassigned:
                return await client.RunPagedReport("CredentialConfiguration", "UnassignedOnly=True", request.PageSize, request.Page);

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }

}
