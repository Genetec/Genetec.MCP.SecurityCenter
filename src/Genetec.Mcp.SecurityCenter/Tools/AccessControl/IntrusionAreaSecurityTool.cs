using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

public enum IntrusionAreaSecurityAction
{
    Arm,
    Disarm
}

public enum IntrusionArmScope { Master, Perimeter }
public enum IntrusionArmTiming { Instant, DefaultDelay, CustomDelay }
public enum IntrusionArmType { Normal, Forced, Bypass }

public sealed record IntrusionAreaSecurityRequest(
    [property: Description("Security action to perform"), Required]
    IntrusionAreaSecurityAction Action,
    [property: Description("Intrusion area GUID"), Required]
    Guid IntrusionAreaId,
    [property: Description("Arming scope; used for arm")]
    IntrusionArmScope Scope = IntrusionArmScope.Master,
    [property: Description("Arming timing; used for arm")]
    IntrusionArmTiming Timing = IntrusionArmTiming.Instant,
    [property: Description("Custom arming delay in seconds; used when timing is custom_delay")]
    int CustomDelay = 0,
    [property: Description("Arming type; used for arm")]
    IntrusionArmType Type = IntrusionArmType.Normal);

[McpServerToolType]
public sealed class IntrusionAreaSecurityTool
{
    [McpServerTool(
        Name = "intrusion_area_security",
        Title = "Intrusion Area Security",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Arm or disarm an intrusion area. " +
        "Arming supports master/perimeter scope, instant/default/custom timing, " +
        "and normal/forced/bypass arm types.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Intrusion area security request")] IntrusionAreaSecurityRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        if (!Enum.IsDefined(request.Action))
            throw new ArgumentException($"Unknown action: {request.Action}", "action");
        await client.RequireEntityType(request.IntrusionAreaId, "intrusion_area_id", "IntrusionArea");

        return request.Action switch
        {
            IntrusionAreaSecurityAction.Arm => await client.ExecuteAction(
                $"ArmIntrusionArea({request.IntrusionAreaId},ArmingData({request.Scope},{request.Timing},{request.CustomDelay},{request.Type}))"),
            IntrusionAreaSecurityAction.Disarm => await client.ExecuteAction(
                $"DisarmIntrusionArea({request.IntrusionAreaId})"),
            _ => throw new ArgumentException($"Unknown action: {request.Action}")
        };
    }
}
