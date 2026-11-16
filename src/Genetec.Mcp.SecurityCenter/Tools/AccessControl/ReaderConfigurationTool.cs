using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

public enum ReaderConfigAction
{
    Get,
    EnableCardAndPin,
    EnableCardOrPin,
    DisableCardAndPin,
    EnableAssuranceProfile
}

public sealed record ReaderConfigRequest(
    [property: Description("Reader GUID"), Required] Guid Id,
    [property: Description("Action to perform"), Required] ReaderConfigAction Action,
    [property: Description("Assurance profile ProfileId from assurance_profile_list (required for enable_assurance_profile action)")] Guid? AssuranceProfileId = null,
    [property: Description("Activation schedule GUID (required for enable_card_and_pin action)")] Guid? ActivationScheduleId = null,
    [property: Description("Failure timeout as hh:mm:ss, from 00:00:00 to 00:16:39 (required for enable_card_and_pin action)")] TimeSpan? FailureTimeout = null);

[McpServerToolType]
public class ReaderConfigurationTool
{
    [McpServerTool(Name = "reader_configuration", Title = "Reader Configuration", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true),
     Description("Configure access control readers. Provide either a Reader device GUID or a card-reader access point GUID; access points are resolved to their associated Reader device. Use when changing reader authentication mode (card only, PIN only, card-and-PIN, card-or-PIN) or enabling assurance profiles. Actions: get, enable_card_and_pin, enable_card_or_pin, disable_card_and_pin, enable_assurance_profile.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Reader configuration request")] ReaderConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case ReaderConfigAction.Get:
            {
                var readerId = await client.ResolveReaderDeviceId(request.Id);
                return await client.GetEntityTrimmed(readerId.ToString());
            }
            case ReaderConfigAction.EnableCardAndPin:
            {
                if (request.ActivationScheduleId is null)
                    throw new ArgumentException("Parameter 'activation_schedule_id' is required for enable_card_and_pin.");
                if (request.FailureTimeout is null)
                    throw new ArgumentException("Parameter 'failure_timeout' is required for enable_card_and_pin.");

                var failureTimeout = request.FailureTimeout.Value;
                if (failureTimeout < TimeSpan.Zero || failureTimeout > TimeSpan.FromSeconds(999))
                    throw new ArgumentException("Parameter 'failure_timeout' must be between 00:00:00 and 00:16:39.");

                var readerId = await client.ResolveReaderDeviceId(request.Id);
                await client.RequireEntityType(request.ActivationScheduleId.Value, "activation_schedule_id", "Schedule");
                return await client.ExecuteEntityMethod(
                    readerId.ToString(),
                    "EnableCardAndPin",
                    $"{request.ActivationScheduleId},{failureTimeout:hh\\:mm\\:ss}");
            }
            case ReaderConfigAction.EnableCardOrPin:
            {
                var readerId = await client.ResolveReaderDeviceId(request.Id);
                return await client.ExecuteEntityMethod(readerId.ToString(), "EnableCardOrPin");
            }
            case ReaderConfigAction.DisableCardAndPin:
            {
                var readerId = await client.ResolveReaderDeviceId(request.Id);
                return await client.ExecuteEntityMethod(readerId.ToString(), "DisableCardAndPin");
            }
            case ReaderConfigAction.EnableAssuranceProfile:
            {
                if (request.AssuranceProfileId is null)
                    throw new ArgumentException("Parameter 'assurance_profile_id' is required for enable_assurance_profile.");

                var readerId = await client.ResolveReaderDeviceId(request.Id);
                return await client.ExecuteEntityMethod(readerId.ToString(), "EnableAssuranceProfile", request.AssuranceProfileId!.ToString());
            }
            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}
