using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class BwcManagerRoleManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity()
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => BwcManagerRoleManagementTool.Execute(sc.Manager, new BwcManagerRoleManagementRequest(BwcManagerRoleAction.Get, Id: id)), "id");
    }

    [Fact]
    public async Task CreateSingle_AndGet_ReturnsBwcManagerRole()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var name = $"MCP_BwcManager_{Guid.NewGuid():N}";
        var created = Assert.IsType<EntityCreateResult>(await BwcManagerRoleManagementTool.Execute(
            sc.Manager,
            new BwcManagerRoleManagementRequest(BwcManagerRoleAction.Create, Items: [name])));
        sc.TrackForCleanup(created.Guid);

        var json = Assert.IsType<string>(await BwcManagerRoleManagementTool.Execute(
            sc.Manager,
            new BwcManagerRoleManagementRequest(BwcManagerRoleAction.Get, Id: created.Guid)));

        using var document = JsonDocument.Parse(json);
        Assert.Equal(name, document.RootElement.GetProperty("Name").GetString());
        Assert.Equal("BwcManager", document.RootElement.GetProperty("Type").GetString());
    }

    [Fact]
    public async Task CreateBatch_ReturnsCreatedRoleIds()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var result = Assert.IsType<BatchCreateResult>(await BwcManagerRoleManagementTool.Execute(
            sc.Manager,
            new BwcManagerRoleManagementRequest(
                BwcManagerRoleAction.Create,
                Items:
                [
                    $"MCP_BwcManager_{Guid.NewGuid():N}",
                    $"MCP_BwcManager_{Guid.NewGuid():N}"
                ])));

        Assert.Equal(2, result.RequestedCount);
        Assert.Equal(2, result.SuccessCount);
        Assert.All(result.Results, item =>
        {
            Assert.True(item.Success);
            Assert.True(item.Guid.HasValue);
            sc.TrackForCleanup(item.Guid);
        });
    }
}
