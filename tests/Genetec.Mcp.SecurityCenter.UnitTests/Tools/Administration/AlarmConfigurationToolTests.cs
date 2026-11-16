using System.Net;
using Genetec.Mcp.SecurityCenter.Tools.Administration;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools;
using System.Text.Json;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class AlarmConfigurationToolTests
{
    [Theory]
    [InlineData(AlarmConfigAction.AddAttachedEntity)]
    [InlineData(AlarmConfigAction.RemoveAttachedEntity)]
    public async Task MalformedAttachedEntities_BatchPreservesSuccessfulItemAndRejectsInvalidCollection(AlarmConfigAction action)
    {
        var validId = Guid.NewGuid();
        var invalidId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var mutations = new List<Uri>();
        var handler = new Tools.ToolTestHandler
        {
            ResponseFactory = request =>
            {
                var query = Uri.UnescapeDataString(request.RequestUri!.Query);
                var id = query.Contains(invalidId.ToString(), StringComparison.Ordinal) ? invalidId : validId;
                if (request.Method == HttpMethod.Post)
                    mutations.Add(request.RequestUri);
                var response = request.Method == HttpMethod.Post ? "{}"
                    : query.EndsWith(",Guid,EntityType", StringComparison.Ordinal)
                        ? $"{{\"Guid\":\"{id}\",\"EntityType\":\"Alarm\"}}"
                        : id == invalidId ? "{\"AttachedEntities\":[\"not-a-guid\"]}" : "{\"AttachedEntities\":[]}";
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) };
            }
        };

        var result = await AlarmConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new AlarmConfigRequest([
                new AlarmConfigBatchItem(validId, action, EntityId: entityId),
                new AlarmConfigBatchItem(invalidId, action, EntityId: entityId)]));
        var batch = Assert.IsType<AlarmConfigBatchResult>(result);
        Assert.Equal(1, batch.SuccessCount);
        Assert.Equal(1, batch.FailureCount);
        Assert.Equal(validId, batch.Results[0].AlarmId);
        Assert.Null(batch.Results[0].Error);
        Assert.Equal(invalidId, batch.Results[1].AlarmId);
        Assert.True(batch.Results[1].Error);
        Assert.Contains("Guid", batch.Results[1].Message);
        Assert.Contains(validId.ToString(), Uri.UnescapeDataString(Assert.Single(mutations).Query));
    }

    [Fact]
    public async Task MixedAlarmBatch_PreservesValidColorChangeAndWrongTypeFailure()
    {
        var alarmId = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(alarmId, "Alarm");
        handler.AddEntity(wrongId, "UserGroup");
        var result = await AlarmConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new AlarmConfigRequest([
                new AlarmConfigBatchItem(alarmId, AlarmConfigAction.SetColor, Color: AlarmColor.Blue),
                new AlarmConfigBatchItem(wrongId, AlarmConfigAction.SetColor, Color: AlarmColor.Red)]));
        var batch = Assert.IsType<AlarmConfigBatchResult>(result);
        Assert.Equal(2, batch.RequestedCount);
        Assert.Equal(1, batch.SuccessCount);
        Assert.Equal(1, batch.FailureCount);
        Assert.Equal(alarmId, batch.Results[0].AlarmId);
        Assert.Equal(wrongId, batch.Results[1].AlarmId);
        Assert.True(batch.Results[1].Error);
        Assert.Contains("items", batch.Results[1].Message);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
    }

    [Theory]
    [InlineData(AlarmConfigAction.AddRecipient)]
    [InlineData(AlarmConfigAction.RemoveRecipient)]
    public async Task MixedRecipientBatch_PreservesValidOperationAndWrongTypeFailure(AlarmConfigAction action)
    {
        var alarmId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(alarmId, "Alarm");
        handler.AddEntity(recipientId, "UserGroup");
        handler.AddEntity(wrongId, "Partition");
        var result = await AlarmConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new AlarmConfigRequest([new AlarmConfigBatchItem(alarmId, action, RecipientIds: [recipientId, wrongId])]));
        var batch = Assert.IsType<AlarmRecipientBatchResult>(result);
        Assert.Equal(2, batch.RequestedCount);
        Assert.Equal(1, batch.SuccessCount);
        Assert.Equal(1, batch.FailureCount);
        Assert.Equal(recipientId, batch.Results[0].RecipientId);
        Assert.False(batch.Results[0].Error);
        Assert.Equal(wrongId, batch.Results[1].RecipientId);
        Assert.True(batch.Results[1].Error);
        Assert.Contains("recipient_ids", batch.Results[1].Message);
        Assert.Equal(4, handler.Requests.Count);
        Assert.Single(handler.Requests, request =>
            !Uri.UnescapeDataString(request.Uri.Query).EndsWith(",Guid,EntityType", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(AlarmConfigAction.AddRecipient)]
    [InlineData(AlarmConfigAction.RemoveRecipient)]
    [InlineData(AlarmConfigAction.AddSchedule)]
    [InlineData(AlarmConfigAction.RemoveSchedule)]
    public async Task RelatedEntityTargets_RejectWrongEntityType(AlarmConfigAction action)
    {
        await TestHelpers.AssertWrongRelatedEntityTypeAsync("Alarm", (manager, primaryId, relatedId) => AlarmConfigurationTool.Execute(manager, new AlarmConfigRequest([new AlarmConfigBatchItem(primaryId, action, RecipientIds: [relatedId], ScheduleId: relatedId)])), action is AlarmConfigAction.AddRecipient or AlarmConfigAction.RemoveRecipient ? "recipient_ids" : "schedule_id", "Partition");
    }

    [Theory]
    [InlineData(AlarmConfigAction.GetRecipients)]
    [InlineData(AlarmConfigAction.AddRecipient)]
    [InlineData(AlarmConfigAction.RemoveRecipient)]
    [InlineData(AlarmConfigAction.GetAttachedEntities)]
    [InlineData(AlarmConfigAction.AddAttachedEntity)]
    [InlineData(AlarmConfigAction.RemoveAttachedEntity)]
    [InlineData(AlarmConfigAction.SetColor)]
    [InlineData(AlarmConfigAction.SetProcedureUrl)]
    [InlineData(AlarmConfigAction.SetRecordingTime)]
    [InlineData(AlarmConfigAction.AddSchedule)]
    [InlineData(AlarmConfigAction.RemoveSchedule)]
    public async Task CurrentEntityTargets_RejectWrongEntityType(AlarmConfigAction action)
    {
        await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => AlarmConfigurationTool.Execute(manager, new AlarmConfigRequest([new AlarmConfigBatchItem(id, action, RecipientIds: [Guid.NewGuid()], EntityId: Guid.NewGuid(), ScheduleId: Guid.NewGuid(), Color: AlarmColor.Blue, ProcedureUrl: "https://example.test", RecordingTime: 10)])), "items", "UserGroup");
    }

    [Fact]
    public async Task Batch_InvalidLaterAction_RejectsBeforeAnyMutation()
    {
        var handler = new Tools.ToolTestHandler();
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmConfigurationTool.Execute(TestHelpers.FakeManager(handler),
                new AlarmConfigRequest([
                    new AlarmConfigBatchItem(Guid.NewGuid(), AlarmConfigAction.SetColor, Color: AlarmColor.Blue),
                    new AlarmConfigBatchItem(Guid.NewGuid(), AlarmConfigAction.AddRecipient)])));
        Assert.Equal("recipient_ids", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(AlarmConfigAction.AddRecipient)]
    [InlineData(AlarmConfigAction.RemoveRecipient)]
    public async Task Batch_NestedRecipientsExceedTotalBudget_RejectsBeforeAnyMutation(AlarmConfigAction action)
    {
        var handler = new Tools.ToolTestHandler();
        var recipients = Enumerable.Range(0, 26).Select(_ => Guid.NewGuid()).ToList();
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmConfigurationTool.Execute(TestHelpers.FakeManager(handler),
                new AlarmConfigRequest([
                    new AlarmConfigBatchItem(Guid.NewGuid(), action, RecipientIds: recipients),
                    new AlarmConfigBatchItem(Guid.NewGuid(), action, RecipientIds: recipients)])));
        Assert.Equal("items", error.ParamName);
        Assert.Contains("52 operations", error.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }


    [Fact]
    public async Task Report_WithoutTimeRange_PreservesEntityFilter()
    {
        var handler = new Tools.ToolTestHandler();
        var id = Guid.NewGuid();

        await AlarmConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new AlarmConfigRequest([new AlarmConfigBatchItem(id, AlarmConfigAction.Report)]));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            $"/WebSdk/report/AlarmActivity?q=Alarms@{id}",
            Uri.UnescapeDataString(request.AbsolutePath + request.Query));
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task AddRecipient_MissingRecipientIds_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmConfigurationTool.Execute(TestHelpers.FakeManager(),
                new AlarmConfigRequest([new AlarmConfigBatchItem(
                    Id: Guid.NewGuid(),
                    Action: AlarmConfigAction.AddRecipient)])));

        Assert.Contains("recipient_ids", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveRecipient_MissingRecipientIds_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmConfigurationTool.Execute(TestHelpers.FakeManager(),
                new AlarmConfigRequest([new AlarmConfigBatchItem(
                    Id: Guid.NewGuid(),
                    Action: AlarmConfigAction.RemoveRecipient)])));

        Assert.Contains("recipient_ids", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddAttachedEntity_MissingEntityId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmConfigurationTool.Execute(TestHelpers.FakeManager(),
                new AlarmConfigRequest([new AlarmConfigBatchItem(
                    Id: Guid.NewGuid(),
                    Action: AlarmConfigAction.AddAttachedEntity)])));

        Assert.Contains("entity_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveAttachedEntity_MissingEntityId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmConfigurationTool.Execute(TestHelpers.FakeManager(),
                new AlarmConfigRequest([new AlarmConfigBatchItem(
                    Id: Guid.NewGuid(),
                    Action: AlarmConfigAction.RemoveAttachedEntity)])));

        Assert.Contains("entity_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetColor_MissingColor_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmConfigurationTool.Execute(TestHelpers.FakeManager(),
                new AlarmConfigRequest([new AlarmConfigBatchItem(
                    Id: Guid.NewGuid(),
                    Action: AlarmConfigAction.SetColor)])));

        Assert.Contains("color", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AlarmColor.Black)]
    [InlineData(AlarmColor.Gray)]
    [InlineData(AlarmColor.White)]
    [InlineData(AlarmColor.DarkBlue)]
    [InlineData(AlarmColor.Blue)]
    [InlineData(AlarmColor.Cyan)]
    [InlineData(AlarmColor.DarkGreen)]
    [InlineData(AlarmColor.Green)]
    [InlineData(AlarmColor.Red)]
    [InlineData(AlarmColor.DarkRed)]
    [InlineData(AlarmColor.Orange)]
    [InlineData(AlarmColor.Yellow)]
    [InlineData(AlarmColor.Indigo)]
    public async Task SetColor_UsesAlarmColorEnumName(AlarmColor color)
    {
        var alarmId = Guid.NewGuid();
        var handler = new Tools.ToolTestHandler();
        var metadata = new Tools.EntityMetadataTestHandler(handler);
        metadata.AddEntity(alarmId, "Alarm");

        await AlarmConfigurationTool.Execute(TestHelpers.FakeManager(metadata),
            new AlarmConfigRequest([new AlarmConfigBatchItem(
                Id: alarmId,
                Action: AlarmConfigAction.SetColor,
                Color: color)]));

        var request = Assert.Single(handler.Requests);
        Assert.Equal($"/WebSdk/entity?q=entity={alarmId},AlarmColor={color}",
            request.AbsolutePath + Uri.UnescapeDataString(request.Query));
        Assert.Contains("%3D", request.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetProcedureUrl_MissingProcedureUrl_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmConfigurationTool.Execute(TestHelpers.FakeManager(),
                new AlarmConfigRequest([new AlarmConfigBatchItem(
                    Id: Guid.NewGuid(),
                    Action: AlarmConfigAction.SetProcedureUrl)])));

        Assert.Contains("procedure_url", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetRecordingTime_MissingRecordingTime_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmConfigurationTool.Execute(TestHelpers.FakeManager(),
                new AlarmConfigRequest([new AlarmConfigBatchItem(
                    Id: Guid.NewGuid(),
                    Action: AlarmConfigAction.SetRecordingTime)])));

        Assert.Contains("recording_time", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AlarmConfigAction.AddSchedule)]
    [InlineData(AlarmConfigAction.RemoveSchedule)]
    public async Task ScheduleAction_MissingScheduleId_ThrowsArgumentException(AlarmConfigAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmConfigurationTool.Execute(TestHelpers.FakeManager(),
                new AlarmConfigRequest([new AlarmConfigBatchItem(
                    Id: Guid.NewGuid(), Action: action)])));

        Assert.Contains("schedule_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AlarmConfigAction.AddRecipient)]
    [InlineData(AlarmConfigAction.RemoveRecipient)]
    public async Task RecipientAction_Multiple_ReturnsRawSdkResponsePerRecipient(AlarmConfigAction action)
    {
        var handler = new Tools.ToolTestHandler();
        var metadata = new Tools.EntityMetadataTestHandler(handler);
        var alarmId = Guid.NewGuid();
        metadata.AddEntity(alarmId, "Alarm");
        var recipientIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        foreach (var id in recipientIds) metadata.AddEntity(id, "User");
        var result = await AlarmConfigurationTool.Execute(TestHelpers.FakeManager(metadata),
            new AlarmConfigRequest([new AlarmConfigBatchItem(
                Id: alarmId,
                Action: action,
                RecipientIds: recipientIds)]));

        var responses = Assert.IsType<AlarmRecipientBatchResult>(result).Results
            .Select(item => item.Response)
            .ToList();

        Assert.Equal(2, responses.Count);
        Assert.All(responses, response => Assert.Equal(
            "{\"Guid\":\"00000000-0000-0000-0000-000000000001\",\"Status\":\"Ok\",\"Name\":\"Test\"}",
            response));
        foreach (var id in recipientIds)
            Assert.Contains(handler.Requests, request => Uri.UnescapeDataString(request.Query).Contains(id.ToString(), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("add_recipient")]
    [InlineData("remove_recipient")]
    public async Task RecipientAction_MalformedLaterGuid_ProductionBindingRejectsBeforeAnyMutation(string action)
    {
        var handler = new Tools.ToolTestHandler();
        var manager = TestHelpers.FakeManager(handler);
        var json = $$"""
            {"items":[
              {"id":"{{Guid.NewGuid()}}","action":"{{action}}","recipient_ids":["{{Guid.NewGuid()}}"]},
              {"id":"{{Guid.NewGuid()}}","action":"{{action}}","recipient_ids":["{{Guid.NewGuid()}}","invalid-guid"]}
            ]}
            """;
        var error = await Assert.ThrowsAsync<JsonException>(async () =>
        {
            var request = JsonSerializer.Deserialize<AlarmConfigRequest>(json, NumericStringConverter.ToolOptions)!;
            await AlarmConfigurationTool.Execute(manager, request);
        });
        Assert.Equal("$.items[1].recipient_ids[1]", error.Path);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void RecipientIds_DiscoveryExposesGuidCollection()
    {
        var discovery = SearchToolsTool.Execute(new SearchToolsRequest("alarm_configuration", Detail: ToolDetailLevel.Full));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(discovery, NumericStringConverter.ToolOptions));
        var tool = Assert.Single(document.RootElement.EnumerateArray(), item => item.GetProperty("name").GetString() == "alarm_configuration");
        var fields = Assert.Single(tool.GetProperty("parameters").EnumerateArray()).GetProperty("fields");
        var items = Assert.Single(fields.EnumerateArray(), item => item.GetProperty("name").GetString() == "items");
        var recipients = Assert.Single(items.GetProperty("fields").EnumerateArray(), item => item.GetProperty("name").GetString() == "recipient_ids");
        Assert.Equal("List<Guid>", recipients.GetProperty("type").GetString());
        Assert.Equal(50, recipients.GetProperty("max_length").GetInt32());
    }

    [Fact]
    public async Task Report_WithTimeRange_UsesTimeFilter()
    {
        var handler = new Tools.ToolTestHandler();
        var alarmId = Guid.NewGuid();

        await AlarmConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new AlarmConfigRequest([new AlarmConfigBatchItem(
                Id: alarmId,
                Action: AlarmConfigAction.Report,
                TimeStart: "2024-01-01T00:00:00Z",
                TimeEnd: "2024-01-02T00:00:00Z")]));

        var uri = Uri.UnescapeDataString(Assert.Single(handler.Requests).ToString());
        Assert.Contains(
            $"report/AlarmActivity?q=Alarms@{alarmId},TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z)",
            uri,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Report_WithOnlyOneTimeBoundary_ThrowsArgumentException(bool provideStart)
    {
        var handler = new Tools.ToolTestHandler();
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            AlarmConfigurationTool.Execute(TestHelpers.FakeManager(handler),
                new AlarmConfigRequest([new AlarmConfigBatchItem(
                    Id: Guid.NewGuid(),
                    Action: AlarmConfigAction.Report,
                    TimeStart: provideStart ? "2024-01-01T00:00:00Z" : null,
                    TimeEnd: provideStart ? null : "2024-01-02T00:00:00Z")])));

        Assert.Contains("time_start", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("time_end", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }
}
