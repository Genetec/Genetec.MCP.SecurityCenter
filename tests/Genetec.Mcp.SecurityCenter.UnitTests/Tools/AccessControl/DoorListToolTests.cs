using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class DoorListToolTests
{
    [Fact]
    public void Request_HasExpectedDefaults()
    {
        var request = new DoorListRequest();
        Assert.Equal(50, request.PageSize);
        Assert.Equal(1, request.Page);
    }
}
