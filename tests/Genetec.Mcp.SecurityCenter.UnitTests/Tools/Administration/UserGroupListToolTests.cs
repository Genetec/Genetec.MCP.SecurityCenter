using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class UserGroupListToolTests
{
    [Fact]
    public void Tool_UsesExpectedName()
        => TestHelpers.AssertToolName<UserGroupListTool>("user_group_list");
}
