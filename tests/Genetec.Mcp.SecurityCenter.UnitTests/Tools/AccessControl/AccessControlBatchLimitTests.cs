namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class AccessControlBatchLimitTests
{
    public static IEnumerable<object[]> Collections() => BatchLimitTestHelpers.Cases("AccessControl");

    [Theory]
    [MemberData(nameof(Collections))]
    public void Collection_BindsAtLimitRejectsOverflowAndPublishesMaximum(string toolName, string path)
        => BatchLimitTestHelpers.AssertLimits(toolName, path);
}
