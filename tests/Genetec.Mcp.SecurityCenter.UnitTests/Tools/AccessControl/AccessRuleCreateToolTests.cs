using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using Genetec.Mcp.SecurityCenter.Client;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class AccessRuleCreateToolTests
{
    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Create_StartTimeWithoutEndTime_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AccessRuleCreateTool.Execute(TestHelpers.FakeManager(),
                new AccessRuleCreateRequest([new AccessRuleItem("Temp Rule", StartTime: "2024-01-01T08:00:00Z")])));

        Assert.Contains("end_time", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_EndTimeWithoutStartTime_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AccessRuleCreateTool.Execute(TestHelpers.FakeManager(),
                new AccessRuleCreateRequest([new AccessRuleItem("Temp Rule", EndTime: "2024-01-31T18:00:00Z")])));

        Assert.Contains("start_time", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_PermanentRule_UsesTypedWebSdkFields()
    {
        var schedule = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var handler = new ToolTestHandler();

        var result = await AccessRuleCreateTool.Execute(TestHelpers.FakeManager(handler),
            new AccessRuleCreateRequest([new AccessRuleItem("Permanent Rule", Schedule: schedule)]));

        Assert.IsType<EntityCreateResult>(result);
        var query = Uri.UnescapeDataString(Assert.Single(handler.Requests).ToString());
        Assert.Contains("entity=NewEntity(AccessRule),Name=Permanent Rule,Schedule=" + schedule, query, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduleId=", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_TemporaryRule_UsesTypedWebSdkFields()
    {
        var handler = new ToolTestHandler();

        var result = await AccessRuleCreateTool.Execute(TestHelpers.FakeManager(handler),
            new AccessRuleCreateRequest([new AccessRuleItem(
                "Temporary Rule", StartTime: "2024-01-01T08:00:00Z", EndTime: "2024-01-31T18:00:00Z")]));

        Assert.IsType<EntityCreateResult>(result);
        var query = Uri.UnescapeDataString(Assert.Single(handler.Requests).ToString());
        Assert.Contains("entity=NewEntity(AccessRule,Temporary),Name=Temporary Rule,StartTime=2024-01-01T08:00:00Z,EndTime=2024-01-31T18:00:00Z", query, StringComparison.Ordinal);
    }
}
