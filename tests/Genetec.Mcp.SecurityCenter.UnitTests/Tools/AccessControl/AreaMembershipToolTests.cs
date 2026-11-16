using Genetec.Mcp.SecurityCenter.Client;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class AreaMembershipToolTests
{
    [Theory]
    [InlineData(AreaMembershipAction.AddMember, "member_id")]
    [InlineData(AreaMembershipAction.RemoveMember, "member_id")]
    [InlineData(AreaMembershipAction.AddCamera, "camera_id")]
    [InlineData(AreaMembershipAction.RemoveCamera, "camera_id")]
    [InlineData(AreaMembershipAction.AddDoor, "door_id")]
    [InlineData(AreaMembershipAction.AddCaptiveDoor, "door_id")]
    [InlineData(AreaMembershipAction.RemoveDoor, "door_id")]
    [InlineData(AreaMembershipAction.AddElevator, "elevator_id")]
    [InlineData(AreaMembershipAction.RemoveElevator, "elevator_id")]
    [InlineData(AreaMembershipAction.AddAccessRule, "access_rule_id")]
    [InlineData(AreaMembershipAction.RemoveAccessRule, "access_rule_id")]
    public async Task WrongLinkedType_RejectsBeforeMutation(AreaMembershipAction action, string parameter)
    {
        var id = Guid.NewGuid();
        var wrong = Guid.NewGuid();
        var escort = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "Area");
        handler.AddEntity(wrong, "UserGroup");
        handler.AddEntity(escort, "Cardholder");
        var request = (action, parameter) switch
        {
            (AreaMembershipAction.AddMember, "member_id") => new AreaMembershipRequest(action, id, MemberId: wrong),
            (AreaMembershipAction.RemoveMember, "member_id") => new AreaMembershipRequest(action, id, MemberId: wrong),
            (AreaMembershipAction.AddCamera, "camera_id") => new AreaMembershipRequest(action, id, CameraId: wrong),
            (AreaMembershipAction.RemoveCamera, "camera_id") => new AreaMembershipRequest(action, id, CameraId: wrong),
            (AreaMembershipAction.AddDoor, "door_id") => new AreaMembershipRequest(action, id, DoorId: wrong),
            (AreaMembershipAction.AddCaptiveDoor, "door_id") => new AreaMembershipRequest(action, id, DoorId: wrong),
            (AreaMembershipAction.RemoveDoor, "door_id") => new AreaMembershipRequest(action, id, DoorId: wrong),
            (AreaMembershipAction.AddElevator, "elevator_id") => new AreaMembershipRequest(action, id, ElevatorId: wrong),
            (AreaMembershipAction.RemoveElevator, "elevator_id") => new AreaMembershipRequest(action, id, ElevatorId: wrong),
            (AreaMembershipAction.AddAccessRule, "access_rule_id") => new AreaMembershipRequest(action, id, AccessRuleId: wrong),
            (AreaMembershipAction.RemoveAccessRule, "access_rule_id") => new AreaMembershipRequest(action, id, AccessRuleId: wrong),
            _ => throw new InvalidOperationException()
        };
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AreaMembershipTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal(parameter, error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    public static IEnumerable<object[]> LinkedBatchActions
    {
        get
        {
            yield return [AreaMembershipAction.AddMembersBatch, "Area"];
            yield return [AreaMembershipAction.RemoveMembersBatch, "Area"];
            yield return [AreaMembershipAction.AddCamerasBatch, "Camera"];
            yield return [AreaMembershipAction.RemoveCamerasBatch, "Camera"];
            yield return [AreaMembershipAction.AddDoorsBatch, "Door"];
            yield return [AreaMembershipAction.RemoveDoorsBatch, "Door"];
            yield return [AreaMembershipAction.AddElevatorsBatch, "Elevator"];
            yield return [AreaMembershipAction.RemoveElevatorsBatch, "Elevator"];
            yield return [AreaMembershipAction.AddAccessRulesBatch, "AccessRule"];
            yield return [AreaMembershipAction.RemoveAccessRulesBatch, "AccessRule"];
        }
    }

    [Theory]
    [MemberData(nameof(LinkedBatchActions))]
    public async Task MixedLinkedBatch_RejectsWrongTypeAndKeepsSuccessfulItem(AreaMembershipAction action, string entityType)
    {
        var areaId = Guid.NewGuid();
        var validId = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(areaId, "Area");
        handler.AddEntity(validId, entityType);
        handler.AddEntity(wrongId, "UserGroup");
        var result = Assert.IsType<BatchMethodResult>(await AreaMembershipTool.Execute(TestHelpers.FakeManager(handler),
            new AreaMembershipRequest(action, areaId, Items: [wrongId, validId], DoorSide: AreaDoorSide.Entry)));
        var batch = result;
        Assert.Equal(2, batch.RequestedCount);
        Assert.Equal(1, batch.SuccessCount);
        Assert.Equal(1, batch.FailureCount);
        Assert.False(batch.Results[0].Success);
        Assert.Contains(entityType, batch.Results[0].Error);
        Assert.True(batch.Results[1].Success);
        var mutation = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
        Assert.Contains(validId.ToString(), Uri.UnescapeDataString(mutation.Uri.Query));
        Assert.DoesNotContain(wrongId.ToString(), Uri.UnescapeDataString(mutation.Uri.Query));
    }

    [Theory]
    [InlineData(AreaMembershipAction.AddCamerasToAreasBatch, true)]
    [InlineData(AreaMembershipAction.AddCamerasToAreasBatch, false)]
    [InlineData(AreaMembershipAction.RemoveCamerasFromAreasBatch, true)]
    [InlineData(AreaMembershipAction.RemoveCamerasFromAreasBatch, false)]
    public async Task MixedCameraAreaBatch_RejectsWrongLinkAndKeepsSuccessfulPair(AreaMembershipAction action, bool wrongArea)
    {
        var areaId = Guid.NewGuid();
        var cameraId = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(areaId, "Area");
        handler.AddEntity(cameraId, "Camera");
        handler.AddEntity(wrongId, "UserGroup");
        var invalid = wrongArea ? new AreaCameraPair(wrongId, cameraId) : new AreaCameraPair(areaId, wrongId);
        var result = Assert.IsType<BatchMethodResult>(await AreaMembershipTool.Execute(TestHelpers.FakeManager(handler),
            new AreaMembershipRequest(action, Guid.Empty, CameraAreaPairs: [invalid, new(areaId, cameraId)])));
        var batch = result;
        Assert.Equal(1, batch.SuccessCount);
        Assert.Equal(1, batch.FailureCount);
        Assert.False(batch.Results[0].Success);
        Assert.True(batch.Results[1].Success);
        var mutation = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
        Assert.DoesNotContain(wrongId.ToString(), Uri.UnescapeDataString(mutation.Uri.Query));
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<AreaMembershipAction>()
        .Where(action => action != AreaMembershipAction.ActivityReport && action != AreaMembershipAction.AddCamerasToAreasBatch && action != AreaMembershipAction.RemoveCamerasFromAreasBatch).Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsBeforeMutation(AreaMembershipAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AreaMembershipTool.Execute(TestHelpers.FakeManager(handler),
            new AreaMembershipRequest(action, Id: id, MemberId: id, CameraId: id, DoorId: id, ElevatorId: id, AccessRuleId: id, Items: [id])));
        Assert.Equal("id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }


    [Fact]
    public void BatchItems_ProductionBindingAndDiscoveryExposeGuidList()
    {
        var areaId = Guid.NewGuid();
        var itemIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var json = $$"""{"action":"add_members_batch","id":"{{areaId}}","items":["{{itemIds[0]}}","{{itemIds[1]}}"]}""";
        var request = JsonSerializer.Deserialize<AreaMembershipRequest>(json, NumericStringConverter.ToolOptions)!;

        Assert.Equal(itemIds, request.Items);
        Assert.Equal(typeof(List<Guid>), typeof(AreaMembershipRequest).GetProperty(nameof(AreaMembershipRequest.Items))!.PropertyType);

        var result = SearchToolsTool.Execute(new SearchToolsRequest(Query: "area_membership", Detail: ToolDetailLevel.Full));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result, NumericStringConverter.ToolOptions));
        var tool = Assert.Single(document.RootElement.EnumerateArray());
        var requestFields = Assert.Single(tool.GetProperty("parameters").EnumerateArray()).GetProperty("fields");
        var items = Assert.Single(requestFields.EnumerateArray(), field => field.GetProperty("name").GetString() == "items");
        Assert.Equal("List<Guid>", items.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(items.GetProperty("description").GetString()));
    }

    [Fact]
    public async Task AccessRuleBatch_PreservesCompoundDoorSideArgument()
    {
        var handler = new EntityMetadataTestHandler();
        var areaId = Guid.NewGuid();
        var accessRuleId = Guid.NewGuid();

        handler.AddEntity(areaId, "Area");
        handler.AddEntity(accessRuleId, "AccessRule");
        await AreaMembershipTool.Execute(TestHelpers.FakeManager(handler),
            new AreaMembershipRequest(AreaMembershipAction.AddAccessRulesBatch, areaId,
                DoorSide: AreaDoorSide.Entry, Items: [accessRuleId]));

        var observed = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
        Assert.Contains($"AddAccessRule({accessRuleId}\\,Entry)", Uri.UnescapeDataString(observed.Uri.Query), StringComparison.Ordinal);
    }


    [Fact]
    public async Task ActivityReport_WithoutTimeRange_PreservesEntityFilter()
    {
        var handler = new Tools.ToolTestHandler();
        var id = Guid.NewGuid();

        await AreaMembershipTool.Execute(TestHelpers.FakeManager(handler),
            new AreaMembershipRequest(AreaMembershipAction.ActivityReport, id));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            $"/WebSdk/report/AreaActivity?q=Areas@{id}",
            Uri.UnescapeDataString(request.AbsolutePath + request.Query));
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task AddMember_NullMemberId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaMembershipTool.Execute(TestHelpers.FakeManager(),
                new AreaMembershipRequest(AreaMembershipAction.AddMember,
                    Id: Guid.NewGuid(),
                    MemberId: null)));

        Assert.Contains("member_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveMember_NullMemberId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaMembershipTool.Execute(TestHelpers.FakeManager(),
                new AreaMembershipRequest(AreaMembershipAction.RemoveMember,
                    Id: Guid.NewGuid(),
                    MemberId: null)));

        Assert.Contains("member_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddCamera_NullCameraId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaMembershipTool.Execute(TestHelpers.FakeManager(),
                new AreaMembershipRequest(AreaMembershipAction.AddCamera,
                    Id: Guid.NewGuid(),
                    CameraId: null)));

        Assert.Contains("camera_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveCamera_NullCameraId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaMembershipTool.Execute(TestHelpers.FakeManager(),
                new AreaMembershipRequest(AreaMembershipAction.RemoveCamera,
                    Id: Guid.NewGuid(),
                    CameraId: null)));

        Assert.Contains("camera_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddDoor_NullDoorId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaMembershipTool.Execute(TestHelpers.FakeManager(),
                new AreaMembershipRequest(AreaMembershipAction.AddDoor,
                    Id: Guid.NewGuid(),
                    DoorId: null)));

        Assert.Contains("door_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveDoor_NullDoorId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaMembershipTool.Execute(TestHelpers.FakeManager(),
                new AreaMembershipRequest(AreaMembershipAction.RemoveDoor,
                    Id: Guid.NewGuid(),
                    DoorId: null)));

        Assert.Contains("door_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddCamerasBatch_NullItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaMembershipTool.Execute(TestHelpers.FakeManager(),
                new AreaMembershipRequest(AreaMembershipAction.AddCamerasBatch,
                    Id: Guid.NewGuid(),
                    Items: null)));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddMembersBatch_NullItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaMembershipTool.Execute(TestHelpers.FakeManager(),
                new AreaMembershipRequest(AreaMembershipAction.AddMembersBatch,
                    Id: Guid.NewGuid(),
                    Items: null)));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddDoorsBatch_NullItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaMembershipTool.Execute(TestHelpers.FakeManager(),
                new AreaMembershipRequest(AreaMembershipAction.AddDoorsBatch,
                    Id: Guid.NewGuid(),
                    Items: null)));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddCaptiveDoor_NullDoorId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaMembershipTool.Execute(TestHelpers.FakeManager(),
                new AreaMembershipRequest(AreaMembershipAction.AddCaptiveDoor,
                    Id: Guid.NewGuid(), DoorId: null)));

        Assert.Contains("door_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AreaMembershipAction.AddElevator)]
    [InlineData(AreaMembershipAction.RemoveElevator)]
    public async Task ElevatorAction_NullElevatorId_ThrowsArgumentException(AreaMembershipAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaMembershipTool.Execute(TestHelpers.FakeManager(),
                new AreaMembershipRequest(action, Id: Guid.NewGuid(), ElevatorId: null)));

        Assert.Contains("elevator_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AreaMembershipAction.AddAccessRule)]
    [InlineData(AreaMembershipAction.RemoveAccessRule)]
    public async Task AccessRuleAction_NullAccessRuleId_ThrowsArgumentException(AreaMembershipAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaMembershipTool.Execute(TestHelpers.FakeManager(),
                new AreaMembershipRequest(action, Id: Guid.NewGuid(), AccessRuleId: null)));

        Assert.Contains("access_rule_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AreaMembershipAction.AddCamerasToAreasBatch)]
    [InlineData(AreaMembershipAction.RemoveCamerasFromAreasBatch)]
    public async Task CameraAreaBatch_NullPairs_ThrowsArgumentException(AreaMembershipAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaMembershipTool.Execute(TestHelpers.FakeManager(),
                new AreaMembershipRequest(action, Id: Guid.NewGuid(), CameraAreaPairs: null)));

        Assert.Contains("camera_area_pairs", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<object[]> EmptyBatchActions()
    {
        yield return [AreaMembershipAction.AddMembersBatch];
        yield return [AreaMembershipAction.RemoveMembersBatch];
        yield return [AreaMembershipAction.AddCamerasBatch];
        yield return [AreaMembershipAction.RemoveCamerasBatch];
        yield return [AreaMembershipAction.AddCamerasToAreasBatch];
        yield return [AreaMembershipAction.RemoveCamerasFromAreasBatch];
        yield return [AreaMembershipAction.AddDoorsBatch];
        yield return [AreaMembershipAction.RemoveDoorsBatch];
        yield return [AreaMembershipAction.AddElevatorsBatch];
        yield return [AreaMembershipAction.RemoveElevatorsBatch];
        yield return [AreaMembershipAction.AddAccessRulesBatch];
        yield return [AreaMembershipAction.RemoveAccessRulesBatch];
    }

    [Theory]
    [MemberData(nameof(EmptyBatchActions))]
    public async Task BatchAction_EmptyItemsOrPairs_ThrowsArgumentException(AreaMembershipAction action)
    {
        var request = action is AreaMembershipAction.AddCamerasToAreasBatch
            or AreaMembershipAction.RemoveCamerasFromAreasBatch
            ? new AreaMembershipRequest(action, Guid.NewGuid(), CameraAreaPairs: [])
            : new AreaMembershipRequest(action, Guid.NewGuid(), Items: []);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaMembershipTool.Execute(TestHelpers.FakeManager(), request));

        Assert.Contains(action is AreaMembershipAction.AddCamerasToAreasBatch
            or AreaMembershipAction.RemoveCamerasFromAreasBatch
            ? "camera_area_pairs"
            : "items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActivityReport_WithTimeRange_UsesTimeFilter()
    {
        var handler = new Tools.ToolTestHandler();
        var id = Guid.NewGuid();

        await AreaMembershipTool.Execute(TestHelpers.FakeManager(handler),
            new AreaMembershipRequest(AreaMembershipAction.ActivityReport, id,
                TimeStart: "2024-01-01T00:00:00Z", TimeEnd: "2024-01-02T00:00:00Z"));

        var uri = Uri.UnescapeDataString(Assert.Single(handler.Requests).ToString());
        Assert.Contains(
            $"report/AreaActivity?q=Areas@{id},TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z)",
            uri,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActivityReport_WithOnlyOneTimeBoundary_ThrowsArgumentException(bool provideStart)
    {
        var request = new AreaMembershipRequest(AreaMembershipAction.ActivityReport, Guid.NewGuid(),
            TimeStart: provideStart ? "2024-01-01T00:00:00Z" : null,
            TimeEnd: provideStart ? null : "2024-01-02T00:00:00Z");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaMembershipTool.Execute(TestHelpers.FakeManager(), request));

        Assert.Contains("time_start", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("time_end", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
