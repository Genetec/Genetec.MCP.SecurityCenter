using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class UserListToolTests
{
    [Fact]
    public void Tool_UsesExpectedName()
        => TestHelpers.AssertToolName<UserListTool>("user_list");
}
