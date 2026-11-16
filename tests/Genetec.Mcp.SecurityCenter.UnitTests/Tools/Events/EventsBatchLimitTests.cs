namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Events;

public class EventsBatchLimitTests
{
    public static IEnumerable<object[]> Collections() => BatchLimitTestHelpers.Cases("Events");

    [Theory]
    [MemberData(nameof(Collections))]
    public void Collection_BindsAtLimitRejectsOverflowAndPublishesMaximum(string toolName, string path)
        => BatchLimitTestHelpers.AssertLimits(toolName, path);
}
