using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class AreaSecurityToolTests
{
    [Theory]
    [InlineData(AreaSecurityAction.ModifyPeopleCount, "cardholder_id")]
    public async Task WrongLinkedType_RejectsBeforeMutation(AreaSecurityAction action, string parameter)
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
            (AreaSecurityAction.ModifyPeopleCount, "cardholder_id") => new AreaSecurityRequest(action, Id: id, CardholderId: wrong, InAccess: true),
            _ => throw new InvalidOperationException()
        };
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AreaSecurityTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal(parameter, error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<AreaSecurityAction>()
        .Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsBeforeMutation(AreaSecurityAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AreaSecurityTool.Execute(TestHelpers.FakeManager(handler),
            new AreaSecurityRequest(action, Id: id, CardholderId: id, InAccess: true, AccessPointId: id, Shunt: true)));
        Assert.Equal(action is AreaSecurityAction.ForgiveAntipassback or AreaSecurityAction.ShuntAccessPointDevice ? "access_point_id" : "id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }


    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Arm_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaSecurityTool.Execute(TestHelpers.FakeManager(),
                new AreaSecurityRequest(AreaSecurityAction.Arm, Id: null)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Disarm_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaSecurityTool.Execute(TestHelpers.FakeManager(),
                new AreaSecurityRequest(AreaSecurityAction.Disarm, Id: null)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ArmPerimeter_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaSecurityTool.Execute(TestHelpers.FakeManager(),
                new AreaSecurityRequest(AreaSecurityAction.ArmPerimeter, Id: null)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ArmZones_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaSecurityTool.Execute(TestHelpers.FakeManager(),
                new AreaSecurityRequest(AreaSecurityAction.ArmZones, Id: null)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DisarmZones_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaSecurityTool.Execute(TestHelpers.FakeManager(),
                new AreaSecurityRequest(AreaSecurityAction.DisarmZones, Id: null)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnlockPerimeterDoors_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaSecurityTool.Execute(TestHelpers.FakeManager(),
                new AreaSecurityRequest(AreaSecurityAction.UnlockPerimeterDoors, Id: null)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetPeopleCount_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaSecurityTool.Execute(TestHelpers.FakeManager(),
                new AreaSecurityRequest(AreaSecurityAction.GetPeopleCount, Id: null)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ModifyPeopleCount_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaSecurityTool.Execute(TestHelpers.FakeManager(),
                new AreaSecurityRequest(AreaSecurityAction.ModifyPeopleCount,
                    Id: null,
                    CardholderId: Guid.NewGuid(),
                    InAccess: true)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ModifyPeopleCount_NullCardholderId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaSecurityTool.Execute(TestHelpers.FakeManager(),
                new AreaSecurityRequest(AreaSecurityAction.ModifyPeopleCount,
                    Id: Guid.NewGuid(),
                    CardholderId: null,
                    InAccess: true)));

        Assert.Contains("cardholder_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ModifyPeopleCount_NullInAccess_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaSecurityTool.Execute(TestHelpers.FakeManager(),
                new AreaSecurityRequest(AreaSecurityAction.ModifyPeopleCount,
                    Id: Guid.NewGuid(),
                    CardholderId: Guid.NewGuid(),
                    InAccess: null)));

        Assert.Contains("in_access", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public async Task ModifyPeopleCount_UsesWebSdkActionSignature(bool inAccess, string expectedInAccess)
    {
        var areaId = Guid.NewGuid();
        var cardholderId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(areaId, "Area");
        handler.AddEntity(cardholderId, "Cardholder");

        await AreaSecurityTool.Execute(TestHelpers.FakeManager(handler),
            new AreaSecurityRequest(AreaSecurityAction.ModifyPeopleCount,
                Id: areaId,
                CardholderId: cardholderId,
                InAccess: inAccess));

        var request = Assert.Single(handler.Requests, observed => observed.Method == HttpMethod.Post).Uri;
        Assert.Equal($"/WebSdk/action?q=ModifyPeopleCount({areaId},{cardholderId},{expectedInAccess})", request.AbsolutePath + Uri.UnescapeDataString(request.Query));
    }

    [Fact]
    public async Task ResetPeopleCount_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaSecurityTool.Execute(TestHelpers.FakeManager(),
                new AreaSecurityRequest(AreaSecurityAction.ResetPeopleCount, Id: null)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForgiveAntipassback_NullAccessPointId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaSecurityTool.Execute(TestHelpers.FakeManager(),
                new AreaSecurityRequest(AreaSecurityAction.ForgiveAntipassback, AccessPointId: null)));

        Assert.Contains("access_point_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShuntAccessPointDevice_NullAccessPointId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaSecurityTool.Execute(TestHelpers.FakeManager(),
                new AreaSecurityRequest(AreaSecurityAction.ShuntAccessPointDevice,
                    AccessPointId: null,
                    Shunt: true)));

        Assert.Contains("access_point_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShuntAccessPointDevice_NullShunt_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaSecurityTool.Execute(TestHelpers.FakeManager(),
                new AreaSecurityRequest(AreaSecurityAction.ShuntAccessPointDevice,
                    AccessPointId: Guid.NewGuid(),
                    Shunt: null)));

        Assert.Contains("shunt", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public async Task ShuntAccessPointDevice_UsesWebSdkSignature(bool shunt, string expectedShunt)
    {
        var accessPointId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(accessPointId, "AccessPoint");

        await AreaSecurityTool.Execute(TestHelpers.FakeManager(handler),
            new AreaSecurityRequest(AreaSecurityAction.ShuntAccessPointDevice,
                AccessPointId: accessPointId,
                Shunt: shunt));

        var request = Assert.Single(handler.Requests, observed => observed.Method == HttpMethod.Post).Uri;
        Assert.Equal($"/WebSdk/action?q=ShuntAccessPointDevice({accessPointId},{expectedShunt})", request.AbsolutePath + Uri.UnescapeDataString(request.Query));
    }
}
