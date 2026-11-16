using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class IntrusionAreaSecurityToolTests
{
    [Theory]
    [InlineData(IntrusionAreaSecurityAction.Arm)]
    [InlineData(IntrusionAreaSecurityAction.Disarm)]
    public void Request_HasRequiredActionAndArea(IntrusionAreaSecurityAction action)
    {
        var request = new IntrusionAreaSecurityRequest(action, Guid.NewGuid());
        Assert.Equal(action, request.Action);
        Assert.NotEqual(Guid.Empty, request.IntrusionAreaId);
    }

    [Fact]
    public void Request_ContainsAllArmingEnumValues()
    {
        Assert.Equal(2, Enum.GetValues<IntrusionArmScope>().Length);
        Assert.Equal(3, Enum.GetValues<IntrusionArmTiming>().Length);
        Assert.Equal(3, Enum.GetValues<IntrusionArmType>().Length);
    }
}
