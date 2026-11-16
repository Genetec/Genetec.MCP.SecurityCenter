using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class RoleListToolTests
{
    [Fact]
    public void Tool_UsesExpectedName()
        => TestHelpers.AssertToolName<RoleListTool>("role_list");
}
