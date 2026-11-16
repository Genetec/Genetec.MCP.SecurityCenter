using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Validation;
using ModelContextProtocol.Server;
using static Genetec.Mcp.SecurityCenter.Client.InputValidation;

namespace Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

[McpServerToolType]
public class CameraBookmarkTool
{
    [McpServerTool(
        Name = "camera_bookmark",
        Title = "Camera Bookmarks",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Manage bookmarks on camera recordings. " +
        "Actions: add, edit, delete, report.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Camera bookmark request")] CameraBookmarkRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case CameraBookmarkAction.Add:
                if (request.Description is null) throw new ArgumentException("Parameter 'description' is required for action 'add'.");
                await client.RequireEntityType(request.Id, "id", "Camera");
                var bookmarkTime = request.BookmarkTime ?? DateTime.UtcNow;
                return await client.ExecuteAction(
                    $"AddCameraBookmark({request.Id},{Esc(FormatWebSdkTime(bookmarkTime))},{Esc(request.Description)})");

            case CameraBookmarkAction.Edit:
                if (request.BookmarkTime is null) throw new ArgumentException("Parameter 'bookmark_time' is required for action 'edit'.");
                if (request.Description is null) throw new ArgumentException("Parameter 'description' is required for action 'edit'.");
                await client.RequireEntityType(request.Id, "id", "Camera");
                return await client.ExecuteAction(
                    $"EditCameraBookmark({request.Id},{Esc(FormatWebSdkTime(request.BookmarkTime.Value, millisecondsOnly: true))},{Esc(request.Description)})");

            case CameraBookmarkAction.Delete:
                if (request.BookmarkTime is null) throw new ArgumentException("Parameter 'bookmark_time' is required for action 'delete'.");
                await client.RequireEntityType(request.Id, "id", "Camera");
                return await client.ExecuteAction(
                    $"DeleteCameraBookmark({request.Id},{Esc(FormatWebSdkTime(request.BookmarkTime.Value))})");

            case CameraBookmarkAction.Report:
                if (request.TimeStart is null) throw new ArgumentException("Parameter 'time_start' is required for report.");
                if (request.TimeEnd is null) throw new ArgumentException("Parameter 'time_end' is required for report.");
                return await client.RunReport("Bookmark",
                    $"Cameras@{request.Id},TimeRange.SetTimeRange({Esc(FormatWebSdkTime(request.TimeStart.Value))},{Esc(FormatWebSdkTime(request.TimeEnd.Value))})");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }

    private static string FormatWebSdkTime(DateTime value, bool millisecondsOnly = false)
        => value.ToUniversalTime().ToString(
            millisecondsOnly ? "yyyy-MM-dd'T'HH:mm:ss.fff'Z'" : "O",
            CultureInfo.InvariantCulture);
}

public enum CameraBookmarkAction { Add, Edit, Delete, Report }

public sealed record CameraBookmarkRequest(
    [property: Description("Camera GUID"), Required] Guid Id,
    [property: Description("Action to perform"), Required] CameraBookmarkAction Action,
    [Description("Bookmark description (required for add, edit)")] string? Description = null,
    [Description("Bookmark timestamp (for add; defaults to current UTC time; required for edit, delete)")] DateTime? BookmarkTime = null,
    [Description("Report start time (for report)")] DateTime? TimeStart = null,
    [Description("Report end time (for report)")] DateTime? TimeEnd = null);
