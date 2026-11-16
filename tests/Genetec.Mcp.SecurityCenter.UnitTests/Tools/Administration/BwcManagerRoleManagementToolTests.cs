using System.Net;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class BwcManagerRoleManagementToolTests
{
    [Fact]
    public async Task CurrentEntityTargets_RejectWrongEntityType()
    {
        await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => BwcManagerRoleManagementTool.Execute(manager, new BwcManagerRoleManagementRequest(BwcManagerRoleAction.Get, Id: id)), "id");
    }

    private static readonly Guid RoleId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private const string SdkResponse = "{\"Guid\":\"11111111-2222-3333-4444-555555555555\",\"EntityType\":\"Role\",\"relation\":\"updated\"}";

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SdkResponse)
            });
        }
    }

    // ── Surface ─────────────────────────────────────────────────────

    [Fact]
    public void SupportedActions_ContainAllBwcManagerOperations()
    {
        Assert.Equal(
            [
                BwcManagerRoleAction.List,
                BwcManagerRoleAction.Get,
                BwcManagerRoleAction.Create
            ],
            Enum.GetValues<BwcManagerRoleAction>());
    }

    // ── Dispatch and Web SDK routes ─────────────────────────────────

    [Fact]
    public async Task List_ReturnsRawSdkResponse()
    {
        var handler = new CapturingHandler();

        var result = await BwcManagerRoleManagementTool.Execute(TestHelpers.FakeManager(handler),
            new BwcManagerRoleManagementRequest(BwcManagerRoleAction.List));

        Assert.Equal(SdkResponse, result);
        Assert.Single(handler.Requests);
        Assert.Contains("report/EntityConfiguration", handler.Requests[0].RequestUri!.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_ReturnsRawSdkResponse()
    {
        var handler = new CapturingHandler();

        var result = await BwcManagerRoleManagementTool.Execute(TestHelpers.FakeManager(handler),
            new BwcManagerRoleManagementRequest(BwcManagerRoleAction.Get, Id: RoleId));

        Assert.Equal(SdkResponse, result);
        Assert.Equal($"https://localhost:4590/WebSdk/entity/{RoleId}",
            handler.Requests.Single().RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateSingle_UsesBwcManagerRoleType()
    {
        var handler = new CapturingHandler();

        var result = await BwcManagerRoleManagementTool.Execute(TestHelpers.FakeManager(handler),
            new BwcManagerRoleManagementRequest(BwcManagerRoleAction.Create, Items: ["BWC role"]));

        Assert.IsType<EntityCreateResult>(result);
        var requestUri = handler.Requests.Single().RequestUri!;
        var query = Uri.UnescapeDataString(requestUri.Query);
        Assert.Contains("Type=BwcManager", query, StringComparison.Ordinal);
        Assert.Contains("Name=BWC role", query, StringComparison.Ordinal);
        Assert.Contains("%3D", requestUri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%20", requestUri.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateBatch_ReturnsRawCreateGuids()
    {
        var handler = new CapturingHandler();

        var result = await BwcManagerRoleManagementTool.Execute(TestHelpers.FakeManager(handler),
            new BwcManagerRoleManagementRequest(BwcManagerRoleAction.Create, Items: ["BWC role"]));

        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.Equal(RoleId, created.Guid);
        var requestUri = handler.Requests.Single().RequestUri!;
        Assert.Contains("Type=BwcManager", Uri.UnescapeDataString(requestUri.Query), StringComparison.Ordinal);
        Assert.Contains("%3D", requestUri.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownAction_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            BwcManagerRoleManagementTool.Execute(TestHelpers.FakeManager(),
                new BwcManagerRoleManagementRequest((BwcManagerRoleAction)999)));

        Assert.Contains("Unknown action", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_MissingId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            BwcManagerRoleManagementTool.Execute(TestHelpers.FakeManager(),
                new BwcManagerRoleManagementRequest(BwcManagerRoleAction.Get)));
        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_InvalidItemName_ThrowsArgumentException(string? name)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            BwcManagerRoleManagementTool.Execute(TestHelpers.FakeManager(),
                new BwcManagerRoleManagementRequest(BwcManagerRoleAction.Create, Items: [name!])));
        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_MissingItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            BwcManagerRoleManagementTool.Execute(TestHelpers.FakeManager(),
                new BwcManagerRoleManagementRequest(BwcManagerRoleAction.Create)));
        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

}
