using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Utilities;

[McpServerToolType]
public class SystemCommunicationTool
{
    [McpServerTool(
        Name = "system_communication",
        Title = "System Communication",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description("Send messages, emails, and play sounds through Security Center. Actions: send_message, send_email, play_sound.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("System communication request")] SystemCommunicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case CommunicationAction.SendMessage:
                if (request.RecipientId is null) throw new ArgumentException("Parameter 'recipient_id' is required for action 'send_message'.");
                if (request.Message is null) throw new ArgumentException("Parameter 'message' is required for action 'send_message'.");
                await client.RequireEntityType(request.RecipientId.Value, "recipient_id", "User", "UserGroup");
                return await client.ExecuteAction(
                    $"SendMessage({request.RecipientId},{SecurityCenterRestClient.EscapeQueryValue(request.Message)})");

            case CommunicationAction.SendEmail:
                if (request.RecipientId is null) throw new ArgumentException("Parameter 'recipient_id' is required for action 'send_email'.");
                if (request.Message is null) throw new ArgumentException("Parameter 'message' is required for action 'send_email'.");
                await client.RequireEntityType(request.RecipientId.Value, "recipient_id", "User", "UserGroup");
                return await client.ExecuteAction(
                    $"SendEmail({request.RecipientId},{SecurityCenterRestClient.EscapeQueryValue(request.Message)})");

            case CommunicationAction.PlaySound:
                if (request.RecipientId is null) throw new ArgumentException("Parameter 'recipient_id' is required for action 'play_sound'.");
                if (request.SoundId is null) throw new ArgumentException("Parameter 'sound_id' is required for action 'play_sound'.");
                await client.RequireEntityType(request.RecipientId.Value, "recipient_id", "User", "UserGroup");
                await client.RequireEntityType(request.SoundId.Value, "sound_id", "File");
                return await client.ExecuteAction(
                    $"PlaySound({request.RecipientId},{request.SoundId})");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum CommunicationAction { SendMessage, SendEmail, PlaySound }

public sealed record SystemCommunicationRequest(
    [property: Description("Action to perform"), Required] CommunicationAction Action,
    [property: Description("Recipient user or user group GUID")] Guid? RecipientId = null,
    [Description("Message text (for send_message, send_email)")] string? Message = null,
    [property: Description("Sound file entity GUID (for play_sound)")] Guid? SoundId = null);
