using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Genetec.Mcp.SecurityCenter.Client;

namespace Genetec.Mcp.SecurityCenter.Tools;

internal static class ActivityReportQuery
{
    public static string? CustomFields(IEnumerable<ReportCustomFieldFilter>? filters)
    {
        if (filters is null)
            return null;

        var values = filters.Select(filter =>
        {
            var value = filter.TextValue
                ?? filter.NumericValue?.ToString(CultureInfo.InvariantCulture)
                ?? filter.DecimalValue?.ToString(CultureInfo.InvariantCulture)
                ?? filter.BooleanValue?.ToString()
                ?? filter.DateValue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                ?? filter.EntityValue?.ToString();
            return value is null
                ? $"CustomField({filter.FieldId})"
                : $"CustomField({filter.FieldId},{InputValidation.Esc(value)})";
        }).ToArray();
        return values.Length == 0 ? null : $"CustomFields@{string.Join("@", values)}";
    }

    public static string WithTimeRange(
        string query,
        string? timeStart,
        string? timeEnd,
        string action = "activity_report")
    {
        if (timeStart is null && timeEnd is null)
            return query;

        if (timeStart is null || timeEnd is null)
            throw new ArgumentException($"Parameters 'time_start' and 'time_end' must be provided together for {action}.");

        return InputValidation.BuildQuery(query, InputValidation.QueryTimeRange(timeStart, timeEnd));
    }
}

public enum ReportSortOrder { Ascending, Descending }

public enum ReportStringSearchMode
{
    StartsWith,
    Contains,
    EndsWith,
    Is,
    DoesNotStartWith,
    DoesNotEndWith,
    DoesNotContain,
    IsNot
}

public sealed record ReportCustomFieldFilter(
    [property: Description("Custom field GUID"), Required] Guid FieldId,
    [Description("Text or enumerated field value")] string? TextValue = null,
    [Description("Integer field value")] int? NumericValue = null,
    [Description("Decimal field value")] double? DecimalValue = null,
    [Description("Boolean field value")] bool? BooleanValue = null,
    [Description("Date field value; DateTime custom fields are unsupported by the Web SDK")] DateOnly? DateValue = null,
    [Description("Entity field GUID value")] Guid? EntityValue = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var supplied = new[]
        {
            TextValue is not null, NumericValue.HasValue, DecimalValue.HasValue,
            BooleanValue.HasValue, DateValue.HasValue, EntityValue.HasValue
        };
        if (supplied.Count(value => value) > 1)
            yield return new ValidationResult(
                "Supply only one custom field value; omit all value fields to use the configured default.");
    }
}


public enum ReportEntityType
{
    None,
    AccessPoint,
    AccessRule,
    Alarm,
    Application,
    Area,
    Camera,
    Cardholder,
    CardholderGroup,
    Credential,
    Device,
    Door,
    Elevator,
    Role,
    OutputBehavior,
    Partition,
    SystemConfiguration,
    Schedule,
    Unit,
    User,
    UserGroup,
    Visitor,
    Zone,
    Macro,
    CashRegister,
    ReportTemplate,
    LprUnit,
    Patroller,
    Permit,
    HotlistRule,
    OvertimeRule,
    PermitRule,
    SharedPermitRule,
    UserTask,
    IntrusionArea,
    IntrusionUnit,
    Server,
    Asset,
    VideoUnit,
    File,
    Monitor,
    ThreatLevel,
    TransferGroup,
    Stream,
    Network,
    Agent,
    Route,
    Badge,
    Sequence,
    TilePlugin,
    LprMlpiRule,
    ScheduledTask,
    AnalogMonitor,
    AnalogMonitorGroup,
    Map,
    ParkingZone,
    ParkingRule,
    CustomEntity,
    TileLayout,
    DoorTemplate,
    InterfaceModuleDeviceDefinition,
    InterfaceModule,
    EventFilterConfiguration
}
