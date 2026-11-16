using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using System.Net;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Lpr;

public class PermitRuleManagementToolTests
{
    private static readonly Guid RuleId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(request.Method == HttpMethod.Get &&
                    (request.RequestUri!.AbsolutePath.EndsWith(RuleId.ToString(), StringComparison.Ordinal) ||
                     Uri.UnescapeDataString(request.RequestUri.Query).Contains("Guid,EntityType", StringComparison.Ordinal))
                    ? $"{{\"Guid\":\"{RuleId}\",\"EntityType\":\"PermitRule\"}}" : "{}")
            });
        }
    }

    [Fact]
    public void Contract_ExposesOnlySupportedWebSdkActions()
    {
        Assert.Equal(
            ["Get", "List", "Update"],
            Enum.GetNames<PermitRuleAction>());
    }

    [Theory]
    [InlineData(PermitRuleAction.Get, "get")]
    [InlineData(PermitRuleAction.Update, "update")]
    public async Task Actions_MissingId_ThrowsArgumentException(
        PermitRuleAction action, string actionName)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            PermitRuleManagementTool.Execute(TestHelpers.FakeManager(),
                new PermitRuleManagementRequest(action)));

        Assert.Contains(action is PermitRuleAction.Update ? "items" : "id", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(actionName, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_MissingBodyAndName_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            PermitRuleManagementTool.Execute(TestHelpers.FakeManager(),
                new PermitRuleManagementRequest(PermitRuleAction.Update, Items: [new PermitRuleManagementItem(Id: RuleId)])));

        Assert.Contains("body", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_WhitespaceName_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            PermitRuleManagementTool.Execute(TestHelpers.FakeManager(),
                new PermitRuleManagementRequest(PermitRuleAction.Update, Items: [new PermitRuleManagementItem(Name: "   ", Id: RuleId)])));

        Assert.Contains("name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task List_UsesPermitRuleEntityType()
    {
        var handler = new CapturingHandler();

        var result = await PermitRuleManagementTool.Execute(TestHelpers.FakeManager(handler),
            new PermitRuleManagementRequest(PermitRuleAction.List));

        Assert.IsType<string>(result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains("report/EntityConfiguration?q=EntityTypes@PermitRule,PageSize=50,Page=1",
            Unescape(request), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_UsesEntityPath()
    {
        var handler = new CapturingHandler();

        var result = await PermitRuleManagementTool.Execute(TestHelpers.FakeManager(handler),
            new PermitRuleManagementRequest(PermitRuleAction.Get, Id: RuleId));

        Assert.IsType<string>(result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains($"entity/{RuleId}", Unescape(request), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_Name_UsesEntityUpdateQuery()
    {
        var handler = new CapturingHandler();

        var result = await PermitRuleManagementTool.Execute(TestHelpers.FakeManager(handler),
            new PermitRuleManagementRequest(PermitRuleAction.Update, Items: [new PermitRuleManagementItem(Name: "Updated Permit Rule", Id: RuleId)]));

        Assert.IsType<string>(result);
        var request = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains($"entity={RuleId},Name=Updated Permit Rule", Unescape(request), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_Body_UsesEntityUpdateQuery()
    {
        var handler = new CapturingHandler();

        var result = await PermitRuleManagementTool.Execute(TestHelpers.FakeManager(handler),
            new PermitRuleManagementRequest(PermitRuleAction.Update, Items: [new PermitRuleManagementItem(Id: RuleId, Body: new PermitRuleUpdateProperties(Color: "11223344"))]));

        Assert.IsType<string>(result);
        var request = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains($"entity={RuleId},Color=11223344", Unescape(request), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownAction_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            PermitRuleManagementTool.Execute(TestHelpers.FakeManager(),
                new PermitRuleManagementRequest((PermitRuleAction)999)));

        Assert.Contains("Unknown action", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string Unescape(HttpRequestMessage request)
        => Uri.UnescapeDataString(request.RequestUri!.ToString());

    [Theory]
    [InlineData(PermitRuleAction.Get)]
    [InlineData(PermitRuleAction.Update)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(PermitRuleAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => PermitRuleManagementTool.Execute(TestHelpers.FakeManager(handler),
            (action is PermitRuleAction.Update ? new PermitRuleManagementRequest(action, Items: [new PermitRuleManagementItem(Id: id, Body: action == PermitRuleAction.Update ? new PermitRuleUpdateProperties(Name: "Must not update") : null)]) : new PermitRuleManagementRequest(action, Id: id))));
        Assert.Equal(action is PermitRuleAction.Update ? "items" : "id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }
}
