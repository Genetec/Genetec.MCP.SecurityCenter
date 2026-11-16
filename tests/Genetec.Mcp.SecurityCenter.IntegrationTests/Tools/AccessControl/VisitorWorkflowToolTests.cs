using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class VisitorWorkflowToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(VisitorWorkflowAction.AssignEscorts, "escort_id")]
    [InlineData(VisitorWorkflowAction.AssignEscorts, "escort2_id")]
    public async Task WrongLinkedType_RejectsAndPreservesPrimary(VisitorWorkflowAction action, string parameter)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var id = await CreateVisitor();
        var validEscort = Assert.IsType<EntityCreateResult>(await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem($"MCP_Escort_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(validEscort.Guid);
        var escort = validEscort.Guid;
        await IntegrationTestHelpers.AssertWrongRelatedEntityTypeAsync(sc, id, "Visitor", wrong => (action, parameter) switch
        {
            (VisitorWorkflowAction.AssignEscorts, "escort_id") => VisitorWorkflowTool.Execute(sc.Manager, new VisitorWorkflowRequest(action, Id: id, EscortId: wrong)),
            (VisitorWorkflowAction.AssignEscorts, "escort2_id") => VisitorWorkflowTool.Execute(sc.Manager, new VisitorWorkflowRequest(action, Id: id, EscortId: escort, Escort2Id: wrong)),
            _ => throw new InvalidOperationException()
        }, parameter);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<VisitorWorkflowAction>()
        .Where(action => action != VisitorWorkflowAction.ActivityReport && action != VisitorWorkflowAction.SearchByStatus).Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsAndPreservesUserGroup(VisitorWorkflowAction action)
    {
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var id = wrong.Id;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => VisitorWorkflowTool.Execute(sc.Manager,
            new VisitorWorkflowRequest(action, Id: id, EscortId: id)));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", wrong.Name);
    }


    private async Task<Guid> CreateVisitor()
    {
        var createResult = await VisitorCreateTool.Execute(sc.Manager,
            new VisitorCreateRequest([new VisitorItem($"MCP_Visitor_{Guid.NewGuid():N}")]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);
        return created.Guid;
    }

    // ── Integration ─────────────────────────────────────────────────

    [Theory]
    [InlineData(VisitorState.Active)]
    [InlineData(VisitorState.Archived)]
    public async Task SearchByStatus_ValidStatus_ReturnsResult(VisitorState status)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await VisitorWorkflowTool.Execute(sc.Manager,
            new VisitorWorkflowRequest(VisitorWorkflowAction.SearchByStatus, Status: status));

        Assert.NotNull(result);
        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task CheckIn_ExistingVisitor_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var visitorId = await CreateVisitor();

        var result = await VisitorWorkflowTool.Execute(sc.Manager,
            new VisitorWorkflowRequest(VisitorWorkflowAction.CheckIn, Id: visitorId));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task CheckOut_ExistingVisitor_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var visitorId = await CreateVisitor();

        // Check in first so checkout has something to act on
        await VisitorWorkflowTool.Execute(sc.Manager,
            new VisitorWorkflowRequest(VisitorWorkflowAction.CheckIn, Id: visitorId));

        var result = await VisitorWorkflowTool.Execute(sc.Manager,
            new VisitorWorkflowRequest(VisitorWorkflowAction.CheckOut, Id: visitorId));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ActivityReport_ExistingVisitor_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var visitorId = await CreateVisitor();

        var result = await VisitorWorkflowTool.Execute(sc.Manager,
            new VisitorWorkflowRequest(VisitorWorkflowAction.ActivityReport, Id: visitorId));

        Assert.NotNull(result);

        var now = DateTimeOffset.UtcNow;
        var timeStart = now.AddDays(-1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var timeEnd = now.AddDays(1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var boundedResult = await VisitorWorkflowTool.Execute(sc.Manager,
            new VisitorWorkflowRequest(VisitorWorkflowAction.ActivityReport,
                Id: visitorId, TimeStart: timeStart, TimeEnd: timeEnd));

        Assert.NotNull(boundedResult);
    }

    [Fact]
    public async Task AssignEscorts_ExistingVisitorAndCardholder_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var visitorId = await CreateVisitor();

        var chCreate = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem($"MCP_CH_{Guid.NewGuid():N}")]));
        var ch = Assert.IsType<EntityCreateResult>(chCreate);
        sc.TrackForCleanup(ch.Guid);

        // CanEscort must be true for the cardholder to be assigned as an escort
        await sc.Manager.GetClient().UpdateEntity(ch.Guid.ToString(), new { CanEscort = true });

        var result = await VisitorWorkflowTool.Execute(sc.Manager,
            new VisitorWorkflowRequest(VisitorWorkflowAction.AssignEscorts,
                Id: visitorId, EscortId: ch.Guid));

        Assert.NotNull(result);
    }
}
