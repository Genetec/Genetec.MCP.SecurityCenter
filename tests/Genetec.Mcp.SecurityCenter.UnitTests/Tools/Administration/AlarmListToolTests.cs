using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class AlarmListToolTests
{
    [Fact]
    public void Tool_UsesExpectedName()
        => TestHelpers.AssertToolName<AlarmListTool>("alarm_list");
}
