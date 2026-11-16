namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class AdministrationBatchLimitTests
{
    public static IEnumerable<object[]> Collections() => BatchLimitTestHelpers.Cases("Administration");

    [Theory]
    [MemberData(nameof(Collections))]
    public void Collection_BindsAtLimitRejectsOverflowAndPublishesMaximum(string toolName, string path)
        => BatchLimitTestHelpers.AssertLimits(toolName, path);
}
