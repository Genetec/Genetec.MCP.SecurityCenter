using System.Text.Json;
using System.Globalization;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class AreaMembershipToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    public static IEnumerable<object[]> MixedLinkedBatchActions
    {
        get
        {
            yield return [AreaMembershipAction.AddMembersBatch, AreaMembershipAction.AddMember, AreaMembershipAction.RemoveMember, AreaMembershipAction.GetMembers, "Area"];
            yield return [AreaMembershipAction.RemoveMembersBatch, AreaMembershipAction.AddMember, AreaMembershipAction.RemoveMember, AreaMembershipAction.GetMembers, "Area"];
            yield return [AreaMembershipAction.AddCamerasBatch, AreaMembershipAction.AddCamera, AreaMembershipAction.RemoveCamera, AreaMembershipAction.GetCameras, "Camera"];
            yield return [AreaMembershipAction.RemoveCamerasBatch, AreaMembershipAction.AddCamera, AreaMembershipAction.RemoveCamera, AreaMembershipAction.GetCameras, "Camera"];
            yield return [AreaMembershipAction.AddDoorsBatch, AreaMembershipAction.AddDoor, AreaMembershipAction.RemoveDoor, AreaMembershipAction.GetDoors, "Door"];
            yield return [AreaMembershipAction.RemoveDoorsBatch, AreaMembershipAction.AddDoor, AreaMembershipAction.RemoveDoor, AreaMembershipAction.GetDoors, "Door"];
            yield return [AreaMembershipAction.AddElevatorsBatch, AreaMembershipAction.AddElevator, AreaMembershipAction.RemoveElevator, AreaMembershipAction.GetElevators, "Elevator"];
            yield return [AreaMembershipAction.RemoveElevatorsBatch, AreaMembershipAction.AddElevator, AreaMembershipAction.RemoveElevator, AreaMembershipAction.GetElevators, "Elevator"];
            yield return [AreaMembershipAction.AddAccessRulesBatch, AreaMembershipAction.AddAccessRule, AreaMembershipAction.RemoveAccessRule, AreaMembershipAction.GetAccessRules, "AccessRule"];
            yield return [AreaMembershipAction.RemoveAccessRulesBatch, AreaMembershipAction.AddAccessRule, AreaMembershipAction.RemoveAccessRule, AreaMembershipAction.GetAccessRules, "AccessRule"];
        }
    }

    [Theory]
    [MemberData(nameof(MixedLinkedBatchActions))]
    public async Task MixedLinkedBatch_RejectsWrongTypeAndReadsBackSuccessfulChange(
        AreaMembershipAction action, AreaMembershipAction add, AreaMembershipAction remove, AreaMembershipAction read, string entityType)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var area = await CreateLinkedEntityAsync("Area");
        var linked = await CreateLinkedEntityAsync(entityType);
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var isRemove = action.ToString().StartsWith("Remove", StringComparison.Ordinal);
        var single = new AreaMembershipRequest(add, area, MemberId: linked, CameraId: linked, DoorId: linked,
            ElevatorId: linked, AccessRuleId: linked);
        try
        {
            if (isRemove)
            {
                await AreaMembershipTool.Execute(sc.Manager, single);
                var before = Assert.IsType<string>(await AreaMembershipTool.Execute(sc.Manager,
                    new AreaMembershipRequest(read, area)));
                Assert.Contains(linked, MembershipIds(before, read));
            }
            var result = Assert.IsType<BatchMethodResult>(await AreaMembershipTool.Execute(sc.Manager,
                new AreaMembershipRequest(action, area, Items: [linked, wrong.Id])));
            var batch = result;
            Assert.Equal(2, batch.RequestedCount);
            Assert.Equal(1, batch.SuccessCount);
            Assert.Equal(1, batch.FailureCount);
            Assert.True(batch.Results[0].Success);
            Assert.Equal(linked.ToString(), batch.Results[0].Arg);
            Assert.False(batch.Results[1].Success);
            Assert.Contains(entityType, batch.Results[1].Error);
            var membership = Assert.IsType<string>(await AreaMembershipTool.Execute(sc.Manager,
                new AreaMembershipRequest(read, area)));
            var ids = MembershipIds(membership, read);
            if (isRemove) Assert.DoesNotContain(linked, ids);
            else Assert.Contains(linked, ids);
            Assert.DoesNotContain(wrong.Id, ids);
            await IntegrationTestHelpers.AssertEntityAsync(sc, wrong.Id, "UserGroup", wrong.Name);
        }
        finally
        {
            await AreaMembershipTool.Execute(sc.Manager, single with { Action = remove });
        }
    }

    [Theory]
    [InlineData(AreaMembershipAction.AddCamerasToAreasBatch, true)]
    [InlineData(AreaMembershipAction.AddCamerasToAreasBatch, false)]
    [InlineData(AreaMembershipAction.RemoveCamerasFromAreasBatch, true)]
    [InlineData(AreaMembershipAction.RemoveCamerasFromAreasBatch, false)]
    public async Task MixedCameraAreaBatch_RejectsWrongLinkAndReadsBackSuccessfulChange(AreaMembershipAction action, bool wrongArea)
    {
        var area = await CreateLinkedEntityAsync("Area");
        var camera = await CreateLinkedEntityAsync("Camera");
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var isRemove = action == AreaMembershipAction.RemoveCamerasFromAreasBatch;
        var single = new AreaMembershipRequest(AreaMembershipAction.AddCamera, area, CameraId: camera);
        try
        {
            if (isRemove)
            {
                await AreaMembershipTool.Execute(sc.Manager, single);
                var before = Assert.IsType<string>(await AreaMembershipTool.Execute(sc.Manager,
                    new AreaMembershipRequest(AreaMembershipAction.GetCameras, area)));
                Assert.Contains(camera, MembershipIds(before, AreaMembershipAction.GetCameras));
            }
            var invalid = wrongArea ? new AreaCameraPair(wrong.Id, camera) : new AreaCameraPair(area, wrong.Id);
            var result = await AreaMembershipTool.Execute(sc.Manager,
                new AreaMembershipRequest(action, Guid.Empty, CameraAreaPairs: [new(area, camera), invalid]));
            var batch = Assert.IsType<BatchMethodResult>(result);
            Assert.Equal(2, batch.RequestedCount);
            Assert.Equal(1, batch.SuccessCount);
            Assert.Equal(1, batch.FailureCount);
            Assert.True(batch.Results[0].Success);
            Assert.Equal(area, batch.Results[0].Id);
            Assert.False(batch.Results[1].Success);
            Assert.Equal(invalid.AreaId, batch.Results[1].Id);
            Assert.Contains(wrongArea ? "Area" : "Camera", batch.Results[1].Error);
            var membership = Assert.IsType<string>(await AreaMembershipTool.Execute(sc.Manager,
                new AreaMembershipRequest(AreaMembershipAction.GetCameras, area)));
            var ids = MembershipIds(membership, AreaMembershipAction.GetCameras);
            if (isRemove) Assert.DoesNotContain(camera, ids);
            else Assert.Contains(camera, ids);
            Assert.DoesNotContain(wrong.Id, ids);
            await IntegrationTestHelpers.AssertEntityAsync(sc, wrong.Id, "UserGroup", wrong.Name);
        }
        finally
        {
            await AreaMembershipTool.Execute(sc.Manager, single with { Action = AreaMembershipAction.RemoveCamera });
        }
    }

    private static IReadOnlyList<Guid> MembershipIds(string json, AreaMembershipAction action)
        => action switch
        {
            AreaMembershipAction.GetMembers => IntegrationTestHelpers.ExtractGuids(json, "CaptiveAreas"),
            AreaMembershipAction.GetCameras => IntegrationTestHelpers.ExtractGuids(json, "Cameras", "Camera"),
            AreaMembershipAction.GetDoors => IntegrationTestHelpers.ExtractGuids(json, "AllDoors"),
            AreaMembershipAction.GetElevators => IntegrationTestHelpers.ExtractGuids(json, "Elevators"),
            AreaMembershipAction.GetAccessRules => IntegrationTestHelpers.ExtractGuids(json, "AccessRules", "AccessRule"),
            _ => throw new ArgumentException($"Unexpected membership read action: {action}")
        };

    private async Task<Guid> CreateLinkedEntityAsync(string entityType)
    {
        if (entityType == "Camera") return await IntegrationTestHelpers.GetFirstVideoEncoderCameraIdAsync(sc);
        var name = $"MCP_Mixed_{Guid.NewGuid():N}";
        var result = entityType switch
        {
            "Area" => await AreaCreateTool.Execute(sc.Manager, new AreaCreateRequest([new AreaItem(name)])),
            "Door" => await DoorCreateTool.Execute(sc.Manager, new DoorCreateRequest([new DoorItem(name)])),
            "Elevator" => await ElevatorManagementTool.Execute(sc.Manager, new ElevatorManagementRequest(ElevatorAction.Create, Items: [new ElevatorManagementItem(Name: name)])),
            "AccessRule" => await AccessRuleCreateTool.Execute(sc.Manager, new AccessRuleCreateRequest([new AccessRuleItem(name)])),
            _ => throw new InvalidOperationException()
        };
        var created = Assert.IsType<EntityCreateResult>(result);
        sc.TrackForCleanup(created.Guid);
        await IntegrationTestHelpers.AssertEntityAsync(sc, created.Guid, entityType, name);
        return created.Guid;
    }

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
    public async Task WrongLinkedType_RejectsAndPreservesPrimary(AreaMembershipAction action, string parameter)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var created = Assert.IsType<EntityCreateResult>(await AreaCreateTool.Execute(sc.Manager,
            new AreaCreateRequest([new AreaItem($"MCP_Linked_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(created.Guid);
        var id = created.Guid;
        await IntegrationTestHelpers.AssertWrongRelatedEntityTypeAsync(sc, id, "Area", wrong => (action, parameter) switch
        {
            (AreaMembershipAction.AddMember, "member_id") => AreaMembershipTool.Execute(sc.Manager, new AreaMembershipRequest(action, id, MemberId: wrong)),
            (AreaMembershipAction.RemoveMember, "member_id") => AreaMembershipTool.Execute(sc.Manager, new AreaMembershipRequest(action, id, MemberId: wrong)),
            (AreaMembershipAction.AddCamera, "camera_id") => AreaMembershipTool.Execute(sc.Manager, new AreaMembershipRequest(action, id, CameraId: wrong)),
            (AreaMembershipAction.RemoveCamera, "camera_id") => AreaMembershipTool.Execute(sc.Manager, new AreaMembershipRequest(action, id, CameraId: wrong)),
            (AreaMembershipAction.AddDoor, "door_id") => AreaMembershipTool.Execute(sc.Manager, new AreaMembershipRequest(action, id, DoorId: wrong)),
            (AreaMembershipAction.AddCaptiveDoor, "door_id") => AreaMembershipTool.Execute(sc.Manager, new AreaMembershipRequest(action, id, DoorId: wrong)),
            (AreaMembershipAction.RemoveDoor, "door_id") => AreaMembershipTool.Execute(sc.Manager, new AreaMembershipRequest(action, id, DoorId: wrong)),
            (AreaMembershipAction.AddElevator, "elevator_id") => AreaMembershipTool.Execute(sc.Manager, new AreaMembershipRequest(action, id, ElevatorId: wrong)),
            (AreaMembershipAction.RemoveElevator, "elevator_id") => AreaMembershipTool.Execute(sc.Manager, new AreaMembershipRequest(action, id, ElevatorId: wrong)),
            (AreaMembershipAction.AddAccessRule, "access_rule_id") => AreaMembershipTool.Execute(sc.Manager, new AreaMembershipRequest(action, id, AccessRuleId: wrong)),
            (AreaMembershipAction.RemoveAccessRule, "access_rule_id") => AreaMembershipTool.Execute(sc.Manager, new AreaMembershipRequest(action, id, AccessRuleId: wrong)),
            _ => throw new InvalidOperationException()
        }, parameter);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<AreaMembershipAction>()
        .Where(action => action != AreaMembershipAction.ActivityReport && action != AreaMembershipAction.AddCamerasToAreasBatch && action != AreaMembershipAction.RemoveCamerasFromAreasBatch).Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsAndPreservesUserGroup(AreaMembershipAction action)
    {
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var id = wrong.Id;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AreaMembershipTool.Execute(sc.Manager,
            new AreaMembershipRequest(action, Id: id, MemberId: id, CameraId: id, DoorId: id, ElevatorId: id, AccessRuleId: id, Items: [id])));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", wrong.Name);
    }


    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task AddRemoveMembersBatch_UsesCreatedAreaGuidsAndReadsBackMembership()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var areaResult = (EntityCreateResult)await AreaCreateTool.Execute(sc.Manager,
            new AreaCreateRequest([new AreaItem($"MCP_Area_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(areaResult.Guid);
        var memberResult = (EntityCreateResult)await AreaCreateTool.Execute(sc.Manager,
            new AreaCreateRequest([new AreaItem($"MCP_Area_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(memberResult.Guid);

        await AreaMembershipTool.Execute(sc.Manager,
            new AreaMembershipRequest(AreaMembershipAction.AddMembersBatch, areaResult.Guid,
                Items: [memberResult.Guid]));
        var afterAdd = await AreaMembershipTool.Execute(sc.Manager,
            new AreaMembershipRequest(AreaMembershipAction.GetMembers, areaResult.Guid));
        Assert.Contains(memberResult.Guid, MembershipIds(Assert.IsType<string>(afterAdd), AreaMembershipAction.GetMembers));

        await AreaMembershipTool.Execute(sc.Manager,
            new AreaMembershipRequest(AreaMembershipAction.RemoveMembersBatch, areaResult.Guid,
                Items: [memberResult.Guid]));
        var afterRemove = await AreaMembershipTool.Execute(sc.Manager,
            new AreaMembershipRequest(AreaMembershipAction.GetMembers, areaResult.Guid));
        Assert.DoesNotContain(memberResult.Guid, MembershipIds(Assert.IsType<string>(afterRemove), AreaMembershipAction.GetMembers));
    }

    [Fact]
    public async Task GetMembers_ExistingArea_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Area_{Guid.NewGuid():N}";
        var createResult = await AreaCreateTool.Execute(sc.Manager,
            new AreaCreateRequest([new AreaItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await AreaMembershipTool.Execute(sc.Manager,
            new AreaMembershipRequest(AreaMembershipAction.GetMembers, Id: created.Guid));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetCameras_ExistingArea_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Area_{Guid.NewGuid():N}";
        var createResult = await AreaCreateTool.Execute(sc.Manager,
            new AreaCreateRequest([new AreaItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await AreaMembershipTool.Execute(sc.Manager,
            new AreaMembershipRequest(AreaMembershipAction.GetCameras, Id: created.Guid));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetDoors_ExistingArea_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Area_{Guid.NewGuid():N}";
        var createResult = await AreaCreateTool.Execute(sc.Manager,
            new AreaCreateRequest([new AreaItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await AreaMembershipTool.Execute(sc.Manager,
            new AreaMembershipRequest(AreaMembershipAction.GetDoors, Id: created.Guid));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetElevators_ExistingArea_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Area_{Guid.NewGuid():N}";
        var createResult = await AreaCreateTool.Execute(sc.Manager,
            new AreaCreateRequest([new AreaItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await AreaMembershipTool.Execute(sc.Manager,
            new AreaMembershipRequest(AreaMembershipAction.GetElevators, Id: created.Guid));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetAccessRules_ExistingArea_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Area_{Guid.NewGuid():N}";
        var createResult = await AreaCreateTool.Execute(sc.Manager,
            new AreaCreateRequest([new AreaItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await AreaMembershipTool.Execute(sc.Manager,
            new AreaMembershipRequest(AreaMembershipAction.GetAccessRules, Id: created.Guid));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetAccessPoints_ExistingArea_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Area_{Guid.NewGuid():N}";
        var createResult = await AreaCreateTool.Execute(sc.Manager,
            new AreaCreateRequest([new AreaItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await AreaMembershipTool.Execute(sc.Manager,
            new AreaMembershipRequest(AreaMembershipAction.GetAccessPoints, Id: created.Guid));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ActivityReport_ExistingArea_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Area_{Guid.NewGuid():N}";
        var createResult = await AreaCreateTool.Execute(sc.Manager,
            new AreaCreateRequest([new AreaItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await AreaMembershipTool.Execute(sc.Manager,
            new AreaMembershipRequest(AreaMembershipAction.ActivityReport, Id: created.Guid));

        Assert.NotNull(result);

        var timeStart = DateTimeOffset.UtcNow.AddDays(-1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var timeEnd = DateTimeOffset.UtcNow.AddDays(1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var boundedResult = await AreaMembershipTool.Execute(sc.Manager,
            new AreaMembershipRequest(AreaMembershipAction.ActivityReport, Id: created.Guid,
                TimeStart: timeStart, TimeEnd: timeEnd));

        Assert.NotNull(boundedResult);
    }

    [Fact]
    public async Task AddCaptiveDoor_ReadsBackMembershipAndRemovesDoor()
    {
        var area = await CreateLinkedEntityAsync("Area");
        var door = await CreateLinkedEntityAsync("Door");
        try
        {
            await AreaMembershipTool.Execute(sc.Manager,
                new AreaMembershipRequest(AreaMembershipAction.AddCaptiveDoor, area, DoorId: door));
            var membership = Assert.IsType<string>(await AreaMembershipTool.Execute(sc.Manager,
                new AreaMembershipRequest(AreaMembershipAction.GetDoors, area)));
            Assert.Contains(door, MembershipIds(membership, AreaMembershipAction.GetDoors));
            await AreaMembershipTool.Execute(sc.Manager,
                new AreaMembershipRequest(AreaMembershipAction.RemoveDoor, area, DoorId: door));
            membership = Assert.IsType<string>(await AreaMembershipTool.Execute(sc.Manager,
                new AreaMembershipRequest(AreaMembershipAction.GetDoors, area)));
            Assert.DoesNotContain(door, MembershipIds(membership, AreaMembershipAction.GetDoors));
        }
        finally
        {
            await AreaMembershipTool.Execute(sc.Manager,
                new AreaMembershipRequest(AreaMembershipAction.RemoveDoor, area, DoorId: door));
        }
    }

    [Theory]
    [InlineData(AreaDoorSide.Entry)]
    [InlineData(AreaDoorSide.Exit)]
    [InlineData(AreaDoorSide.Both)]
    public async Task AccessRuleDoorSide_ReadsBackMembership(AreaDoorSide side)
    {
        var area = await CreateLinkedEntityAsync("Area");
        var rule = await CreateLinkedEntityAsync("AccessRule");
        var request = new AreaMembershipRequest(AreaMembershipAction.AddAccessRule, area, AccessRuleId: rule, DoorSide: side);
        try
        {
            await AreaMembershipTool.Execute(sc.Manager, request);
            var membership = Assert.IsType<string>(await AreaMembershipTool.Execute(sc.Manager,
                new AreaMembershipRequest(AreaMembershipAction.GetAccessRules, area)));
            Assert.Contains(rule, MembershipIds(membership, AreaMembershipAction.GetAccessRules));
            await AreaMembershipTool.Execute(sc.Manager, request with { Action = AreaMembershipAction.RemoveAccessRule });
            membership = Assert.IsType<string>(await AreaMembershipTool.Execute(sc.Manager,
                new AreaMembershipRequest(AreaMembershipAction.GetAccessRules, area)));
            Assert.DoesNotContain(rule, MembershipIds(membership, AreaMembershipAction.GetAccessRules));
        }
        finally
        {
            await AreaMembershipTool.Execute(sc.Manager, request with { Action = AreaMembershipAction.RemoveAccessRule });
        }
    }
}
