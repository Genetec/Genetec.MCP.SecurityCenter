using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Lpr;

public class ParkingOperationsToolTests
{
    [Theory]
    [InlineData(ParkingOperationsAction.ResetInventory)]
    [InlineData(ParkingOperationsAction.SetOccupancy)]
    [InlineData(ParkingOperationsAction.TriggerEnforceViolation)]
    public async Task Zone_WrongEntityType_RejectsBeforeAction(ParkingOperationsAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingOperationsTool.Execute(TestHelpers.FakeManager(handler),
            new ParkingOperationsRequest(id, action, Occupancy: 10, SessionIds: [Guid.NewGuid()])));
        Assert.Equal("zone_id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000001),ResetParkingZoneInventory(00000000-0000-0000-0000-000000000001)")]
    [InlineData("%2Cunexpected")]
    public void SessionIds_InvalidGuid_RejectsProductionBinding(string id)
    {
        var json = JsonSerializer.Serialize(new { zone_id = Guid.NewGuid(), action = "trigger_enforce_violation", session_ids = new[] { id } });
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ParkingOperationsRequest>(json, NumericStringConverter.ToolOptions));
    }

    [Fact]
    public void SessionIds_BindingAndDiscoveryExposeGuidCollection()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var json = JsonSerializer.Serialize(new { zone_id = Guid.NewGuid(), action = "trigger_enforce_violation", session_ids = ids });
        var request = JsonSerializer.Deserialize<ParkingOperationsRequest>(json, NumericStringConverter.ToolOptions)!;
        Assert.Equal(ids, request.SessionIds);

        var result = SearchToolsTool.Execute(new SearchToolsRequest("parking_operations", Detail: ToolDetailLevel.Full));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result, NumericStringConverter.ToolOptions));
        var tool = Assert.Single(document.RootElement.EnumerateArray());
        var fields = Assert.Single(tool.GetProperty("parameters").EnumerateArray()).GetProperty("fields");
        var sessionIds = Assert.Single(fields.EnumerateArray(), field => field.GetProperty("name").GetString() == "session_ids");
        Assert.Equal("List<Guid>", sessionIds.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(sessionIds.GetProperty("description").GetString()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ResourceLimits.MaxBatchItems + 1)]
    public void SessionIds_BindingValidatesCollectionBounds(int count)
    {
        var json = JsonSerializer.Serialize(new { zone_id = Guid.NewGuid(), action = "trigger_enforce_violation",
            session_ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray() });
        Assert.Throws<ValidationException>(() => JsonSerializer.Deserialize<ParkingOperationsRequest>(json, NumericStringConverter.ToolOptions));
    }
    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task SetOccupancy_NullOccupancy_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ParkingOperationsTool.Execute(TestHelpers.FakeManager(),
                new ParkingOperationsRequest(Guid.NewGuid(),ParkingOperationsAction.SetOccupancy, Occupancy: null)));

        Assert.Contains("occupancy", ex.Message);
    }

    [Fact]
    public async Task TriggerEnforceViolation_NullSessionIds_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ParkingOperationsTool.Execute(TestHelpers.FakeManager(),
                new ParkingOperationsRequest(Guid.NewGuid(),ParkingOperationsAction.TriggerEnforceViolation)));

        Assert.Contains("session_ids", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TriggerEnforceViolation_EmptySessionIds_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ParkingOperationsTool.Execute(TestHelpers.FakeManager(),
                new ParkingOperationsRequest(Guid.NewGuid(),ParkingOperationsAction.TriggerEnforceViolation,
                    SessionIds: [])));

        Assert.Contains("session_ids", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
