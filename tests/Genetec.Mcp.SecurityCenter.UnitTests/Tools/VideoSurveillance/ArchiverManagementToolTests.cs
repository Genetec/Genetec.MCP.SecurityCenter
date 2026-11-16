using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.VideoSurveillance;

public class ArchiverManagementToolTests
{
    [Theory]
    [InlineData("set_recording_modes")]
    [InlineData("move_all_video_units")]
    [InlineData("add_agent")]
    [InlineData("remove_agent")]
    public void RemovedAction_RejectsProductionBinding(string action)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ArchiverManagementRequest>(
            $$"""{"action":"{{action}}"}""", NumericStringConverter.ToolOptions));

    [Theory]
    [InlineData("get_cameras", ArchiverAction.GetCameras)]
    [InlineData("get_agents", ArchiverAction.GetAgents)]
    [InlineData("move_video_unit", ArchiverAction.MoveVideoUnit)]
    public void SupportedActions_ProductionBindingPreservesTypedGuids(string action, ArchiverAction expectedAction)
    {
        var id = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var request = JsonSerializer.Deserialize<ArchiverManagementRequest>(
            $$"""{"action":"{{action}}","id":"{{id}}","video_unit_id":"{{unitId}}","target_archiver_id":"{{targetId}}"}""",
            NumericStringConverter.ToolOptions)!;
        Assert.Equal(expectedAction, request.Action);
        Assert.Equal(id, request.Id);
        Assert.Equal(unitId, request.VideoUnitId);
        Assert.Equal(targetId, request.TargetArchiverId);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("video_unit_id")]
    [InlineData("target_archiver_id")]
    public void MalformedGuid_RejectsProductionBinding(string field)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ArchiverManagementRequest>(
            $$"""{"action":"get_agents","{{field}}":"not-a-guid"}""", NumericStringConverter.ToolOptions));

    [Fact]
    public void Discovery_ExposesOnlySupportedActionsAndFields()
    {
        var result = SearchToolsTool.Execute(new SearchToolsRequest(Query: "archiver_management", Detail: ToolDetailLevel.Full));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result, NumericStringConverter.ToolOptions));
        var tool = Assert.Single(document.RootElement.EnumerateArray(), tool => tool.GetProperty("name").GetString() == "archiver_management");
        var request = Assert.Single(tool.GetProperty("parameters").EnumerateArray());
        var fields = request.GetProperty("fields").EnumerateArray().ToArray();
        Assert.Equal(new[] { "action", "id", "video_unit_id", "target_archiver_id" },
            fields.Select(field => field.GetProperty("name").GetString()));
        var action = Assert.Single(fields, field => field.GetProperty("name").GetString() == "action");
        Assert.True(action.GetProperty("required").GetBoolean());
        Assert.Equal(new[] { "get_cameras", "get_agents", "move_video_unit" },
            action.GetProperty("enum_values").EnumerateArray().Select(value => value.GetString()));
        Assert.All(fields.Where(field => field.GetProperty("name").GetString() != "action"),
            field => Assert.Equal("Guid", field.GetProperty("type").GetString()));
        Assert.Contains("ArchiveSources", tool.GetProperty("description").GetString());
        Assert.DoesNotContain("add_agent", tool.GetProperty("description").GetString());
        Assert.DoesNotContain("remove_agent", tool.GetProperty("description").GetString());
        TestHelpers.AssertRequiredProperty<ArchiverManagementRequest>(nameof(ArchiverManagementRequest.Action));
    }

    [Fact]
    public async Task Move_RejectsWrongDestinationType()
        => await TestHelpers.AssertWrongRelatedEntityTypeAsync("VideoUnit", (manager, primaryId, relatedId) =>
            ArchiverManagementTool.Execute(manager, new ArchiverManagementRequest(ArchiverAction.MoveVideoUnit,
                VideoUnitId: primaryId, TargetArchiverId: relatedId)), "target_archiver_id");

    [Fact]
    public async Task Move_RejectsWrongVideoUnitType()
        => await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => ArchiverManagementTool.Execute(manager,
            new ArchiverManagementRequest(ArchiverAction.MoveVideoUnit,
                VideoUnitId: id, TargetArchiverId: Guid.NewGuid())), "video_unit_id");

    [Theory]
    [InlineData(ArchiverAction.GetCameras)]
    [InlineData(ArchiverAction.GetAgents)]
    public async Task CurrentEntityTargets_RejectWrongEntityType(ArchiverAction action)
        => await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => ArchiverManagementTool.Execute(manager,
            new ArchiverManagementRequest(action, Id: id)), "id", "UserGroup");

    [Theory]
    [InlineData(ArchiverAction.GetCameras)]
    [InlineData(ArchiverAction.GetAgents)]
    public async Task Action_MissingArchiverId_ThrowsArgumentException(ArchiverAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ArchiverManagementTool.Execute(TestHelpers.FakeManager(), new ArchiverManagementRequest(action)));
        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MoveVideoUnit_MissingRequiredGuid_ThrowsArgumentException(bool missingVideoUnit)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ArchiverManagementTool.Execute(TestHelpers.FakeManager(),
                new ArchiverManagementRequest(ArchiverAction.MoveVideoUnit,
                    VideoUnitId: missingVideoUnit ? null : Guid.NewGuid(),
                    TargetArchiverId: missingVideoUnit ? Guid.NewGuid() : null)));
        Assert.Contains(missingVideoUnit ? "video_unit_id" : "target_archiver_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidAction_RejectsBeforeHttp()
        => await Assert.ThrowsAsync<ArgumentException>(() => ArchiverManagementTool.Execute(TestHelpers.FakeManager(),
            new ArchiverManagementRequest((ArchiverAction)int.MaxValue)));
}
