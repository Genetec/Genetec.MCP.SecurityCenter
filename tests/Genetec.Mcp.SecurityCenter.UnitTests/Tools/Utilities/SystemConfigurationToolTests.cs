using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Utilities;

public class SystemConfigurationToolTests
{
    [Fact]
    public void Tool_UsesExpectedName()
        => TestHelpers.AssertToolName<SystemConfigurationTool>("system_configuration");
}
