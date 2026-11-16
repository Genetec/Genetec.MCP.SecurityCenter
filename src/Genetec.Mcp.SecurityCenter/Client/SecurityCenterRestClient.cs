using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Helpers;

namespace Genetec.Mcp.SecurityCenter.Client;

public class SecurityCenterRestClient
{
    public sealed record BatchCreateItem(string Name, object? Body = null, string? EntityType = null, string? InitializationMethod = null);

    private const int MaxBatchCreateItems = 50;
    private const int MaxBatchDeleteItems = 50;
    private const int MaxBatchQueryLength = 32000;

    public SecurityCenterSession Session { get; }
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly CancellationToken _invocationToken;
    private readonly SdkConcurrencyLimiter? _sdkLimiter;
    private readonly string _userId;
    private readonly CancellationTokenSource _invocationCancellation = new();

    public SecurityCenterRestClient(SecurityCenterSession session, HttpClient httpClient, ILogger? logger = null,
        CancellationToken cancellationToken = default, SdkConcurrencyLimiter? sdkLimiter = null, string userId = "__default__")
    {
        Session = session;
        _httpClient = httpClient;
        _logger = logger ?? NullLogger.Instance;
        _invocationToken = cancellationToken;
        _sdkLimiter = sdkLimiter;
        _userId = userId;
        _httpClient.Timeout = TimeSpan.FromSeconds(session.InvokeTimeoutSec);
    }

    // ── Low-level HTTP ──────────────────────────────────────────────

    internal Task<string> SendAsync(string relativePath, string method, string? body = null, CancellationToken ct = default)
        => SendInternalAsync(Session.BaseUrl + relativePath, Session.AuthHeader, method, body, ct);

    internal async Task<T> SendAsync<T>(string relativePath, string method, string? body = null, CancellationToken ct = default)
    {
        var raw = await SendInternalAsync(Session.BaseUrl + relativePath, Session.AuthHeader, method, body, ct);
        return WebSdkResponse.Deserialize<T>(raw, _jsonOptions);
    }

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions _accessPointJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DoorSidePathJsonConverter(), new JsonStringEnumConverter() }
    };

    public async Task<string> StreamEventsAsync(int timeoutSeconds, int maxEvents, CancellationToken ct = default)
    {
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, _invocationToken, _invocationCancellation.Token);
        ct = requestCancellation.Token;
        using var lease = _sdkLimiter is null ? null : await _sdkLimiter.AcquireAsync(_userId, ct);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        using var request = new HttpRequestMessage(HttpMethod.Get, Session.BaseUrl + "events");
        request.Headers.Add("Authorization", Session.AuthHeader);
        request.Headers.Add("Accept", "text/json");

        var timer = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}", null, response.StatusCode);
            }

            WebSdkLogging.LogResponse(_logger, request, response, null, timer.ElapsedMilliseconds);

            var events = new List<string>();
            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream);
            // The collection window starts once the stream is open.
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            const string boundary = "--GENETECBOUNDARY";
            var contentBuffer = new StringBuilder();
            bool inContent = false;

            while (events.Count < maxEvents)
            {
                string? line;
                try { line = await reader.ReadLineAsync(cts.Token); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { break; }

                if (line is null) break;

                if (line.StartsWith(boundary))
                {
                    if (inContent)
                    {
                        var content = contentBuffer.ToString().Trim();
                        if (!string.IsNullOrEmpty(content) && content != "KeepAlive")
                        {
                            events.Add(content);
                            WebSdkLogging.LogEvent(_logger, content);
                        }
                    }
                    contentBuffer.Clear();
                    inContent = false;
                }
                else if (!inContent)
                {
                    if (line.Length == 0)
                        inContent = true;
                }
                else
                {
                    if (contentBuffer.Length > 0) contentBuffer.AppendLine();
                    contentBuffer.Append(line);
                }
            }

            // The SDK starts each frame with a boundary; it does not send a
            // closing boundary until the next event or keep-alive. Retain the
            // final complete event when the collection window ends.
            var lastContent = contentBuffer.ToString().Trim();
            if (inContent && events.Count < maxEvents && !string.IsNullOrEmpty(lastContent) && lastContent != "KeepAlive")
            {
                try
                {
                    using var document = JsonDocument.Parse(lastContent);
                    events.Add(lastContent);
                    WebSdkLogging.LogEvent(_logger, lastContent);
                }
                catch (JsonException)
                {
                    // Cancellation can leave a partially received frame.
                }
            }

            return "[" + string.Join(",", events) + "]";
        }
        catch (OperationCanceledException exception) when (exception.InnerException is TimeoutException && !ct.IsCancellationRequested)
        {
            _invocationCancellation.Cancel();
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            WebSdkLogging.LogResponse(_logger, request, response, null, timer.ElapsedMilliseconds, exception);
            throw;
        }
        finally
        {
            response?.Dispose();
        }
    }

    private async Task<string> SendInternalAsync(string url, string authHeader, string method, string? body = null, CancellationToken ct = default, int? pageSize = null, bool includeEnvelope = false)
    {
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, _invocationToken, _invocationCancellation.Token);
        ct = requestCancellation.Token;
        ct.ThrowIfCancellationRequested();
        using var lease = _sdkLimiter is null ? null : await _sdkLimiter.AcquireAsync(_userId, ct);
        var httpMethod = new HttpMethod(method);
        using var request = new HttpRequestMessage(httpMethod, url);
        request.Headers.Add("Authorization", authHeader);
        request.Headers.Add("Accept", "text/json");

        if (body != null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }
        else if (httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put)
        {
            request.Content = new StringContent("", Encoding.UTF8);
            request.Content.Headers.ContentLength = 0;
        }

        var timer = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        string? responseBody = null;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            responseBody = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {responseBody}", null, response.StatusCode);

            var result = WebSdkResponse.Unwrap(responseBody, pageSize, includeEnvelope);
            WebSdkLogging.LogResponse(_logger, request, response, responseBody, timer.ElapsedMilliseconds);
            return result;
        }
        catch (OperationCanceledException exception) when (exception.InnerException is TimeoutException && !ct.IsCancellationRequested)
        {
            _invocationCancellation.Cancel();
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            WebSdkLogging.LogResponse(_logger, request, response, responseBody, timer.ElapsedMilliseconds, exception);
            throw;
        }
        finally
        {
            response?.Dispose();
        }
    }

    // ── Entity CRUD ─────────────────────────────────────────────────

    /// <summary>GET /entity/{id} – all properties</summary>
    public Task<string> GetEntity(string id)
        => SendAsync($"entity/{id}", "GET");

    public async Task RequireEntityType(Guid id, string parameterName, params string[] expectedTypes)
    {
        if (id == Guid.Empty)
            throw new ArgumentException($"Parameter '{parameterName}' must identify an existing entity.", parameterName);
        var json = await GetEntityFields(id.ToString(), "Guid", "EntityType");
        var entity = ReadEntityMetadata<EntityMetadataResponse>(json);
        ValidateEntityType(entity, id, parameterName, expectedTypes);
    }

    public async Task<string> GetEntity(Guid id, string expectedType)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Parameter 'id' must identify an existing entity.", "id");
        var json = await GetEntity(id.ToString());
        var entity = ReadEntityMetadata<EntityMetadataResponse>(json);
        ValidateEntityType(entity, id, "id", expectedType);
        return json;
    }

    public async Task<string> GetEntityTrimmed(Guid id, string expectedType)
        => StripEntityNoise(await GetEntity(id, expectedType));

    private static T ReadEntityMetadata<T>(string json) where T : EntityMetadataResponse
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, _jsonOptions)
                ?? throw new InvalidOperationException("Security Center returned invalid entity identity or type metadata.");
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException("Security Center returned invalid entity identity or type metadata.", error);
        }
    }

    private static void ValidateEntityType(EntityMetadataResponse entity, Guid id, string parameterName, params string[] expectedTypes)
    {
        if (entity.Guid != id || string.IsNullOrEmpty(entity.EntityType))
            throw new InvalidOperationException("Security Center returned invalid entity identity or type metadata.");

        if (!expectedTypes.Contains(entity.EntityType, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Parameter '{parameterName}' must identify {string.Join(" or ", expectedTypes)}; the entity is {entity.EntityType}.", parameterName);
    }

    public async Task<string> GetDoorAccessPoint(Guid doorId, Guid accessPointId, string parameterName, bool requireDoorSide = false)
    {
        var doorJson = await GetEntityFields(doorId.ToString(), "Guid", "EntityType", "DoorSides");
        var door = ReadEntityMetadata<DoorMetadataResponse>(doorJson);
        ValidateEntityType(door, doorId, "id", "Door");

        var accessPointJson = await GetEntity(accessPointId.ToString());
        var point = ReadEntityMetadata<AccessPointMetadataResponse>(accessPointJson);
        ValidateEntityType(point, accessPointId, parameterName, "AccessPoint");
        if (point.Door != doorId)
            throw new ArgumentException($"Parameter '{parameterName}' must identify an access point belonging to door '{doorId}'.", parameterName);

        if (requireDoorSide && door.DoorSides?.Contains(accessPointId) != true)
            throw new ArgumentException($"Parameter '{parameterName}' must identify a side of door '{doorId}'.", parameterName);

        return accessPointJson;
    }

    /// <summary>
    /// Resolves a card-reader access point to its associated Reader device.
    /// The Web SDK reader configuration methods are declared on Reader, not AccessPoint.
    /// </summary>
    public async Task<Guid> ResolveReaderDeviceId(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Parameter 'id' must identify a reader device or card-reader access point.", "id");
        var raw = await GetEntity(id.ToString());
        var metadata = ReadEntityMetadata<EntityMetadataResponse>(raw);
        ValidateEntityType(metadata, id, "id", "Device", "AccessPoint");
        var readerId = id;
        if (string.Equals(metadata.EntityType, "AccessPoint", StringComparison.OrdinalIgnoreCase))
        {
            var point = ReadEntityMetadata<ReaderAccessPointMetadataResponse>(raw);
            if (point.AccessPointType != AccessPointType.CardReader)
                throw new ArgumentException("Parameter 'id' must identify a card-reader access point.", "id");
            if (point.Device is null || point.Device == Guid.Empty)
                throw new InvalidOperationException(
                    $"Access point '{id}' has no associated reader device. Assign a physical reader before configuring card and PIN.");
            readerId = point.Device.Value;
            raw = await GetEntity(readerId.ToString());
        }
        var reader = ReadEntityMetadata<DeviceMetadataResponse>(raw);
        ValidateEntityType(reader, readerId, "id", "Device");
        if (reader.DeviceType != DeviceType.Reader)
            throw new ArgumentException("Parameter 'id' must identify a Reader device or an access point linked to a Reader device.", "id");
        return readerId;
    }

    /// <summary>GET /entity/{id} with privilege arrays stripped for LLM-friendly output</summary>
    public async Task<string> GetEntityTrimmed(string id)
    {
        var raw = await GetEntity(id);
        return StripEntityNoise(raw);
    }

    /// <summary>Removes privilege arrays and other LLM-unhelpful bloat from entity JSON.</summary>
    public static string StripEntityNoise(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return json;

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);
            WriteStrippedObject(doc.RootElement, writer);
            writer.Flush();
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch
        {
            return json;
        }
    }

    private static void WriteStrippedObject(JsonElement element, Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.EndsWith("Privileges", StringComparison.OrdinalIgnoreCase))
                continue;
            writer.WritePropertyName(property.Name);
            WriteStrippedValue(property.Value, writer);
        }
        writer.WriteEndObject();
    }

    private static void WriteStrippedValue(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WriteStrippedObject(element, writer);
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteStrippedValue(item, writer);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    /// <summary>GET /entity?q=entity={id},Field1,Field2</summary>
    public Task<string> GetEntityFields(string id, params string[] fields)
    {
        ResourceLimits.EnsureBatchOperationCount(fields.Length, "fields");
        var fieldList = string.Join(",", fields);
        return SendAsync(BuildQueryPath("entity", $"entity={id},{fieldList}"), "GET");
    }

    /// <summary>POST /entity?q=entity=NewEntity(Type),Name=...,Guid</summary>
    public Task<string> CreateEntity(string entityType, string name, object? body = null)
    {
        var segment = BuildCreateEntityQuerySegment(entityType, name, body);
        return SendAsync(BuildQueryPath("entity", segment), "POST");
    }

    /// <summary>POST /entity?q=entity=NewEntity(Type),Name=...,Guid — typed response variant</summary>
    public Task<T> CreateEntity<T>(string entityType, string name, object? body = null, string? initializationMethod = null)
    {
        var segment = BuildCreateEntityQuerySegment(entityType, name, body, initializationMethod);
        return SendAsync<T>(BuildQueryPath("entity", segment), "POST");
    }

    /// <summary>POST /entity?q=entity=NewEntity(...),...,Guid,entity=NewEntity(...),...,Guid</summary>
    public Task<string> CreateEntitiesBatch(string entityType, IReadOnlyList<BatchCreateItem> items)
    {
        if (items.Count == 0)
            throw new ArgumentException("Batch create requires at least one item.");

        if (items.Count > MaxBatchCreateItems)
            throw new ArgumentException($"Batch create supports up to {MaxBatchCreateItems} items per request.");

        var segments = new List<string>(items.Count);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
                throw new ArgumentException("Every batch item requires a non-empty 'Name'.");

            var itemEntityType = string.IsNullOrWhiteSpace(item.EntityType) ? entityType : item.EntityType;
            segments.Add(BuildCreateEntityQuerySegment(itemEntityType, item.Name, item.Body, item.InitializationMethod));
        }

        var path = BuildQueryPath("entity", string.Join(",", segments));
        if (path.Length - "entity?q=".Length > MaxBatchQueryLength)
            throw new ArgumentException($"Batch request is too large (max query length {MaxBatchQueryLength}).");

        return SendAsync(path, "POST");
    }

    public Task<string> UpdateEntity(string id, object body)
        => UpdateEntity(id, JsonSerializer.Serialize(body));

    public Task<string> UpdateEntitiesBatch(IReadOnlyList<(string Id, object Body)> items)
        => UpdateEntitiesBatchCore(items.Select(i => (i.Id, JsonSerializer.Serialize(i.Body))).ToList(), null);

    public Task<string> UpdateEntitiesBatch(IReadOnlyList<(Guid Id, object Body)> items, string? expectedType = null,
        Func<int, Task>? validateItem = null)
        => UpdateEntitiesBatchCore(
            items.Select(i => (i.Id, JsonSerializer.Serialize(i.Body))).ToList(),
            expectedType is null ? null : id => RequireEntityType(id, "items", expectedType), validateItem);

    /// <summary>POST /entity?q=entity={id},Prop1=Val1,... – update from JSON body</summary>
    public Task<string> UpdateEntity(string id, string jsonBody)
    {
        var parts = new List<string> { $"entity={id}" };
        foreach (var (key, value) in JsonToQueryParams(jsonBody, omitEntitySelector: true))
        {
            parts.Add($"{key}={EscapeQueryValue(value)}");
        }
        return SendAsync(BuildQueryPath("entity", string.Join(",", parts)), "POST");
    }

    /// <summary>DELETE /entity/{id}</summary>
    public Task<string> DeleteEntity(Guid id)
        => SendAsync($"entity/{id:D}", "DELETE");

    /// <summary>DELETE /entity/{id} — typed response variant</summary>
    public Task<T> DeleteEntity<T>(Guid id)
        => SendAsync<T>($"entity/{id:D}", "DELETE");

    /// <summary>Update multiple entities in parallel. Each item must have an 'id' and 'body' (JSON).</summary>
    public Task<string> UpdateEntitiesBatch(IReadOnlyList<(string Id, string Body)> items)
        => UpdateEntitiesBatchCore(items, null);

    public Task<string> UpdateEntitiesBatch(IReadOnlyList<(Guid Id, string Body)> items, string? expectedType = null)
        => UpdateEntitiesBatchCore(items,
            expectedType is null ? null : id => RequireEntityType(id, "items", expectedType));

    private async Task<string> UpdateEntitiesBatchCore<TId>(
        IReadOnlyList<(TId Id, string Body)> items,
        Func<TId, Task>? validateId,
        Func<int, Task>? validateItem = null)
    {
        if (items.Count == 0)
            throw new ArgumentException("Batch update requires at least one item.");

        if (items.Count > MaxBatchCreateItems)
            throw new ArgumentException($"Batch update supports up to {MaxBatchCreateItems} items per request.");

        var updateTasks = items
            .Select(async (item, index) =>
            {
                var id = $"{item.Id}";
                try
                {
                    if (validateId is not null)
                        await validateId(item.Id);
                    if (validateItem is not null)
                        await validateItem(index);
                    var response = await UpdateEntity(id, item.Body);
                    return new { index, id, response = (string?)response, error = (string?)null };
                }
                catch (Exception ex) { return new { index, id, response = (string?)null, error = (string?)ex.Message }; }
            })
            .ToArray();

        var rawResults = await Task.WhenAll(updateTasks);
        var results = new List<object>(items.Count);
        var successCount = 0;

        foreach (var item in rawResults.OrderBy(r => r.index))
        {
            if (item.error is not null)
            {
                results.Add(new { index = item.index, success = false, id = item.id, error = item.error });
                continue;
            }
            results.Add(new { index = item.index, success = true, id = item.id, response = item.response });
            successCount++;
        }

        return JsonSerializer.Serialize(new
        {
            mode = "batch",
            requested_count = items.Count,
            success_count = successCount,
            failure_count = Math.Max(0, items.Count - successCount),
            results
        });
    }

    /// <summary>DELETE multiple entities by fan-out across independent delete requests.</summary>
    public Task<string> DeleteEntitiesBatch(IReadOnlyList<Guid> ids, string? expectedType = null)
        => DeleteEntitiesBatchCore(ids,
            expectedType is null ? null : id => RequireEntityType(id, "items", expectedType));

    private async Task<string> DeleteEntitiesBatchCore(
        IReadOnlyList<Guid> ids,
        Func<Guid, Task>? validateId)
    {
        if (ids.Count == 0)
            throw new ArgumentException("Batch delete requires at least one item.");

        if (ids.Count > MaxBatchDeleteItems)
            throw new ArgumentException($"Batch delete supports up to {MaxBatchDeleteItems} items per request.");

        var deleteTasks = ids
            .Select(async (entityId, index) =>
            {
                var id = entityId.ToString("D");
                try
                {
                    if (validateId is not null)
                        await validateId(entityId);
                    var response = await DeleteEntity(entityId);
                    return new { index, id, response = (string?)response, error = (string?)null };
                }
                catch (Exception ex) { return new { index, id, response = (string?)null, error = (string?)ex.Message }; }
            })
            .ToArray();

        var rawResults = await Task.WhenAll(deleteTasks);
        var results = new List<object>(ids.Count);
        var successCount = 0;

        foreach (var item in rawResults.OrderBy(r => r.index))
        {
            if (item.error is not null)
            {
                results.Add(new { index = item.index, success = false, id = item.id, error = item.error });
                continue;
            }
            results.Add(new { index = item.index, success = true, id = item.id, response = item.response });
            successCount++;
        }

        return JsonSerializer.Serialize(new
        {
            mode = "batch",
            requested_count = ids.Count,
            success_count = successCount,
            failure_count = Math.Max(0, ids.Count - successCount),
            results
        });
    }

    /// <summary>GET /entity/exists/{id}</summary>
    public Task<string> EntityExists(string id)
        => SendAsync($"entity/exists/{id}", "GET");

    /// <summary>GET /entity?q=entity=LogicalID({entityType},{logicalId}),Name,EntityType</summary>
    public Task<string> GetEntityByLogicalId(string entityType, string logicalId)
        => SendAsync(BuildQueryPath("entity", $"entity=LogicalID({EscapeQueryValue(entityType)},{EscapeQueryValue(logicalId)}),Name,EntityType"), "GET");

    public static bool TryGetErrorMessage(string rawResult, out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            using var doc = JsonDocument.Parse(rawResult);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("error", out var errorElement) &&
                errorElement.ValueKind == JsonValueKind.True)
            {
                errorMessage = doc.RootElement.TryGetProperty("message", out var messageElement) &&
                               messageElement.ValueKind == JsonValueKind.String
                    ? messageElement.GetString()
                    : rawResult;
                return true;
            }
        }
        catch
        {
            // Treat non-JSON or empty responses as success.
        }

        return false;
    }

    // ── Entity Field & Collection Operations ────────────────────────

    /// <summary>GET /entity?q=entity={id},{field}</summary>
    public Task<string> GetEntityField(string entityId, string field)
        => SendAsync(BuildQueryPath("entity", $"entity={entityId},{field}"), "GET");

    /// <summary>POST /entity?q=entity={id},{field}={value}</summary>
    public Task<string> SetEntityField(string entityId, string field, string value)
        => SendAsync(BuildQueryPath("entity", $"entity={entityId},{field}={EscapeQueryValue(value)}"), "POST");

    public Task<string> SetAlarmRecordingTime(Guid alarmId, int seconds)
        => SetEntityField(alarmId.ToString(), "AlarmRecordingTime",
            TimeSpan.FromSeconds(seconds).ToString("c", CultureInfo.InvariantCulture));

    /// <summary>POST /entity?q=entity={id},{field}@{valueGuid}</summary>
    public Task<string> AddToCollection(string entityId, string field, string valueGuid)
        => SendAsync(BuildQueryPath("entity", $"entity={entityId},{field}@{valueGuid}"), "POST");

    /// <summary>POST /entity?q=entity={id},{field}-{valueGuid}</summary>
    public Task<string> RemoveFromCollection(string entityId, string field, string valueGuid)
        => SendAsync(BuildQueryPath("entity", $"entity={entityId},{field}-{valueGuid}"), "POST");

    /// <summary>GET /Entities/{id}/{relation}.</summary>
    public Task<string> GetEntityRelation(string entityId, string relation)
        => SendAsync($"Entities/{entityId}/{relation}", "GET");

    /// <summary>POST /Entities/{id}/{relation} with a related entity id.</summary>
    public Task<string> SetEntityRelation(string entityId, string relation, string body)
        => SendAsync($"Entities/{entityId}/{relation}", "POST", body);

    public Task<string> SetEntityRelation(string entityId, string relation, object body)
        => SetEntityRelation(entityId, relation, JsonSerializer.Serialize(body));

    /// <summary>DELETE /Entities/{id}/{relation}/{relatedId}.</summary>
    public Task<string> RemoveEntityRelation(string entityId, string relation, string relatedId)
        => SendAsync($"Entities/{entityId}/{relation}/{relatedId}", "DELETE");

    public async Task<string> AddAlarmAttachedEntity(Guid alarmId, Guid entityId)
    {
        var entityIds = await GetAlarmAttachedEntityIds(alarmId);
        if (!entityIds.Contains(entityId))
            entityIds.Add(entityId);

        return await SetAlarmAttachedEntities(alarmId, entityIds);
    }

    public async Task<string> RemoveAlarmAttachedEntity(Guid alarmId, Guid entityId)
    {
        var entityIds = await GetAlarmAttachedEntityIds(alarmId);
        entityIds.RemoveAll(id => id == entityId);

        return await SetAlarmAttachedEntities(alarmId, entityIds);
    }

    private async Task<List<Guid>> GetAlarmAttachedEntityIds(Guid alarmId)
    {
        var response = await SendAsync<AlarmAttachedEntitiesResponse>(
            BuildQueryPath("entity", $"entity={alarmId},AttachedEntities"), "GET");
        return response.AttachedEntities
            ?? throw new InvalidOperationException("Security Center returned a null attached-entity collection.");
    }

    private Task<string> SetAlarmAttachedEntities(Guid alarmId, IReadOnlyCollection<Guid> entityIds)
        => SetEntityField(alarmId.ToString(), "AttachedEntities", $"{{{string.Join(',', entityIds)}}}");

    /// <summary>POST /entity?q=entity={alarmId},Recipients.Add({recipientGuid},0)</summary>
    public Task<string> AddAlarmRecipient(string alarmId, string recipientGuid, int delaySeconds = 0)
        => ExecuteEntityMethod(alarmId, "Recipients.Add", $"{recipientGuid},{delaySeconds}");

    /// <summary>POST /entity?q=entity={alarmId},Recipients.Remove({recipientGuid})</summary>
    public Task<string> RemoveAlarmRecipient(string alarmId, string recipientGuid)
        => ExecuteEntityMethod(alarmId, "Recipients.Remove", recipientGuid);

    /// <summary>POST /entity?q=entity={id},{method}({args})</summary>
    public Task<string> ExecuteEntityMethod(string entityId, string method, string? args = null)
    {
        var methodCall = args != null ? $"{method}({args})" : $"{method}()";
        return SendAsync(BuildQueryPath("entity", $"entity={entityId},{methodCall}"), "POST");
    }

    public Task<string> ExecuteEntityMethod(string entityId, string method, bool argument)
        => ExecuteEntityMethod(entityId, method, argument ? "true" : "false");

    public Task<string> GetAssuranceProfiles()
        => ExecuteEntityMethod("00000000-0000-0000-0000-000000000007", "GetAssuranceProfiles");

    public Task<string> SynchronizeActiveDirectory(Guid roleId, bool synchronizeImages)
        => ExecuteEntityMethod(roleId.ToString(), "Synchronize", synchronizeImages);

    public async Task<string> ExecuteDoorSideMethod(
        Guid doorId,
        Guid accessPointId,
        DoorSideMethod method,
        Guid argument)
    {
        var accessPointJson = await GetDoorAccessPoint(doorId, accessPointId, "access_point_id");
        var argumentType = method switch
        {
            DoorSideMethod.AddCardholder or DoorSideMethod.RemoveCardholder => "Cardholder",
            DoorSideMethod.AddCardholderGroup or DoorSideMethod.RemoveCardholderGroup => "CardholderGroup",
            DoorSideMethod.SetDevice => "Device",
            _ => throw new ArgumentException("Unknown door side method.", nameof(method))
        };
        var argumentName = method switch
        {
            DoorSideMethod.AddCardholder or DoorSideMethod.RemoveCardholder => "cardholder_id",
            DoorSideMethod.AddCardholderGroup or DoorSideMethod.RemoveCardholderGroup => "cardholder_group_id",
            DoorSideMethod.SetDevice => "device_id",
            _ => throw new ArgumentException("Unknown door side method.", nameof(method))
        };
        if (method != DoorSideMethod.SetDevice || argument != Guid.Empty)
            await RequireEntityType(argument, argumentName, argumentType);
        var accessPoint = JsonSerializer.Deserialize<AccessPointSideResponse>(accessPointJson, _accessPointJsonOptions)
            ?? throw new InvalidOperationException("The access point response was empty.");

        _logger.LogDebug("Access point side: {AccessPointSide}", accessPoint.AccessPointSide);


        if (method == DoorSideMethod.SetDevice)
        {

            var component = accessPoint.AccessPointType switch
            {
                AccessPointType.CardReader => DoorSideComponent.Reader,
                AccessPointType.Rex => DoorSideComponent.Rex,
                AccessPointType.EntrySensor => DoorSideComponent.EntrySensor,
                _ => throw new ArgumentException(
                    $"Access point type '{accessPoint.AccessPointType}' cannot be assigned a device through a door side.",
                    nameof(accessPointId))
            };

            _logger.LogDebug("Method {AccessPointSide}.{Component}.SetDevice called with argument {Argument}", accessPoint.AccessPointSide, component, argument);
            return await SetEntityField(
                doorId.ToString(), $"{accessPoint.AccessPointSide}.{component}.Device", argument.ToString());
        }

        var entityId = doorId.ToString();
        var methodName = $"{accessPoint.AccessPointSide}.{method}";
        _logger.LogDebug("Executing door side method: {Method} on entity {EntityId} with argument {Argument}", methodName, entityId, argument);

        return await ExecuteEntityMethod(entityId, methodName, argument.ToString());
    }

    /// <summary>Invoke the same entity method on multiple entities, preserving each item's result.</summary>
    public Task<BatchMethodResult> ExecuteEntityMethodBatch(
        string method,
        IReadOnlyList<(Guid Id, string? Args)> items,
        Func<int, Task>? validateItem = null)
        => ExecuteEntityMethodBatchCore(method, items, validateItem, includeArguments: false);

    private async Task<BatchMethodResult> ExecuteEntityMethodBatchCore(
        string method,
        IReadOnlyList<(Guid Id, string? Args)> items,
        Func<int, Task>? validateItem,
        bool includeArguments)
    {
        if (items.Count == 0)
            throw new ArgumentException("Batch method call requires at least one item.", "items");
        ResourceLimits.EnsureBatchOperationCount(items.Count, "items");

        var tasks = items.Select(async (item, index) =>
        {
            Guid? id = includeArguments ? null : item.Id;
            var arg = includeArguments ? item.Args : null;
            try
            {
                if (validateItem is not null)
                    await validateItem(index);
                var response = await ExecuteEntityMethod(item.Id.ToString(), method, item.Args);
                return new BatchMethodResultItem(index, true, id, arg, Response: response);
            }
            catch (Exception ex)
            {
                return new BatchMethodResultItem(index, false, id, arg, Error: ex.Message);
            }
        });
        var results = (await Task.WhenAll(tasks)).ToList();
        var successCount = results.Count(result => result.Success);
        return new BatchMethodResult("batch", items.Count, successCount, items.Count - successCount,
            results, Method: includeArguments ? method : null);
    }

    /// <summary>Get specific fields for multiple entities in parallel.</summary>
    public async Task<string> GetEntityFieldsBatch(IReadOnlyList<(string Id, List<string> Fields)> items)
    {
        foreach (var item in items)
            ResourceLimits.EnsureBatchOperationCount(item.Fields.Count, "fields");
        if (items.Count == 0)
            throw new ArgumentException("Batch get_fields requires at least one item.");

        if (items.Count > MaxBatchDeleteItems)
            throw new ArgumentException($"Batch get_fields supports up to {MaxBatchDeleteItems} items per request.");

        var tasks = items
            .Select(async (item, index) =>
            {
                try
                {
                    var data = await GetEntityFields(item.Id, [.. item.Fields]);
                    return new { index, id = item.Id, data = (string?)data, error = (string?)null };
                }
                catch (Exception ex) { return new { index, id = item.Id, data = (string?)null, error = (string?)ex.Message }; }
            })
            .ToArray();

        var rawResults = await Task.WhenAll(tasks);
        var results = new List<object>(items.Count);
        var successCount = 0;

        foreach (var item in rawResults.OrderBy(r => r.index))
        {
            if (item.error is not null)
            {
                results.Add(new { index = item.index, success = false, id = item.id, error = item.error });
                continue;
            }
            results.Add(new { index = item.index, success = true, id = item.id, data = item.data });
            successCount++;
        }

        return JsonSerializer.Serialize(new
        {
            mode = "batch",
            requested_count = items.Count,
            success_count = successCount,
            failure_count = Math.Max(0, items.Count - successCount),
            results
        });
    }

    /// <summary>Execute a method on one entity with multiple arguments, preserving each item's result.</summary>
    public Task<BatchMethodResult> ExecuteEntityMethodBatchOnSingleEntity(
        Guid entityId,
        string method,
        IReadOnlyList<string> argsList,
        Func<int, Task>? validateItem = null)
        => ExecuteEntityMethodBatchCore(method,
            argsList.Select(args => (entityId, (string?)args)).ToList(), validateItem, includeArguments: true);

    // ── Entity Listing & Search ─────────────────────────────────────

    /// <summary>Fetch a bounded page of entities through the EntityConfiguration report.</summary>
    public Task<string> ListEntities(string entityType, int pageSize = 50, int page = 1, string[]? returnFields = null, bool includeEnvelope = false)
        => ListEntities([entityType], pageSize, page, returnFields, includeEnvelope);

    /// <summary>Fetch a bounded page matching multiple entity types in one query.</summary>
    public async Task<string> ListEntities(IReadOnlyList<string> entityTypes, int pageSize = 50, int page = 1, string[]? returnFields = null, bool includeEnvelope = false)
    {
        var parts = new List<string> { "EntityTypes@" + string.Join("@", entityTypes) };
        if (returnFields is { Length: > 0 })
            parts.Add("ReturnFields@" + string.Join("@", returnFields));

        try
        {
            // Page=1 selects the same ordered paging query used by later pages.
            return await RunPagedReport("EntityConfiguration", string.Join(",", parts), pageSize, page, includeEnvelope);
        }
        catch (Exception ex) when (returnFields is { Length: > 0 } && ex.Message.Contains("ReturnFields"))
        {
            // Older SC versions don't support ReturnFields — retry without it
            var fallbackParts = parts.Where(p => !p.StartsWith("ReturnFields")).ToList();
            return await RunPagedReport("EntityConfiguration", string.Join(",", fallbackParts), pageSize, page, includeEnvelope);
        }
    }

    /// <summary>Search entities by name/type via EntityConfiguration report</summary>
    public Task<string> SearchEntities(string? name = null, string? entityType = null, string? nameSearchMode = null, int pageSize = 50, int page = 1)
        => SearchEntities(name, entityType is null ? null : [entityType], nameSearchMode, pageSize, page);

    /// <summary>Search entities matching any of the given types (and optionally a name) in one query.</summary>
    public Task<string> SearchEntities(string? name = null, IReadOnlyList<string>? entityTypes = null, string? nameSearchMode = null, int pageSize = 50, int page = 1)
    {
        var parts = new List<string>();
        if (entityTypes is { Count: > 0 }) parts.Add("EntityTypes@" + string.Join("@", entityTypes));
        if (name != null)
        {
            parts.Add($"Name={EscapeQueryValue(name)}");
            parts.Add($"NameSearchMode={nameSearchMode ?? "Contains"}");
        }
        return RunPagedReport("EntityConfiguration", string.Join(",", parts), pageSize, page);
    }

    // ── Reports ─────────────────────────────────────────────────────

    /// <summary>GET /report/{reportType}?q={query}</summary>
    public Task<string> RunReport(string reportType, string query)
        => SendAsync(BuildQueryPath($"report/{reportType}", query), "GET");

    /// <summary>Fetch one report page and omit the Web SDK's look-ahead rows while preserving incomplete response status.</summary>
    public Task<string> RunPagedReport(string reportType, string query, int? pageSize = null, int? page = null, bool includeEnvelope = false)
    {
        pageSize ??= ResourceLimits.DefaultPageSize;
        page ??= 1;
        if (pageSize is < 1 or > ResourceLimits.MaxPageSize)
            throw new ArgumentException("page_size must be between 1 and 100.", "page_size");
        if (page is < 1)
            throw new ArgumentException("page must be greater than zero.", "page");

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(query)) parts.Add(query);
        if (pageSize is not null) parts.Add($"PageSize={pageSize}");
        if (page is not null) parts.Add($"Page={page}");
        return SendInternalAsync(Session.BaseUrl + BuildQueryPath($"report/{reportType}", string.Join(",", parts)),
            Session.AuthHeader, "GET", pageSize: pageSize, includeEnvelope: includeEnvelope);
    }

    // ── Credentials ─────────────────────────────────────────────────

    /// <summary>Search CredentialConfiguration by custom format or exact custom field values.</summary>
    public Task<string> SearchCredentialsByCustomFormat(
        string formatId,
        IReadOnlyDictionary<string, string>? customValues,
        int pageSize,
        int page)
    {
        var filter = customValues is null
            ? $"FormatType@{EscapeQueryValue(formatId)}"
            : $"UniqueIds@{BuildCustomCredentialFormat(formatId, customValues)}";
        return RunPagedReport("CredentialConfiguration", filter, pageSize, page);
    }

    public static string BuildCustomCredentialFormat(string formatId, IReadOnlyDictionary<string, string>? values)
    {
        var pairs = values?.Select(pair => $"{{{EscapeQueryValue(pair.Key)},{EscapeQueryValue(pair.Value)}}}")
            ?? [];
        return $"CustomCredentialFormat({EscapeQueryValue(formatId)},{{{string.Join(",", pairs)}}})";
    }

    // ── Custom Fields ───────────────────────────────────────────────

    /// <summary>GET /customField/{entityId}/{fieldName}</summary>
    public Task<string> GetCustomFieldValue(string entityId, string fieldName)
        => SendAsync($"customField/{entityId}/{EscapeCustomFieldPathSegment(fieldName)}", "GET");

    /// <summary>PUT /customField/{entityId}/{fieldName}/{value}</summary>
    public Task<string> SetCustomFieldValue(string entityId, string fieldName, string value)
        => SendAsync($"customField/{entityId}/{EscapeCustomFieldPathSegment(fieldName)}/{Uri.EscapeDataString(value)}", "PUT");

    /// <summary>POST /customField/{entityType}/{fieldName}/{dataType}/{defaultValue}</summary>
    public Task<string> CreateCustomField(CustomFieldEntityType entityType, string fieldName, CustomFieldDataType dataType, string? defaultValue = null)
        => SendAsync($"customField/{entityType}/{EscapeCustomFieldPathSegment(fieldName)}/{dataType}/{Uri.EscapeDataString(defaultValue ?? "")}", "POST");

    /// <summary>DELETE /customField/{entityType}/{fieldName}</summary>
    public Task<string> DeleteCustomField(CustomFieldEntityType entityType, string fieldName)
        => SendAsync($"customField/{entityType}/{EscapeCustomFieldPathSegment(fieldName)}", "DELETE");

    // ── Events ──────────────────────────────────────────────────────

    /// <summary>GET /events/subscribed</summary>
    public Task<string> GetEventSubscriptions()
        => SendAsync("events/subscribed", "GET");

    /// <summary>GET /events/subscribe?q=event(entityId,Type),...</summary>
    public Task<string> SubscribeToEvents(string entityId, IEnumerable<ScEventType> eventTypes)
    {
        var boundedTypes = eventTypes.Take(ResourceLimits.MaxBatchItems + 1).ToArray();
        ResourceLimits.EnsureBatchOperationCount(boundedTypes.Length, "event_types");
        var query = string.Join(",", boundedTypes.Select(t => $"event({entityId},{t})"));
        return SendAsync(BuildQueryPath("events/subscribe", query), "GET");
    }

    /// <summary>GET /events/unsubscribe?q=event(entityId,Type),...</summary>
    public Task<string> UnsubscribeFromEvents(string entityId, IEnumerable<ScEventType> eventTypes)
    {
        var boundedTypes = eventTypes.Take(ResourceLimits.MaxBatchItems + 1).ToArray();
        ResourceLimits.EnsureBatchOperationCount(boundedTypes.Length, "event_types");
        var query = string.Join(",", boundedTypes.Select(t => $"event({entityId},{t})"));
        return SendAsync(BuildQueryPath("events/unsubscribe", query), "GET");
    }

    /// <summary>POST /events/RaiseEvent/{eventType}/{entityId}</summary>
    public Task<string> RaiseEvent(ScRaisableEventType eventType, string entityId)
        => SendAsync($"events/RaiseEvent/{eventType}/{entityId}", "POST");

    /// <summary>POST /events/RaiseCustomEvent/{id}/{entityId}[/{message}]</summary>
    public Task<string> RaiseCustomEvent(int customEventId, string entityId, string? message = null)
    {
        var path = message is not null
            ? $"events/RaiseCustomEvent/{customEventId}/{entityId}/{Uri.EscapeDataString(message)}"
            : $"events/RaiseCustomEvent/{customEventId}/{entityId}";
        return SendAsync(path, "POST");
    }

    // ── Alarms ──────────────────────────────────────────────────────

    /// <summary>GET /activealarms</summary>
    public Task<string> GetActiveAlarms()
        => SendAsync("activealarms", "GET");

    /// <summary>GET /alarm?q={query} – alarm workflow operations</summary>
    public Task<string> AlarmAction(string query)
        => SendAsync(BuildQueryPath("alarm", query), "GET");

    public Task<string> ForwardAlarm(int instanceId, IReadOnlyCollection<Guid> recipientIds)
    {
        ResourceLimits.EnsureBatchOperationCount(recipientIds.Count, "recipient_ids");
        return AlarmAction($"ForwardAlarm({instanceId},{{{string.Join(',', recipientIds)}}})");
    }

    // ── Actions ─────────────────────────────────────────────────────

    /// <summary>POST /action?q={actionQuery}</summary>
    public Task<string> ExecuteAction(string actionQuery)
        => SendAsync(BuildQueryPath("action", actionQuery), "POST");

    // ── Thumbnails ──────────────────────────────────────────────────

    /// <summary>Fetch thumbnails for one or more cameras. Returns JSON with base64 image data.</summary>
    public async Task<string> GetThumbnails(IReadOnlyCollection<Guid> cameraIds, string timeStart, string timeEnd, int width = 640, int count = 1)
    {
        if (cameraIds.Count is < 1 or > 10)
            throw new ArgumentException("camera_ids must contain 1 to 10 cameras.", "camera_ids");
        if (count < 1)
            throw new ArgumentException("count must be positive.", "count");
        if ((long)cameraIds.Count * count > 50)
            throw new ArgumentException("camera_ids and count must request at most 50 images in total.", "count");
        if (width < 1)
            throw new ArgumentException("width must be positive.", "width");
        if (!DateTimeOffset.TryParse(timeStart, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var start) ||
            !DateTimeOffset.TryParse(timeEnd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var end) || start > end)
            throw new ArgumentException("time_start must be no later than time_end and both must be valid timestamps.", "time_start");

        // Web SDK Thumbnail report uses chained AddTimestampRange calls for multiple cameras:
        // q=AddTimestampRange(cam1,start,end,count,width),AddTimestampRange(cam2,start,end,count,width)
        var query = string.Join(",", cameraIds.Select(id =>
            $"AddTimestampRange({id},{timeStart},{timeEnd},{count},{width})"));

        return await SendInternalAsync(Session.BaseUrl + BuildQueryPath("report/Thumbnail", query), Session.AuthHeader, "GET");
    }

    // ── Connection Test ─────────────────────────────────────────────

    /// <summary>GET / – test if Web SDK is online</summary>
    public Task<string> TestConnection()
        => SendAsync("", "GET");

    // ── Helpers ─────────────────────────────────────────────────────


    internal static Dictionary<string, string> JsonToQueryParams(string json, bool omitEntitySelector = false)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                // Typed entity update records carry Id for the MCP entity selector.
                // The Web SDK receives that selector separately as entity={id}; Id
                // is not a writable entity property and must not be sent as a filter.
                if (omitEntitySelector && prop.Name.Equals("Id", StringComparison.OrdinalIgnoreCase)) continue;
                if (prop.Value.ValueKind == JsonValueKind.Null) continue;

                var value = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString()!,
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    _ => prop.Value.GetRawText()
                };
                result[prop.Name] = value;
            }
        }
        catch
        {
            // If JSON parsing fails, return empty
        }
        return result;
    }

    public static string EscapeQueryValue(string value)
    {
        return value
            .Replace(@"\", @"\\")
            .Replace(",", @"\,")
            .Replace("@", @"\@")
            .Replace("(", @"\(")
            .Replace(")", @"\)");
    }

    /// <summary>POST /entity?q=entity=NewEntity(Credential),Format=ClassName(args),Name=...,Guid</summary>
    public Task<EntityCreateResult> CreateCredentialByPath(string formatString, string name)
    {
        var parts = new List<string>
        {
            "entity=NewEntity(Credential)",
            $"Format={EscapeQueryValue(formatString)}",
            $"Name={EscapeQueryValue(name)}",
            "Guid"
        };
        return SendAsync<EntityCreateResult>(BuildQueryPath("entity", string.Join(",", parts)), "POST");
    }

    private static string EscapeCustomFieldPathSegment(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("Parameter 'field_name' must be a non-empty path segment.", "field_name");

        if (fieldName is "." or ".." || fieldName.Contains('/') || fieldName.Contains('\\'))
            throw new ArgumentException("Parameter 'field_name' must not contain path separators or traversal segments.", "field_name");

        return Uri.EscapeDataString(fieldName);
    }

    private static string BuildCreateEntityQuerySegment(string entityType, string name, object? body, string? initializationMethod = null)
    {
        var parts = new List<string>
        {
            $"entity=NewEntity({entityType})",
            $"Name={EscapeQueryValue(name)}"
        };

        var jsonBody = body switch { null => null, string s => s, _ => JsonSerializer.Serialize(body) };

        if (jsonBody != null)
        {
            foreach (var (key, value) in JsonToQueryParams(jsonBody))
            {
                if (!key.Equals("Name", StringComparison.OrdinalIgnoreCase) &&
                    !key.Equals("EntityType", StringComparison.OrdinalIgnoreCase))
                    parts.Add($"{key}={EscapeQueryValue(value)}");
            }
        }

        if (initializationMethod is not null)
            parts.Add(initializationMethod);
        parts.Add("Guid");
        return string.Join(",", parts);
    }

    // Callers supply a completed SDK expression with its values already SDK-escaped.
    // Encode only the q value here; transport methods receive the completed URL unchanged.
    private static string BuildQueryPath(string path, string expression)
        => $"{path}?q={Uri.EscapeDataString(expression)}";
}
