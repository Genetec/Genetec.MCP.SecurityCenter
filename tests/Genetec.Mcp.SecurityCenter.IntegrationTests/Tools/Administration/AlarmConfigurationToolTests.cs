using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Administration;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using System.Text.Json;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class AlarmConfigurationToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task MixedAlarmBatch_PreservesValidColorChangeAndWrongTypeFailure()
    {
        var alarm = await CreateAlarm();
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var alarmId = alarm.Guid;
        var wrongId = wrong.Id;
        var result = await AlarmConfigurationTool.Execute(sc.Manager,
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
        using var color = IntegrationTestHelpers.ParseJson(await sc.Manager.GetClient().GetEntityField(alarmId.ToString(), "AlarmColor"));
        Assert.Equal("Blue", IntegrationTestHelpers.GetResult(color.RootElement).GetProperty("AlarmColor").GetString());
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongId, "UserGroup", wrong.Name);
    }

    [Theory]
    [InlineData(AlarmConfigAction.AddRecipient)]
    [InlineData(AlarmConfigAction.RemoveRecipient)]
    public async Task MixedRecipientBatch_PreservesValidOperationAndWrongTypeFailure(AlarmConfigAction action)
    {
        var alarm = await CreateAlarm();
        var recipient = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var wrongName = $"MCP_WrongRecipient_{Guid.NewGuid():N}";
        var wrong = Assert.IsType<EntityCreateResult>(await PartitionManagementTool.Execute(sc.Manager,
            new PartitionManagementRequest(PartitionAction.Create, Items: [new PartitionManagementItem(Name: wrongName)])));
        sc.TrackForCleanup(wrong.Guid);
        var alarmId = alarm.Guid;
        var recipientId = recipient.Id;
        var wrongId = wrong.Guid;
        if (action is AlarmConfigAction.RemoveRecipient)
            await AlarmConfigurationTool.Execute(sc.Manager,
                new AlarmConfigRequest([new AlarmConfigBatchItem(alarmId, AlarmConfigAction.AddRecipient, RecipientIds: [recipientId])]));
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongId, "Partition", wrongName);
        var result = await AlarmConfigurationTool.Execute(sc.Manager,
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
        var recipients = await sc.Manager.GetClient().GetEntityField(alarmId.ToString(), "Recipients");
        if (action is AlarmConfigAction.AddRecipient)
            Assert.Contains(recipientId.ToString(), recipients, StringComparison.OrdinalIgnoreCase);
        else
            Assert.DoesNotContain(recipientId.ToString(), recipients, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(wrongId.ToString(), recipients, StringComparison.OrdinalIgnoreCase);
        await IntegrationTestHelpers.AssertEntityAsync(sc, recipientId, "UserGroup", recipient.Name);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongId, "Partition", wrongName);
    }

    [Theory]
    [InlineData(AlarmConfigAction.AddRecipient)]
    [InlineData(AlarmConfigAction.RemoveRecipient)]
    [InlineData(AlarmConfigAction.AddSchedule)]
    [InlineData(AlarmConfigAction.RemoveSchedule)]
    public async Task RelatedEntityTargets_RejectWrongEntityTypeAndPreserveEntities(AlarmConfigAction action)
    {
        var primaryId = (await CreateAlarm()).Guid;
        await IntegrationTestHelpers.AssertWrongRelatedEntityTypeAsync(sc, primaryId, "Alarm", relatedId => AlarmConfigurationTool.Execute(sc.Manager, new AlarmConfigRequest([new AlarmConfigBatchItem(primaryId, action, RecipientIds: [relatedId], ScheduleId: relatedId)])), action is AlarmConfigAction.AddRecipient or AlarmConfigAction.RemoveRecipient ? "recipient_ids" : "schedule_id", "Partition");
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
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity(AlarmConfigAction action)
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => AlarmConfigurationTool.Execute(sc.Manager, new AlarmConfigRequest([new AlarmConfigBatchItem(id, action, RecipientIds: [Guid.NewGuid()], EntityId: Guid.NewGuid(), ScheduleId: Guid.NewGuid(), Color: AlarmColor.Blue, ProcedureUrl: "https://example.test", RecordingTime: 10)])), "items", "UserGroup");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Batch_PreflightFailure_DoesNotAddEarlierRecipients(bool exceedOperationBudget)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var alarm = await CreateAlarm();
        var user = Assert.IsType<EntityCreateResult>(await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem($"MCP_Preflight_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(user.Guid);
        var recipients = Enumerable.Repeat(user.Guid, exceedOperationBudget ? 26 : 1).ToList();
        var items = new List<AlarmConfigBatchItem>
        {
            new(alarm.Guid, AlarmConfigAction.AddRecipient, RecipientIds: recipients),
            exceedOperationBudget
                ? new(alarm.Guid, AlarmConfigAction.AddRecipient, RecipientIds: recipients)
                : new(alarm.Guid, AlarmConfigAction.SetColor)
        };
        await Assert.ThrowsAsync<ArgumentException>(() => AlarmConfigurationTool.Execute(sc.Manager, new AlarmConfigRequest(items)));
        var readback = await sc.Manager.GetClient().GetEntityField(alarm.Guid.ToString(), "Recipients");
        Assert.DoesNotContain(user.Guid.ToString(), readback, StringComparison.OrdinalIgnoreCase);
    }

    // ── Integration ─────────────────────────────────────────────────

    public static IEnumerable<object[]> AlarmColors()
        => Enum.GetValues<AlarmColor>().Select(color => new object[] { color });

    [Fact]
    public async Task GetRecipients_ExistingAlarm_ReturnsString()
    {
        var name = $"MCP_TestAlarm_{Guid.NewGuid():N}";
        var createResult = await AlarmCreateTool.Execute(sc.Manager,
            new AlarmCreateRequest([new AlarmItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        Assert.NotEqual(default(Guid), created.Guid);
        var alarmId = created.Guid;
        sc.TrackForCleanup(alarmId);

        var result = await AlarmConfigurationTool.Execute(sc.Manager,
            new AlarmConfigRequest([new AlarmConfigBatchItem(
                Id: alarmId,
                Action: AlarmConfigAction.GetRecipients)]));

        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task AddAndRemoveRecipient_ExistingAlarm_Succeeds()
    {
        var alarm = await CreateAlarm();
        var userResult = await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem($"MCP_AlarmRecipient_{Guid.NewGuid():N}")]));
        var user = Assert.IsType<EntityCreateResult>(userResult);
        sc.TrackForCleanup(user.Guid);

        var addResult = await AlarmConfigurationTool.Execute(sc.Manager,
            new AlarmConfigRequest([new AlarmConfigBatchItem(
                Id: alarm.Guid,
                Action: AlarmConfigAction.AddRecipient,
                RecipientIds: [user.Guid])]));
        Assert.IsType<string>(addResult);

        var recipients = await sc.Manager.GetClient().GetEntityField(alarm.Guid.ToString(), "Recipients");
        Assert.Contains(user.Guid.ToString(), recipients, StringComparison.OrdinalIgnoreCase);

        var removeResult = await AlarmConfigurationTool.Execute(sc.Manager,
            new AlarmConfigRequest([new AlarmConfigBatchItem(
                Id: alarm.Guid,
                Action: AlarmConfigAction.RemoveRecipient,
                RecipientIds: [user.Guid])]));
        Assert.IsType<string>(removeResult);
        recipients = await sc.Manager.GetClient().GetEntityField(alarm.Guid.ToString(), "Recipients");
        Assert.DoesNotContain(user.Guid.ToString(), recipients, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddAndRemoveMultipleRecipients_ExistingAlarm_Succeeds()
    {
        var alarm = await CreateAlarm();
        var users = new List<EntityCreateResult>();
        foreach (var index in Enumerable.Range(1, 2))
        {
            var userResult = await UserCreateTool.Execute(sc.Manager,
                new UserCreateRequest([new UserItem($"MCP_AlarmBulkRecipient_{index}_{Guid.NewGuid():N}")]));
            var user = Assert.IsType<EntityCreateResult>(userResult);
            users.Add(user);
            sc.TrackForCleanup(user.Guid);
        }

        var recipientIds = users.Select(user => user.Guid).ToList();
        var addResult = await AlarmConfigurationTool.Execute(sc.Manager,
            new AlarmConfigRequest([new AlarmConfigBatchItem(
                Id: alarm.Guid,
                Action: AlarmConfigAction.AddRecipient,
                RecipientIds: recipientIds)]));
        Assert.Equal(2, Assert.IsType<AlarmRecipientBatchResult>(addResult).SuccessCount);
        var recipients = await sc.Manager.GetClient().GetEntityField(alarm.Guid.ToString(), "Recipients");
        foreach (var id in recipientIds)
            Assert.Contains(id.ToString(), recipients, StringComparison.OrdinalIgnoreCase);

        var removeResult = await AlarmConfigurationTool.Execute(sc.Manager,
            new AlarmConfigRequest([new AlarmConfigBatchItem(
                Id: alarm.Guid,
                Action: AlarmConfigAction.RemoveRecipient,
                RecipientIds: recipientIds)]));
        Assert.Equal(2, Assert.IsType<AlarmRecipientBatchResult>(removeResult).SuccessCount);
        recipients = await sc.Manager.GetClient().GetEntityField(alarm.Guid.ToString(), "Recipients");
        foreach (var id in recipientIds)
            Assert.DoesNotContain(id.ToString(), recipients, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AlarmConfigAction.AddRecipient)]
    [InlineData(AlarmConfigAction.RemoveRecipient)]
    public async Task RecipientAction_MalformedLaterGuid_DoesNotChangeEarlierRecipients(AlarmConfigAction action)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var alarm = await CreateAlarm();
        var user = Assert.IsType<EntityCreateResult>(await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem($"MCP_GuidPreflight_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(user.Guid);
        if (action == AlarmConfigAction.RemoveRecipient)
            await AlarmConfigurationTool.Execute(sc.Manager, new AlarmConfigRequest([
                new AlarmConfigBatchItem(alarm.Guid, AlarmConfigAction.AddRecipient, RecipientIds: [user.Guid])]));

        var actionJson = JsonSerializer.Serialize(action, NumericStringConverter.ToolOptions);
        var json = $$"""
            {"items":[
              {"id":"{{alarm.Guid}}","action":{{actionJson}},"recipient_ids":["{{user.Guid}}"]},
              {"id":"{{alarm.Guid}}","action":{{actionJson}},"recipient_ids":["{{user.Guid}}","invalid-guid"]}
            ]}
            """;
        var error = await Assert.ThrowsAsync<JsonException>(async () =>
        {
            var request = JsonSerializer.Deserialize<AlarmConfigRequest>(json, NumericStringConverter.ToolOptions)!;
            await AlarmConfigurationTool.Execute(sc.Manager, request);
        });
        Assert.Equal("$.items[1].recipient_ids[1]", error.Path);
        var recipients = await sc.Manager.GetClient().GetEntityField(alarm.Guid.ToString(), "Recipients");
        if (action == AlarmConfigAction.RemoveRecipient)
            Assert.Contains(user.Guid.ToString(), recipients, StringComparison.OrdinalIgnoreCase);
        else
            Assert.DoesNotContain(user.Guid.ToString(), recipients, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAttachedEntities_ExistingAlarm_ReturnsString()
    {
        var name = $"MCP_TestAlarm_{Guid.NewGuid():N}";
        var createResult = (EntityCreateResult)await AlarmCreateTool.Execute(sc.Manager,
            new AlarmCreateRequest([new AlarmItem(name)]));
        sc.TrackForCleanup(createResult.Guid);

        var result = await AlarmConfigurationTool.Execute(sc.Manager,
            new AlarmConfigRequest([new AlarmConfigBatchItem(
                Id: createResult.Guid,
                Action: AlarmConfigAction.GetAttachedEntities)]));

        Assert.IsType<string>(result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task AddAndRemoveAttachedEntity_PreservesOtherEntitiesAndSupportsRepeatedOperations(int count)
    {
        var alarms = new List<Guid>();
        for (var index = 0; index < count; index++)
            alarms.Add((await CreateAlarm()).Guid);
        var attachedAlarm = await CreateAlarm();
        var retainedAlarm = await CreateAlarm();

        async Task Mutate(AlarmConfigAction action, Guid entityId)
        {
            var result = await AlarmConfigurationTool.Execute(sc.Manager,
                new AlarmConfigRequest(alarms.Select(id => new AlarmConfigBatchItem(id, action, EntityId: entityId)).ToList()));
            if (count == 1)
                Assert.IsType<string>(result);
            else
            {
                var batch = Assert.IsType<AlarmConfigBatchResult>(result);
                Assert.Equal(count, batch.SuccessCount);
                Assert.Equal(0, batch.FailureCount);
                Assert.Equal(alarms, batch.Results.Select(item => item.AlarmId));
            }
        }

        async Task AssertAttached(params Guid[] expected)
        {
            foreach (var id in alarms)
            {
                await IntegrationTestHelpers.GetEntityNameAsync(sc, id, "Alarm");
                var result = Assert.IsType<string>(await AlarmConfigurationTool.Execute(sc.Manager,
                    new AlarmConfigRequest([new AlarmConfigBatchItem(id, AlarmConfigAction.GetAttachedEntities)])));
                var actual = IntegrationTestHelpers.ExtractGuids(result, "AttachedEntities");
                Assert.Equal(expected.Order(), actual.Order());
            }
        }

        await AssertAttached();
        await Mutate(AlarmConfigAction.RemoveAttachedEntity, attachedAlarm.Guid);
        await AssertAttached();
        await Mutate(AlarmConfigAction.AddAttachedEntity, retainedAlarm.Guid);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await Mutate(AlarmConfigAction.AddAttachedEntity, attachedAlarm.Guid);
            await AssertAttached(retainedAlarm.Guid, attachedAlarm.Guid);
        }
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await Mutate(AlarmConfigAction.RemoveAttachedEntity, attachedAlarm.Guid);
            await AssertAttached(retainedAlarm.Guid);
        }
    }

    [Fact]
    public async Task SetRecordingTime_ExistingAlarm_ReturnsSdkResponse()
    {
        var name = $"MCP_TestAlarm_{Guid.NewGuid():N}";
        var createResult = (EntityCreateResult)await AlarmCreateTool.Execute(sc.Manager,
            new AlarmCreateRequest([new AlarmItem(name)]));
        sc.TrackForCleanup(createResult.Guid);

        var result = await AlarmConfigurationTool.Execute(sc.Manager,
            new AlarmConfigRequest([new AlarmConfigBatchItem(
                Id: createResult.Guid,
                Action: AlarmConfigAction.SetRecordingTime,
                RecordingTime: 30)]));

        Assert.IsType<string>(result);
        using var readback = JsonDocument.Parse(await sc.Manager.GetClient().GetEntity(createResult.Guid.ToString()));
        var entity = IntegrationTestHelpers.GetResult(readback.RootElement);
        Assert.Equal(createResult.Guid, entity.GetProperty("Guid").GetGuid());
        Assert.Equal(TimeSpan.FromSeconds(30), TimeSpan.Parse(entity.GetProperty("AlarmRecordingTime").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task SetProcedureUrl_ExistingAlarm_StoresUrl()
    {
        var alarm = await CreateAlarm();
        const string procedureUrl = "https://example.com/alarm-procedure";

        var result = await AlarmConfigurationTool.Execute(sc.Manager,
            new AlarmConfigRequest([new AlarmConfigBatchItem(
                Id: alarm.Guid,
                Action: AlarmConfigAction.SetProcedureUrl,
                ProcedureUrl: procedureUrl)]));

        Assert.IsType<string>(result);

        var storedUrl = await sc.Manager.GetClient().GetEntityField(alarm.Guid.ToString(), "AlarmProcedureUrl");
        Assert.Contains(procedureUrl, storedUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(AlarmColors))]
    public async Task SetColor_AllAlarmColorValues_StoresAlarmColorEnum(AlarmColor color)
    {
        var name = $"MCP_TestAlarm_{Guid.NewGuid():N}";
        var createResult = (EntityCreateResult)await AlarmCreateTool.Execute(sc.Manager,
            new AlarmCreateRequest([new AlarmItem(name)]));
        sc.TrackForCleanup(createResult.Guid);

        var result = await AlarmConfigurationTool.Execute(sc.Manager,
            new AlarmConfigRequest([new AlarmConfigBatchItem(
                Id: createResult.Guid,
                Action: AlarmConfigAction.SetColor,
                Color: color)]));

        Assert.IsType<string>(result);

        var storedColor = await sc.Manager.GetClient().GetEntityField(createResult.Guid.ToString(), "AlarmColor");
        Assert.Contains(color.ToString(), storedColor, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddAndRemoveSchedule_ExistingAlarm_Succeeds()
    {
        var alarm = await CreateAlarm();
        var scheduleResult = await ScheduleManagementTool.Execute(sc.Manager,
            new ScheduleManagementRequest(
                ScheduleAction.Create,
                Items: [new ScheduleBatchItem(Name: $"MCP_AlarmSchedule_{Guid.NewGuid():N}")]));
        var schedule = Assert.IsType<EntityCreateResult>(scheduleResult);
        sc.TrackForCleanup(schedule.Guid);

        var addResult = await AlarmConfigurationTool.Execute(sc.Manager,
            new AlarmConfigRequest([new AlarmConfigBatchItem(
                Id: alarm.Guid,
                Action: AlarmConfigAction.AddSchedule,
                ScheduleId: schedule.Guid)]));
        Assert.IsType<string>(addResult);

        var schedules = await sc.Manager.GetClient().GetEntityField(alarm.Guid.ToString(), "SchedulesGuids");
        Assert.Contains(schedule.Guid.ToString(), schedules, StringComparison.OrdinalIgnoreCase);

        var removeResult = await AlarmConfigurationTool.Execute(sc.Manager,
            new AlarmConfigRequest([new AlarmConfigBatchItem(
                Id: alarm.Guid,
                Action: AlarmConfigAction.RemoveSchedule,
                ScheduleId: schedule.Guid)]));
        Assert.IsType<string>(removeResult);
    }

    [Fact]
    public async Task Batch_MultipleActions_ReturnsAllSuccesses()
    {
        var alarm = await CreateAlarm();
        var result = await AlarmConfigurationTool.Execute(sc.Manager,
            new AlarmConfigRequest([
                new AlarmConfigBatchItem(alarm.Guid, AlarmConfigAction.SetColor, Color: AlarmColor.Red),
                new AlarmConfigBatchItem(alarm.Guid, AlarmConfigAction.SetProcedureUrl,
                    ProcedureUrl: "https://example.com/batch-procedure"),
                new AlarmConfigBatchItem(alarm.Guid, AlarmConfigAction.SetRecordingTime, RecordingTime: 60),
                new AlarmConfigBatchItem(alarm.Guid, AlarmConfigAction.GetRecipients),
                new AlarmConfigBatchItem(alarm.Guid, AlarmConfigAction.GetAttachedEntities),
                new AlarmConfigBatchItem(alarm.Guid, AlarmConfigAction.Report)]));

        var batch = Assert.IsType<AlarmConfigBatchResult>(result);
        Assert.Equal(6, batch.RequestedCount);
        Assert.Equal(6, batch.SuccessCount);
        Assert.Equal(0, batch.FailureCount);
    }

    [Fact]
    public async Task Report_ExistingAlarm_ReturnsString()
    {
        var name = $"MCP_TestAlarm_{Guid.NewGuid():N}";
        var createResult = (EntityCreateResult)await AlarmCreateTool.Execute(sc.Manager,
            new AlarmCreateRequest([new AlarmItem(name)]));
        sc.TrackForCleanup(createResult.Guid);

        var result = await AlarmConfigurationTool.Execute(sc.Manager,
            new AlarmConfigRequest([new AlarmConfigBatchItem(
                Id: createResult.Guid,
                Action: AlarmConfigAction.Report)]));

        Assert.IsType<string>(result);

        var timeStart = DateTimeOffset.UtcNow.AddDays(-1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var timeEnd = DateTimeOffset.UtcNow.AddDays(1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var boundedResult = await AlarmConfigurationTool.Execute(sc.Manager,
            new AlarmConfigRequest([new AlarmConfigBatchItem(
                Id: createResult.Guid,
                Action: AlarmConfigAction.Report,
                TimeStart: timeStart,
                TimeEnd: timeEnd)]));

        Assert.IsType<string>(boundedResult);
    }

    private async Task<EntityCreateResult> CreateAlarm()
    {
        var result = await AlarmCreateTool.Execute(sc.Manager,
            new AlarmCreateRequest([new AlarmItem($"MCP_TestAlarm_{Guid.NewGuid():N}")]));
        var alarm = Assert.IsType<EntityCreateResult>(result);
        sc.TrackForCleanup(alarm.Guid);
        return alarm;
    }

}
