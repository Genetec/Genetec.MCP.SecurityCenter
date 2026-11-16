using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class VisitorWorkflowToolTests
{
    [Theory]
    [InlineData(VisitorWorkflowAction.AssignEscorts, "escort_id")]
    [InlineData(VisitorWorkflowAction.AssignEscorts, "escort2_id")]
    public async Task WrongLinkedType_RejectsBeforeMutation(VisitorWorkflowAction action, string parameter)
    {
        var id = Guid.NewGuid();
        var wrong = Guid.NewGuid();
        var escort = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "Visitor");
        handler.AddEntity(wrong, "UserGroup");
        handler.AddEntity(escort, "Cardholder");
        var request = (action, parameter) switch
        {
            (VisitorWorkflowAction.AssignEscorts, "escort_id") => new VisitorWorkflowRequest(action, Id: id, EscortId: wrong),
            (VisitorWorkflowAction.AssignEscorts, "escort2_id") => new VisitorWorkflowRequest(action, Id: id, EscortId: escort, Escort2Id: wrong),
            _ => throw new InvalidOperationException()
        };
        var error = await Assert.ThrowsAsync<ArgumentException>(() => VisitorWorkflowTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal(parameter, error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<VisitorWorkflowAction>()
        .Where(action => action != VisitorWorkflowAction.ActivityReport && action != VisitorWorkflowAction.SearchByStatus).Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsBeforeMutation(VisitorWorkflowAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => VisitorWorkflowTool.Execute(TestHelpers.FakeManager(handler),
            new VisitorWorkflowRequest(action, Id: id, EscortId: id)));
        Assert.Equal("id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }


    [Fact]
    public void VisitorState_ContainsOnlySupportedVisitorStates()
    {
        Assert.Equal(["Active", "Archived"], Enum.GetNames<VisitorState>());
    }

    [Fact]
    public async Task ActivityReport_WithoutTimeRange_PreservesEntityFilter()
    {
        var handler = new Tools.ToolTestHandler();
        var id = Guid.NewGuid();

        await VisitorWorkflowTool.Execute(TestHelpers.FakeManager(handler),
            new VisitorWorkflowRequest(VisitorWorkflowAction.ActivityReport, Id: id));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            $"/WebSdk/report/VisitorActivity?q=Visitor={id},Events*",
            Uri.UnescapeDataString(request.AbsolutePath + request.Query));
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task CheckIn_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            VisitorWorkflowTool.Execute(TestHelpers.FakeManager(),
                new VisitorWorkflowRequest(VisitorWorkflowAction.CheckIn, Id: null)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssignEscorts_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            VisitorWorkflowTool.Execute(TestHelpers.FakeManager(),
                new VisitorWorkflowRequest(VisitorWorkflowAction.AssignEscorts, Id: null, EscortId: Guid.NewGuid())));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssignEscorts_NullEscortId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            VisitorWorkflowTool.Execute(TestHelpers.FakeManager(),
                new VisitorWorkflowRequest(VisitorWorkflowAction.AssignEscorts, Id: Guid.NewGuid(), EscortId: null)));

        Assert.Contains("escort_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchByStatus_NullStatus_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            VisitorWorkflowTool.Execute(TestHelpers.FakeManager(),
                new VisitorWorkflowRequest(VisitorWorkflowAction.SearchByStatus, Status: null)));

        Assert.Contains("status", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActivityReport_IncludesAllEventsFilter()
    {
        var handler = new ToolTestHandler();
        var manager = TestHelpers.FakeManager(handler);
        var visitorId = Guid.NewGuid();

        await VisitorWorkflowTool.Execute(manager,
            new VisitorWorkflowRequest(
                VisitorWorkflowAction.ActivityReport,
                Id: visitorId,
                TimeStart: "2026-01-01T00:00:00Z",
                TimeEnd: "2026-01-02T00:00:00Z"));

        var uri = Uri.UnescapeDataString(Assert.Single(handler.Requests).ToString());
        Assert.Contains(
            $"report/VisitorActivity?q=Visitor={visitorId},Events*",
            uri,
            StringComparison.Ordinal);
        Assert.Contains("Events*", uri, StringComparison.Ordinal);
        Assert.Contains("TimeRange.SetTimeRange(2026-01-01T00:00:00Z,2026-01-02T00:00:00Z)", uri, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActivityReport_WithOnlyOneTimeBoundary_ThrowsArgumentException(bool provideStart)
    {
        var request = new VisitorWorkflowRequest(
            VisitorWorkflowAction.ActivityReport,
            Id: Guid.NewGuid(),
            TimeStart: provideStart ? "2026-01-01T00:00:00Z" : null,
            TimeEnd: provideStart ? null : "2026-01-02T00:00:00Z");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            VisitorWorkflowTool.Execute(TestHelpers.FakeManager(), request));

        Assert.Contains("time_start", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("time_end", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
