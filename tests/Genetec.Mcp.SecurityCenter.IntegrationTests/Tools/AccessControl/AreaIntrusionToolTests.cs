using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using System.Diagnostics;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class AreaIntrusionToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<AreaIntrusionAction>()
        .Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsAndPreservesUserGroup(AreaIntrusionAction action)
    {
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var id = wrong.Id;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AreaIntrusionTool.Execute(sc.Manager,
            new AreaIntrusionRequest(action, IntrusionAreaId: id, DeviceId: id, Delay: 30, ArmingDelay: 10)));
        Assert.Equal(action is AreaIntrusionAction.BypassInputDevice or AreaIntrusionAction.ClearInputDeviceBypass ? "device_id" : "intrusion_area_id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", wrong.Name);
    }


    [Theory]
    [InlineData(AreaIntrusionAction.TriggerIntrusionAlarm)]
    [InlineData(AreaIntrusionAction.SilenceIntrusionAlarm)]
    [InlineData(AreaIntrusionAction.AcknowledgeIntrusionAlarm)]
    public async Task IntrusionAreaActions_ReachSecurityCenter(AreaIntrusionAction action)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var exception = await Record.ExceptionAsync(() => AreaIntrusionTool.Execute(sc.Manager,
            new AreaIntrusionRequest(action, IntrusionAreaId: Guid.NewGuid())));

        Assert.True(exception is null or not ArgumentException, exception?.ToString());
    }

    [Theory]
    [InlineData(AreaIntrusionAction.BypassInputDevice)]
    [InlineData(AreaIntrusionAction.ClearInputDeviceBypass)]
    public async Task InputDeviceActions_ReachSecurityCenter(AreaIntrusionAction action)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var exception = await Record.ExceptionAsync(() => AreaIntrusionTool.Execute(sc.Manager,
            new AreaIntrusionRequest(action, DeviceId: Guid.NewGuid())));

        Assert.True(exception is null or not ArgumentException, exception?.ToString());
    }

    [Theory]
    [InlineData(null, 10, "delay")]
    [InlineData(30, null, "arming_delay")]
    public async Task PostponeIntrusionArming_MissingDelay_RejectsAndPreservesEntity(int? delay, int? armingDelay, string parameterName)
    {
        var entity = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AreaIntrusionTool.Execute(sc.Manager,
            new AreaIntrusionRequest(AreaIntrusionAction.PostponeIntrusionArming, IntrusionAreaId: entity.Id, Delay: delay, ArmingDelay: armingDelay)));
        Assert.Equal(parameterName, error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, entity.Id, "UserGroup", entity.Name);
    }

    [Theory]
    [InlineData(4, 3)]
    [InlineData(2, 5)]
    public async Task PostponeIntrusionArming_SecondsDelay_RearmsAfterDelayAndRestoresState(int delaySeconds, int armingDelaySeconds)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var client = sc.Manager.GetClient();
        using var areasDocument = IntegrationTestHelpers.ParseJson(await client.ListEntities(
            "IntrusionArea", 10, 1, ["Name", "IsOnline", "ArmedState"]));
        var area = IntegrationTestHelpers.GetResult(areasDocument.RootElement).EnumerateArray().FirstOrDefault(item =>
            item.GetProperty("Name").GetString()!.StartsWith("SIM-", StringComparison.Ordinal) &&
            item.GetProperty("IsOnline").GetString() == "True" &&
            item.GetProperty("ArmedState").GetString() == "ReadyToArm");
        Assert.True(area.ValueKind == System.Text.Json.JsonValueKind.Object,
            "Postponement integration requires an online SIM- intrusion area ready to arm in the first 10 areas.");
        var areaId = area.GetProperty("Guid").GetGuid();
        Assert.Equal("ReadyToArm", await ReadArmedStateAsync(areaId));

        try
        {
            await IntrusionAreaSecurityTool.Execute(sc.Manager,
                new IntrusionAreaSecurityRequest(IntrusionAreaSecurityAction.Arm, areaId,
                    Timing: IntrusionArmTiming.CustomDelay, CustomDelay: 20));
            await AssertArmedStateEventuallyAsync(areaId, "Arming", TimeSpan.FromSeconds(5));

            var elapsed = Stopwatch.StartNew();
            await AreaIntrusionTool.Execute(sc.Manager,
                new AreaIntrusionRequest(AreaIntrusionAction.PostponeIntrusionArming,
                    IntrusionAreaId: areaId, Delay: delaySeconds, ArmingDelay: armingDelaySeconds));
            await AssertArmedStateEventuallyAsync(areaId, "ReadyToArm", TimeSpan.FromSeconds(2));
            await Task.Delay(TimeSpan.FromSeconds(1));
            Assert.Equal("ReadyToArm", await ReadArmedStateAsync(areaId));
            await AssertArmedStateEventuallyAsync(areaId, "Arming", TimeSpan.FromSeconds(delaySeconds + 5));
            Assert.True(elapsed.Elapsed >= TimeSpan.FromSeconds(delaySeconds),
                "The arming countdown started before the requested postponement delay expired.");
            await Task.Delay(TimeSpan.FromSeconds(1));
            Assert.Equal("Arming", await ReadArmedStateAsync(areaId));
            await AssertArmedStateEventuallyAsync(areaId, "MasterArmed", TimeSpan.FromSeconds(armingDelaySeconds + 5));
            Assert.True(elapsed.Elapsed >= TimeSpan.FromSeconds(delaySeconds + armingDelaySeconds),
                "The area armed before both requested delays expired.");
        }
        finally
        {
            await client.ExecuteEntityMethod(areaId.ToString(), "CancelPostponedArming");
            await IntrusionAreaSecurityTool.Execute(sc.Manager,
                new IntrusionAreaSecurityRequest(IntrusionAreaSecurityAction.Disarm, areaId));
            await AssertArmedStateEventuallyAsync(areaId, "ReadyToArm", TimeSpan.FromSeconds(5));
        }
    }

    private async Task<string> ReadArmedStateAsync(Guid areaId)
    {
        using var document = IntegrationTestHelpers.ParseJson(await sc.Manager.GetClient().GetEntityFields(
            areaId.ToString(), "Guid", "EntityType", "ArmedState"));
        var area = IntegrationTestHelpers.GetResult(document.RootElement);
        Assert.Equal(areaId, area.GetProperty("Guid").GetGuid());
        Assert.Equal("IntrusionArea", area.GetProperty("EntityType").GetString());
        return area.GetProperty("ArmedState").GetString()!;
    }

    private async Task AssertArmedStateEventuallyAsync(Guid areaId, string expectedState, TimeSpan timeout)
    {
        var elapsed = Stopwatch.StartNew();
        string state;
        do
        {
            state = await ReadArmedStateAsync(areaId);
            if (state == expectedState) return;
            await Task.Delay(250);
        } while (elapsed.Elapsed < timeout);
        Assert.Equal(expectedState, state);
    }
}
