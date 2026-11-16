using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Tools;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class CredentialCreateTool
{
    [McpServerTool(
        Name = "credential_create",
        Title = "Create Credential",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Create one or more credentials in Security Center. " +
        "Provide a single item for a direct create, or multiple items for a batch create.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Credential create request")] CredentialCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        ResourceLimits.EnsureBatchOperationCount(
            request.Items.Sum(item => item.AssignToCardholderId is null ? 1L : 2L), "items");
        var formats = request.Items.Select(BuildCredentialFormatString).ToList();

        if (request.Items.Count == 1)
        {
            var item = request.Items[0];
            var result = await client.CreateCredentialByPath(formats[0], EntityNameSanitizer.Sanitize(item.Name));

            if (item.AssignToCardholderId is not null)
            {
                try
                {
                    await client.RequireEntityType(item.AssignToCardholderId.Value, "assign_to_cardholder_id", "Cardholder", "Visitor");
                    var assignmentResponse = await client.AddToCollection(
                        item.AssignToCardholderId.Value.ToString(), "Credentials", result.Guid.ToString());
                    return new CredentialCreateAssignmentResult(result, assignmentResponse);
                }
                catch (Exception ex)
                {
                    return new CredentialCreateAssignmentResult(result, null, ex.Message);
                }
            }

            return result;
        }

        var results = new List<BatchCreateResultItem>(request.Items.Count);
        var successCount = 0;
        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            try
            {
                var result = await client.CreateCredentialByPath(formats[i], EntityNameSanitizer.Sanitize(item.Name));

                if (item.AssignToCardholderId is not null)
                {
                    try
                    {
                        await client.RequireEntityType(item.AssignToCardholderId.Value, "assign_to_cardholder_id", "Cardholder", "Visitor");
                        var assignmentResponse = await client.AddToCollection(
                            item.AssignToCardholderId.Value.ToString(), "Credentials", result.Guid.ToString());
                        results.Add(new BatchCreateResultItem(i, true, Guid: result.Guid, Response: assignmentResponse));
                        successCount++;
                        continue;
                    }
                    catch (Exception ex)
                    {
                        results.Add(new BatchCreateResultItem(i, true, Guid: result.Guid, Warning: $"Credential created but assignment failed: {ex.Message}"));
                        successCount++;
                        continue;
                    }
                }

                results.Add(new BatchCreateResultItem(i, true, Guid: result.Guid));
                successCount++;
            }
            catch (Exception ex)
            {
                results.Add(new BatchCreateResultItem(i, false, Error: ex.Message));
            }
        }

        return new BatchCreateResult("batch", request.Items.Count, successCount,
            request.Items.Count - successCount, results);
    }

    private static string BuildCredentialFormatString(CredentialItem item) =>
        item.FormatType switch
        {
            CredentialFormatType.Pin when item.Pin is not null =>
                $"KeypadCredentialFormat({item.Pin})",
            CredentialFormatType.Pin =>
                throw new ArgumentException("Parameter 'pin' is required for format_type 'pin'."),

            CredentialFormatType.Wiegand26Bit when item.FacilityCode is not null && item.CardNumber is not null =>
                $"WiegandStandardCredentialFormat({item.FacilityCode},{item.CardNumber})",
            CredentialFormatType.Wiegand26Bit =>
                throw new ArgumentException("Parameters 'facility_code', 'card_number' are required for format_type 'wiegand26_bit'."),

            CredentialFormatType.HidH10306 when item.FacilityCode is not null && item.CardNumber is not null =>
                $"WiegandH10306CredentialFormat({item.FacilityCode},{item.CardNumber})",
            CredentialFormatType.HidH10306 =>
                throw new ArgumentException("Parameters 'facility_code', 'card_number' are required for format_type 'hid_h10306'."),

            CredentialFormatType.HidH1030237Bit when item.CardNumber is not null =>
                $"WiegandH10302CredentialFormat({item.CardNumber})",
            CredentialFormatType.HidH1030237Bit =>
                throw new ArgumentException("Parameter 'card_number' is required for format_type 'hid_h1030237_bit'."),

            CredentialFormatType.HidH1030437Bit when item.FacilityCode is not null && item.CardNumber is not null =>
                $"WiegandH10304CredentialFormat({item.FacilityCode},{item.CardNumber})",
            CredentialFormatType.HidH1030437Bit =>
                throw new ArgumentException("Parameters 'facility_code', 'card_number' are required for format_type 'hid_h1030437_bit'."),

            CredentialFormatType.HidCorp100035Bit when item.CompanyCode is not null && item.CardNumber is not null =>
                $"WiegandCorporate1000CredentialFormat({item.CompanyCode},{item.CardNumber})",
            CredentialFormatType.HidCorp100035Bit =>
                throw new ArgumentException("Parameters 'company_code', 'card_number' are required for format_type 'hid_corp100035_bit'."),

            CredentialFormatType.HidCorp100048Bit when item.CompanyCode is not null && item.CardNumber is not null =>
                $"Wiegand48BitCorporate1000CredentialFormat({item.CompanyCode},{item.CardNumber})",
            CredentialFormatType.HidCorp100048Bit =>
                throw new ArgumentException("Parameters 'company_code', 'card_number' are required for format_type 'hid_corp100048_bit'."),

            CredentialFormatType.Csn32Bit when item.Csn is not null =>
                $"WiegandCsn32CredentialFormat({item.Csn})",
            CredentialFormatType.Csn32Bit =>
                throw new ArgumentException("Parameter 'csn' is required for format_type 'csn32_bit'."),

            CredentialFormatType.LicensePlate when item.PlateNumber is not null =>
                $"LicensePlateCredentialFormat({item.PlateNumber})",
            CredentialFormatType.LicensePlate =>
                throw new ArgumentException("Parameter 'plate_number' is required for format_type 'license_plate'."),

            CredentialFormatType.Custom when item.CustomFormatId is not null =>
                SecurityCenterRestClient.BuildCustomCredentialFormat(item.CustomFormatId.Value.ToString(), item.CustomValues),
            CredentialFormatType.Custom =>
                throw new ArgumentException("Parameter 'custom_format_id' is required for format_type 'custom'."),

            CredentialFormatType.RawCard when item.RawData is not null && item.BitLength is not null =>
                $"RawCardCredentialFormat({item.RawData},{item.BitLength})",
            CredentialFormatType.RawCard =>
                throw new ArgumentException("Parameters 'raw_data', 'bit_length' are required for format_type 'raw_card'."),

            _ => throw new ArgumentException($"Unknown format_type: '{item.FormatType}'.")
        };
}

public sealed record CredentialCreateAssignmentResult(
    EntityCreateResult Credential,
    string? AssignmentResponse = null,
    string? AssignmentError = null);

public enum CredentialFormatType
{
    Pin,
    Wiegand26Bit,
    HidH10306,
    HidH1030237Bit,
    HidH1030437Bit,
    HidCorp100035Bit,
    HidCorp100048Bit,
    Csn32Bit,
    LicensePlate,
    Custom,
    RawCard
}

public sealed record CredentialCreateRequest(
    [property: Description("One or more credentials to create"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<CredentialItem> Items);

public sealed record CredentialItem(
    [property: Description("Credential name"), Required, MinLength(1)] string Name,
    [property: Description("Credential format type"), Required] CredentialFormatType FormatType,
    [Description("PIN value (for pin)")] string? Pin = null,
    [Description("Facility code (for wiegand26_bit, hid_h10306, hid_h1030437_bit)")] int? FacilityCode = null,
    [Description("Card number (for wiegand26_bit, hid_h10306, hid_h1030237_bit, hid_h1030437_bit, hid_corp100035_bit, hid_corp100048_bit)")] int? CardNumber = null,
    [Description("Company code / site code (for hid_corp100035_bit, hid_corp100048_bit)")] int? CompanyCode = null,
    [Description("Card serial number as decimal long (for csn32_bit)")] long? Csn = null,
    [Description("License plate string (for license_plate)")] string? PlateNumber = null,
    [Description("License plate state or province (for license_plate)")] string? PlateState = null,
    [property: Description("Custom card format GUID (for custom)")] Guid? CustomFormatId = null,
    [Description("Custom credential field values keyed by field name (for custom)")] Dictionary<string, string>? CustomValues = null,
    [Description("Raw Wiegand data hex string (for raw_card)")] string? RawData = null,
    [Description("Bit length of raw data (for raw_card)")] int? BitLength = null,
    [property: Description("Cardholder GUID to assign credential to after creation")] Guid? AssignToCardholderId = null);
