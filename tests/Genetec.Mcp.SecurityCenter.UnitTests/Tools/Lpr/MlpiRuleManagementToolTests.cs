using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using System.Net;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Lpr;

public class MlpiRuleManagementToolTests
{
    private static readonly Guid RuleId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid LprManagerId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri!.ToString().Contains(LprManagerId.ToString(), StringComparison.Ordinal)
                    ? $"{{\"Guid\":\"{LprManagerId}\",\"EntityType\":\"Role\"}}"
                    : $"{{\"Guid\":\"{RuleId}\",\"EntityType\":\"LprMlpiRule\"}}")
            });
        }
    }

    [Theory]
    [InlineData(MlpiRuleAction.Get, "get")]
    [InlineData(MlpiRuleAction.Update, "update")]
    [InlineData(MlpiRuleAction.GetLocations, "get_locations")]
    public async Task MissingId_ThrowsArgumentException(MlpiRuleAction action, string actionName)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            MlpiRuleManagementTool.Execute(TestHelpers.FakeManager(),
                new MlpiRuleManagementRequest(action)));

        Assert.Contains(action is MlpiRuleAction.Update ? "items" : "id", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(actionName, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_MissingBodyAndName_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            MlpiRuleManagementTool.Execute(TestHelpers.FakeManager(),
                new MlpiRuleManagementRequest(MlpiRuleAction.Update, Items: [new MlpiRuleManagementItem(Id: Guid.NewGuid())])));

        Assert.Contains("body", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_WhitespaceName_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            MlpiRuleManagementTool.Execute(TestHelpers.FakeManager(),
                new MlpiRuleManagementRequest(MlpiRuleAction.Update, Items: [new MlpiRuleManagementItem(Name: "   ", Id: Guid.NewGuid())])));

        Assert.Contains("name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task List_UsesMlpiRuleEntityType()
    {
        var handler = new CapturingHandler();

        var result = await MlpiRuleManagementTool.Execute(TestHelpers.FakeManager(handler),
            new MlpiRuleManagementRequest(MlpiRuleAction.List));

        Assert.IsType<string>(result);
        var request = handler.Requests.Last();
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains("report/EntityConfiguration?q=EntityTypes@LprMlpiRule,PageSize=50,Page=1",
            Unescape(request), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_UsesEntityPath()
    {
        var handler = new CapturingHandler();

        var result = await MlpiRuleManagementTool.Execute(TestHelpers.FakeManager(handler),
            new MlpiRuleManagementRequest(MlpiRuleAction.Get, Id: RuleId));

        Assert.IsType<string>(result);
        var request = handler.Requests.Last();
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains($"entity/{RuleId}", Unescape(request), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLocations_UsesLocationsFieldQuery()
    {
        var handler = new CapturingHandler();

        var result = await MlpiRuleManagementTool.Execute(TestHelpers.FakeManager(handler),
            new MlpiRuleManagementRequest(MlpiRuleAction.GetLocations, Id: RuleId));

        Assert.IsType<string>(result);
        var request = handler.Requests.Last();
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains($"entity={RuleId},Locations", Unescape(request), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_Name_UsesEntityUpdateQuery()
    {
        var handler = new CapturingHandler();

        var result = await MlpiRuleManagementTool.Execute(TestHelpers.FakeManager(handler),
            new MlpiRuleManagementRequest(MlpiRuleAction.Update, Items: [new MlpiRuleManagementItem(Name: "Updated MLPI Rule", Id: RuleId)]));

        Assert.IsType<string>(result);
        var request = handler.Requests.Last();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains($"entity={RuleId},Name=Updated MLPI Rule", Unescape(request), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_Body_UsesEntityUpdateQuery()
    {
        var handler = new CapturingHandler();

        var result = await MlpiRuleManagementTool.Execute(TestHelpers.FakeManager(handler),
            new MlpiRuleManagementRequest(MlpiRuleAction.Update, Items: [new MlpiRuleManagementItem(Id: RuleId, Body: new MlpiRuleUpdateProperties(LprManager: LprManagerId))]));

        Assert.IsType<string>(result);
        var request = handler.Requests.Last();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains($"entity={RuleId},LprManager={LprManagerId}", Unescape(request), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownAction_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            MlpiRuleManagementTool.Execute(TestHelpers.FakeManager(),
                new MlpiRuleManagementRequest((MlpiRuleAction)999)));

        Assert.Contains("Unknown action", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string Unescape(HttpRequestMessage request)
        => Uri.UnescapeDataString(request.RequestUri!.ToString());

    [Theory]
    [InlineData(MlpiRuleAction.Get)]
    [InlineData(MlpiRuleAction.Update)]
    [InlineData(MlpiRuleAction.GetLocations)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(MlpiRuleAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => MlpiRuleManagementTool.Execute(TestHelpers.FakeManager(handler),
            (action is MlpiRuleAction.Update ? new MlpiRuleManagementRequest(action, Items: [new MlpiRuleManagementItem(Id: id, Body: action == MlpiRuleAction.Update ? new MlpiRuleUpdateProperties(Name: "Must not update") : null)]) : new MlpiRuleManagementRequest(action, Id: id))));
        Assert.Equal(action is MlpiRuleAction.Update ? "items" : "id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Fact]
    public async Task Update_WrongLprManagerType_RejectsLinkedTarget()
    {
        var id = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "LprMlpiRule");
        handler.AddEntity(wrongId, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => MlpiRuleManagementTool.Execute(TestHelpers.FakeManager(handler),
            new MlpiRuleManagementRequest(MlpiRuleAction.Update, Items: [new MlpiRuleManagementItem(Id: id, Body: new MlpiRuleUpdateProperties(LprManager: wrongId))])));
        Assert.Equal("items.body.lpr_manager", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }
}
