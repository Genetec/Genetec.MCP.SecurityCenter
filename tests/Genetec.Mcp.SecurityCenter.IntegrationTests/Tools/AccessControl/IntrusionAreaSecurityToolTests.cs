using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class IntrusionAreaSecurityToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    public static IEnumerable<object[]> ArmVariants()
    {
        foreach (var scope in Enum.GetValues<IntrusionArmScope>())
        foreach (var timing in Enum.GetValues<IntrusionArmTiming>())
        foreach (var type in Enum.GetValues<IntrusionArmType>())
            yield return [scope, timing, type];
    }

    [Theory]
    [MemberData(nameof(ArmVariants))]
    public async Task Arm_AllScopeTimingAndTypeValues_ReachSecurityCenter(
        IntrusionArmScope scope, IntrusionArmTiming timing, IntrusionArmType type)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var exception = await Record.ExceptionAsync(() => IntrusionAreaSecurityTool.Execute(sc.Manager,
            new IntrusionAreaSecurityRequest(IntrusionAreaSecurityAction.Arm, Guid.NewGuid(),
                scope, timing, CustomDelay: 5, Type: type)));
        Assert.True(exception is null or not ArgumentException, exception?.ToString());
    }

    [Fact]
    public async Task Disarm_ReachSecurityCenter()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var exception = await Record.ExceptionAsync(() => IntrusionAreaSecurityTool.Execute(sc.Manager,
            new IntrusionAreaSecurityRequest(IntrusionAreaSecurityAction.Disarm, Guid.NewGuid())));
        Assert.True(exception is null or not ArgumentException, exception?.ToString());
    }
}
