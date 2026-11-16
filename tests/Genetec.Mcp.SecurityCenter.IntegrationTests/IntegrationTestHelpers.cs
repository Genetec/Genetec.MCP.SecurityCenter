using System.Text.Json;
using System.Text.Json.Serialization;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests;

internal static class IntegrationTestHelpers
{
    public static void AssertScAvailable(ScIntegrationFixture sc)
        => Assert.True(sc.IsAvailable,
            "Security Center integration configuration is required in appsettings.test.json.");

    public static JsonDocument ParseJson(string json)
        => JsonDocument.Parse(json);

    public static JsonElement GetResult(JsonElement root)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Rsp", out var rsp) &&
           rsp.TryGetProperty("Result", out var result) ? result : root;

    public static async Task AssertStatusMutationAsync(
        ScIntegrationFixture sc, Guid id, string entityType, string action,
        string activationDate, string expirationDate, int unusedDays, int firstUseDays)
    {
        using var document = ParseJson(await sc.Manager.GetClient().GetEntityFields(id.ToString(), "Guid", "EntityType", "Status"));
        var entity = GetResult(document.RootElement);
        Assert.Equal(id, entity.GetProperty("Guid").GetGuid());
        Assert.Equal(entityType, entity.GetProperty("EntityType").GetString());
        var status = entity.GetProperty("Status");
        switch (action)
        {
            case "Activate":
                Assert.Equal("Active", status.GetProperty("State").GetString());
                break;
            case "Deactivate":
                Assert.Equal("Inactive", status.GetProperty("State").GetString());
                break;
            case "MarkLost":
                Assert.Equal("Lost", status.GetProperty("State").GetString());
                break;
            case "MarkStolen":
                Assert.Equal("Stolen", status.GetProperty("State").GetString());
                break;
            case "ActivateOnDate":
            case "ActivateDateRange":
                Assert.Equal("SpecificDate", status.GetProperty("ActivationType").GetString());
                AssertStatusDate(status, "ActivationDate", activationDate);
                if (action == "ActivateDateRange")
                {
                    Assert.Equal("SpecificDate", status.GetProperty("ExpirationType").GetString());
                    AssertStatusDate(status, "ExpirationDate", expirationDate);
                }
                break;
            case "DeactivateOnDate":
                Assert.Equal("SpecificDate", status.GetProperty("ExpirationType").GetString());
                AssertStatusDate(status, "ExpirationDate", expirationDate);
                break;
            case "SetExpirationNever":
                Assert.Equal("Never", status.GetProperty("ExpirationType").GetString());
                break;
            case "SetExpirationIfUnused":
            case "SetExpirationUponFirstUse":
                Assert.Equal(action == "SetExpirationIfUnused" ? "WhenNotUsed" : "OnFirstUse",
                    status.GetProperty("ExpirationType").GetString());
                Assert.Equal(action == "SetExpirationIfUnused" ? unusedDays : firstUseDays,
                    status.GetProperty("ExpirationDuration").GetInt32());
                break;
            default:
                throw new ArgumentException($"Unknown status test action: {action}");
        }
    }

    private static void AssertStatusDate(JsonElement status, string field, string expected)
        => Assert.InRange((DateTimeOffset.Parse(status.GetProperty(field).GetString()!) - DateTimeOffset.Parse(expected)).Duration(),
            TimeSpan.Zero, TimeSpan.FromSeconds(1));

    public static JsonElement GetRows(JsonElement root)
    {
        root = GetResult(root);
        if (root.ValueKind == JsonValueKind.Array)
            return root;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Body", out var body))
            return body;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Cameras", out var cameras))
            return cameras;
        throw new InvalidOperationException("Expected report or entity list rows.");
    }

    public static JsonElement GetReportField(JsonElement root, JsonElement row, string name)
    {
        if (row.ValueKind == JsonValueKind.Object) return row.GetProperty(name);
        var columns = GetResult(root).GetProperty("Columns");
        var index = columns.EnumerateArray().Select((column, index) => (column, index))
            .Single(entry => entry.column.GetString() == name).index;
        return row[index];
    }

    public static async Task<Guid> GetFirstVideoEncoderCameraIdAsync(ScIntegrationFixture sc)
        => (await GetVideoEncoderCameraIdsAsync(sc))[0];

    public static async Task<List<Guid>> GetVideoEncoderCameraIdsAsync(ScIntegrationFixture sc, int maximumCameraCount = 1)
    {
        AssertScAvailable(sc);
        var client = sc.Manager.GetClient();
        // Device subtype 5 (BQ==) is VideoEncoder. Verify its unit and camera relationship.
        using var encoders = ParseJson(await client.RunPagedReport("EntityConfiguration",
            "EntityTypeFilter.Add(Device,BQ==)", pageSize: 10, page: 1));
        var cameraIds = new List<Guid>();
        foreach (var encoderRow in GetRows(encoders.RootElement).EnumerateArray())
        {
            var encoderId = encoderRow.GetProperty("Guid").GetGuid();
            using var encoderDocument = ParseJson(await client.GetEntity(encoderId.ToString()));
            var encoder = GetResult(encoderDocument.RootElement);
            Assert.Equal(encoderId, encoder.GetProperty("Guid").GetGuid());
            Assert.Equal("Device", encoder.GetProperty("EntityType").GetString());
            Assert.Equal("VideoEncoder", encoder.GetProperty("DeviceType").GetString());
            var unitId = encoder.GetProperty("Unit").GetGuid();
            using var unitDocument = ParseJson(await client.GetEntityFields(unitId.ToString(), "Guid", "EntityType", "Cameras"));
            var unit = GetResult(unitDocument.RootElement);
            Assert.Equal(unitId, unit.GetProperty("Guid").GetGuid());
            Assert.Equal("VideoUnit", unit.GetProperty("EntityType").GetString());
            foreach (var cameraValue in unit.GetProperty("Cameras").EnumerateArray())
            {
                var cameraId = cameraValue.GetGuid();
                using var cameraDocument = ParseJson(await client.GetEntityFields(cameraId.ToString(), "Guid", "EntityType", "VideoEncoderDevice"));
                var camera = GetResult(cameraDocument.RootElement);
                Assert.Equal(cameraId, camera.GetProperty("Guid").GetGuid());
                Assert.Equal("Camera", camera.GetProperty("EntityType").GetString());
                if (camera.GetProperty("VideoEncoderDevice").GetGuid() == encoderId && !cameraIds.Contains(cameraId))
                    cameraIds.Add(cameraId);
                if (cameraIds.Count == maximumCameraCount)
                    return cameraIds;
            }
        }
        Assert.NotEmpty(cameraIds);
        return cameraIds;
    }

    public static async Task AssertSmallPages(
        ScIntegrationFixture sc,
        Func<int, int, Task<object>> execute,
        int pageSize,
        Action<string>? report = null)
    {
        AssertScAvailable(sc);
        if (pageSize == ResourceLimits.MaxPageSize)
        {
            var json = Assert.IsType<string>(await execute(pageSize, 1));
            using var document = ParseJson(json);
            var rows = GetRows(document.RootElement);
            var ids = ExtractGuids(json);
            report?.Invoke($"Page ceiling: rows={rows.GetArrayLength()}, page_size={pageSize}, GUIDs=[{string.Join(",", ids)}].");
            Assert.InRange(rows.GetArrayLength(), 0, pageSize);
            Assert.Equal(rows.GetArrayLength(), ids.Count);
            var invalid = await Assert.ThrowsAsync<ArgumentException>(() => execute(pageSize + 1, 1));
            Assert.Equal("page_size", invalid.ParamName);
            if (ids.Count > 0)
            {
                using var entity = ParseJson(await sc.Manager.GetClient().GetEntity(ids[0].ToString()));
                Assert.Equal(ids[0], GetResult(entity.RootElement).GetProperty("Guid").GetGuid());
            }
            return;
        }
        var firstJson = Assert.IsType<string>(await execute(pageSize, 1));
        var secondJson = Assert.IsType<string>(await execute(pageSize, 2));
        using var first = ParseJson(firstJson);
        using var second = ParseJson(secondJson);
        var firstRows = GetRows(first.RootElement);
        var secondRows = GetRows(second.RootElement);
        report?.Invoke($"Rows: page_1={firstRows.GetArrayLength()}, page_2={secondRows.GetArrayLength()}, page_size={pageSize}.");
        Assert.InRange(firstRows.GetArrayLength(), 0, pageSize);
        Assert.InRange(secondRows.GetArrayLength(), 0, pageSize);

        var firstIds = ExtractGuids(firstJson);
        var secondIds = ExtractGuids(secondJson);
        report?.Invoke($"GUIDs: page_1=[{string.Join(",", firstIds)}], page_2=[{string.Join(",", secondIds)}].");
        if (report is not null && firstRows.GetArrayLength() > 0 && firstRows[0].ValueKind == JsonValueKind.Object)
            report($"First row fields: {string.Join(",", firstRows[0].EnumerateObject().Select(property => property.Name))}.");
        Assert.Equal(firstRows.GetArrayLength(), firstIds.Count);
        Assert.Equal(secondRows.GetArrayLength(), secondIds.Count);

        // Pages are independent Web SDK queries. Preserve upstream order and
        // duplicates; the MCP only trims look-ahead rows to the requested limit.

        foreach (var id in firstIds.Concat(secondIds))
        {
            var entityJson = Assert.IsType<string>(await
                Genetec.Mcp.SecurityCenter.Tools.Utilities.EntitySearchTool.Execute(sc.Manager,
                    new Genetec.Mcp.SecurityCenter.Tools.Utilities.EntitySearchRequest(
                        Genetec.Mcp.SecurityCenter.Tools.Utilities.EntitySearchAction.GetEntity, Id: id)));
            using var entity = ParseJson(entityJson);
            Assert.Equal(id, GetResult(entity.RootElement).GetProperty("Guid").GetGuid());
        }
    }

    public static Guid? ExtractFirstGuid(string json)
    {
        using var document = ParseJson(json);
        var root = GetResult(document.RootElement);
        JsonElement rows;
        if (root.ValueKind == JsonValueKind.Array)
            rows = root;
        else if (root.TryGetProperty("Body", out var body))
            rows = body;
        else
            return null;

        foreach (var row in rows.EnumerateArray())
        {
            var value = row.ValueKind == JsonValueKind.Array
                ? row[0].GetString()
                : row.TryGetProperty("Guid", out var guid) ? guid.GetString() : null;
            return Guid.TryParse(value, out var parsed) ? parsed : null;
        }

        return null;
    }

    public static IReadOnlyList<Guid> ExtractGuids(string json)
    {
        using var document = ParseJson(json);
        var root = GetResult(document.RootElement);
        var rows = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("Body", out var body) && body.ValueKind == JsonValueKind.Array
                ? body
                : root.TryGetProperty("Cameras", out var cameras) && cameras.ValueKind == JsonValueKind.Array
                    ? cameras
                : default;

        if (rows.ValueKind != JsonValueKind.Array)
            return [];

        var guids = new List<Guid>();
        foreach (var row in rows.EnumerateArray())
        {
            var value = row.ValueKind == JsonValueKind.String
                ? row.GetString()
                : row.ValueKind == JsonValueKind.Array
                    ? row[0].GetString()
                    : row.TryGetProperty("Guid", out var guid) ? guid.GetString()
                    : row.TryGetProperty("Camera", out var camera) ? camera.GetString()
                    : null;
            if (Guid.TryParse(value, out var parsed))
                guids.Add(parsed);
        }

        return guids;
    }

    public static IReadOnlyList<Guid> ExtractGuids(string json, string collectionField, string identityField = "Guid")
    {
        using var document = ParseJson(json);
        var root = GetResult(document.RootElement);
        Assert.True(root.ValueKind == JsonValueKind.Object && root.TryGetProperty(collectionField, out _),
            $"Expected collection '{collectionField}' in {json}");
        var rows = root.GetProperty(collectionField);
        Assert.True(rows.ValueKind == JsonValueKind.Array, $"Expected '{collectionField}' to be an array in {json}");
        var ids = new List<Guid>();
        foreach (var row in rows.EnumerateArray())
        {
            Assert.True(row.ValueKind == JsonValueKind.String ||
                row.ValueKind == JsonValueKind.Object && row.TryGetProperty(identityField, out _),
                $"Expected GUID or '{identityField}' in {row.GetRawText()}");
            var value = row.ValueKind == JsonValueKind.String ? row.GetString() : row.GetProperty(identityField).GetString();
            Assert.True(Guid.TryParse(value, out var id), $"Expected GUID identity in {row.GetRawText()}");
            ids.Add(id);
        }
        return ids;
    }

    public static int GetAlarmInstanceId(string json)
    {
        using var document = ParseJson(json);
        return GetResult(document.RootElement).GetProperty("alarminstanceid").GetInt32();
    }

    public static bool ContainsAlarmInstance(string json, int instanceId)
    {
        using var document = ParseJson(json);
        return GetResult(document.RootElement).EnumerateArray()
            .Any(alarm => alarm.GetProperty("InstanceID").GetInt32() == instanceId);
    }

    public static async Task<(Guid Id, string Name)> CreateTrackedUserGroupAsync(ScIntegrationFixture sc)
    {
        AssertScAvailable(sc);
        var name = $"MCP_TypeGuard_{Guid.NewGuid():N}";
        var result = Assert.IsType<EntityCreateResult>(await UserGroupCreateTool.Execute(sc.Manager,
            new UserGroupCreateRequest([new UserGroupItem(name)])));
        sc.TrackForCleanup(result.Guid);
        return (result.Guid, name);
    }

    public static async Task AssertWrongEntityTypeAsync(
        ScIntegrationFixture sc, Func<Guid, Task<object>> execute,
        string parameterName = "id", string wrongType = "UserGroup")
    {
        var target = await CreateTypeGuardTargetAsync(sc, wrongType);
        await AssertEntityAsync(sc, target.Id, wrongType, target.Name);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => execute(target.Id));
        Assert.Equal(parameterName, error.ParamName);
        await AssertEntityAsync(sc, target.Id, wrongType, target.Name);
    }

    public static async Task AssertDeleteTypeGuardAsync(
        ScIntegrationFixture sc, Func<Task<EntityCreateResult>> createValid,
        Func<List<Guid>, Task<object>> delete, bool invalidFirst, string wrongType = "UserGroup")
    {
        var target = await CreateTypeGuardTargetAsync(sc, wrongType);
        await AssertEntityAsync(sc, target.Id, wrongType, target.Name);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => delete([target.Id]));
        Assert.Equal("items", error.ParamName);
        await AssertEntityAsync(sc, target.Id, wrongType, target.Name);

        var valid = await createValid();
        sc.TrackForCleanup(valid.Guid);
        var ids = invalidFirst ? new List<Guid> { target.Id, valid.Guid } : [valid.Guid, target.Id];
        var result = await delete(ids);
        AssertPartialBatchResult(result, valid.Guid, target.Id, invalidFirst);
        await AssertEntityMissingAsync(sc, valid.Guid);
        await AssertEntityAsync(sc, target.Id, wrongType, target.Name);
    }

    private static async Task<(Guid Id, string Name)> CreateTypeGuardTargetAsync(ScIntegrationFixture sc, string wrongType)
    {
        (Guid Id, string Name) target;
        if (wrongType == "UserGroup")
            target = await CreateTrackedUserGroupAsync(sc);
        else
        {
            AssertScAvailable(sc);
            var name = $"MCP_TypeGuard_{Guid.NewGuid():N}";
            var created = Assert.IsType<EntityCreateResult>(await PartitionManagementTool.Execute(sc.Manager,
                new PartitionManagementRequest(PartitionAction.Create, Items: [new PartitionManagementItem(Name: name)])));
            sc.TrackForCleanup(created.Guid);
            target = (created.Guid, name);
        }
        return target;
    }

    public static async Task AssertWrongRelatedEntityTypeAsync(
        ScIntegrationFixture sc, Guid primaryId, string primaryType,
        Func<Guid, Task<object>> execute, string parameterName, string wrongType = "UserGroup")
    {
        var (_, primaryName) = await GetEntityReadbackAsync(sc, primaryId);
        Assert.NotNull(primaryName);
        await AssertEntityAsync(sc, primaryId, primaryType, primaryName);
        await AssertWrongEntityTypeAsync(sc, execute, parameterName, wrongType);
        await AssertEntityAsync(sc, primaryId, primaryType, primaryName);
    }

    public static async Task AssertEntityAsync(
        ScIntegrationFixture sc, Guid id, string expectedType, string expectedName)
    {
        var (entity, name) = await GetEntityReadbackAsync(sc, id);
        Assert.Equal(expectedType, entity.EntityType);
        Assert.Equal(expectedName, name);
    }

    public static async Task<string> GetEntityNameAsync(
        ScIntegrationFixture sc, Guid id, string expectedType)
    {
        var (entity, name) = await GetEntityReadbackAsync(sc, id);
        Assert.Equal(expectedType, entity.EntityType);
        return name
            ?? throw new InvalidOperationException("Security Center returned an entity without a name.");
    }

    private static async Task<(EntityMetadataResponse Metadata, string? Name)> GetEntityReadbackAsync(
        ScIntegrationFixture sc, Guid id)
    {
        var json = await sc.Manager.GetClient().GetEntity(id.ToString());
        using var document = ParseJson(json);
        var entityJson = GetResult(document.RootElement);
        var entity = entityJson.Deserialize<EntityMetadataResponse>()
            ?? throw new InvalidOperationException("Security Center returned an empty entity response.");
        Assert.Equal(id, entity.Guid);
        var name = entityJson.Deserialize<EntityNameResponse>()
            ?? throw new InvalidOperationException("Security Center returned an empty entity name response.");
        return (entity, name.Name);
    }

    public static async Task AssertEntityMissingAsync(ScIntegrationFixture sc, Guid id)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            string json;
            try
            {
                json = await sc.Manager.GetClient().EntityExists(id.ToString());
            }
            catch (InvalidOperationException error) when (
                error.Message.Contains("\"SdkErrorCode\":\"UnableToRetrieveEntity\"", StringComparison.Ordinal) &&
                error.Message.Contains(id.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            using var document = ParseJson(json);
            var result = GetResult(document.RootElement).Deserialize<EntityExistsResponse>()
                ?? throw new InvalidOperationException("Security Center returned an empty entity existence response.");
            var exists = result.Value;
            if (!exists) return;
            if (attempt == 19) Assert.False(exists, $"Entity {id} still exists after deletion.");
            await Task.Delay(250);
        }
    }

    public static void AssertPartialBatchResult(object result, Guid successfulId, Guid failedId, bool invalidFirst = false)
    {
        using var document = ParseJson(Assert.IsType<string>(result));
        var batch = GetResult(document.RootElement).Deserialize<PartialBatchResponse>()
            ?? throw new InvalidOperationException("Security Center returned an empty batch response.");
        Assert.Equal(2, batch.RequestedCount);
        Assert.Equal(1, batch.SuccessCount);
        Assert.Equal(1, batch.FailureCount);
        Assert.NotNull(batch.Results);
        Assert.Equal(2, batch.Results.Count);
        var successIndex = invalidFirst ? 1 : 0;
        var failureIndex = invalidFirst ? 0 : 1;
        Assert.Equal(successfulId.ToString(), batch.Results[successIndex].Id);
        Assert.True(batch.Results[successIndex].Success);
        Assert.Equal(failedId.ToString(), batch.Results[failureIndex].Id);
        Assert.False(batch.Results[failureIndex].Success);
    }

    public static async Task AssertDeletedEntitiesAsync(ScIntegrationFixture sc, object result, IReadOnlyList<Guid> ids)
    {
        if (ids.Count == 1)
            Assert.Equal("Ok", Assert.IsType<StatusResult>(result).Status);
        else
        {
            using var document = ParseJson(Assert.IsType<string>(result));
            var batch = GetResult(document.RootElement).Deserialize<PartialBatchResponse>()
                ?? throw new InvalidOperationException("Security Center returned an empty batch response.");
            Assert.Equal(ids.Count, batch.RequestedCount);
            Assert.Equal(ids.Count, batch.SuccessCount);
            Assert.Equal(0, batch.FailureCount);
            Assert.NotNull(batch.Results);
            Assert.Equal(ids.Count, batch.Results.Count);
            for (var index = 0; index < ids.Count; index++)
            {
                Assert.Equal(ids[index].ToString(), batch.Results[index].Id);
                Assert.True(batch.Results[index].Success);
            }
        }
        foreach (var id in ids)
            await AssertEntityMissingAsync(sc, id);
    }

    private sealed record EntityExistsResponse([property: JsonRequired] bool Value);

    private sealed record EntityNameResponse(string? Name);

    private sealed record PartialBatchResponse(
        [property: JsonPropertyName("requested_count")] int RequestedCount,
        [property: JsonPropertyName("success_count")] int SuccessCount,
        [property: JsonPropertyName("failure_count")] int FailureCount,
        [property: JsonPropertyName("results")] List<PartialBatchItem> Results);

    private sealed record PartialBatchItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("success")] bool Success);
}
