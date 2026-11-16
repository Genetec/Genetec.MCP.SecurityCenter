namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Lpr;

public class LprBatchLimitTests
{
    public static IEnumerable<object[]> Collections() => BatchLimitTestHelpers.Cases("Lpr");

    [Theory]
    [MemberData(nameof(Collections))]
    public void Collection_BindsAtLimitRejectsOverflowAndPublishesMaximum(string toolName, string path)
        => BatchLimitTestHelpers.AssertLimits(toolName, path);
}
