namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Utilities;

public class UtilitiesBatchLimitTests
{
    public static IEnumerable<object[]> Collections() => BatchLimitTestHelpers.Cases("Utilities");

    [Theory]
    [MemberData(nameof(Collections))]
    public void Collection_BindsAtLimitRejectsOverflowAndPublishesMaximum(string toolName, string path)
        => BatchLimitTestHelpers.AssertLimits(toolName, path);
}
