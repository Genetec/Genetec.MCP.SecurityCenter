using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Administration;
using System.Text.Json;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class ActiveDirectoryRoleManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    public static IEnumerable<object[]> CurrentEntityActions() => Enum.GetValues<ActiveDirectoryRoleAction>().Where(action => action is ActiveDirectoryRoleAction.Get or ActiveDirectoryRoleAction.Sync).Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(CurrentEntityActions))]
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity(ActiveDirectoryRoleAction action)
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => ActiveDirectoryRoleManagementTool.Execute(sc.Manager, new ActiveDirectoryRoleManagementRequest(action, Id: id)), "id");
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var result = await ActiveDirectoryRoleManagementTool.Execute(sc.Manager,
            new ActiveDirectoryRoleManagementRequest(ActiveDirectoryRoleAction.List));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Get_FirstListedRole_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var listJson = (string)await ActiveDirectoryRoleManagementTool.Execute(sc.Manager,
            new ActiveDirectoryRoleManagementRequest(ActiveDirectoryRoleAction.List));
        var guid = ScIntegrationFixture.ExtractFirstGuid(listJson);
        Assert.True(guid is not null, "No roles found in Security Center.");

        var result = await ActiveDirectoryRoleManagementTool.Execute(sc.Manager,
            new ActiveDirectoryRoleManagementRequest(ActiveDirectoryRoleAction.Get, Id: guid));
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sync_ExistingActiveDirectoryRole_AcceptsCommandAndPreservesImportedGroups(bool synchronizeImages)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var client = sc.Manager.GetClient();
        using var rolesDocument = IntegrationTestHelpers.ParseJson(await client.ListEntities("Role", 50, 1, ["Type"]));
        var roles = IntegrationTestHelpers.GetResult(rolesDocument.RootElement);
        var role = roles.EnumerateArray().FirstOrDefault(item => item.GetProperty("Type").GetString() == "ActiveDirectory");
        Assert.True(role.ValueKind == JsonValueKind.Object,
            "Synchronization integration requires an existing Active Directory role in the first 50 roles.");
        var roleId = role.GetProperty("Guid").GetGuid();
        using var originalDocument = IntegrationTestHelpers.ParseJson(await client.GetEntityFields(roleId.ToString(), "Guid", "Type", "ImportedGroups"));
        var original = IntegrationTestHelpers.GetResult(originalDocument.RootElement);
        Assert.Equal(roleId, original.GetProperty("Guid").GetGuid());
        Assert.Equal("ActiveDirectory", original.GetProperty("Type").GetString());

        var result = Assert.IsType<string>(await ActiveDirectoryRoleManagementTool.Execute(sc.Manager,
            new ActiveDirectoryRoleManagementRequest(ActiveDirectoryRoleAction.Sync, Id: roleId, SynchronizeImages: synchronizeImages)));
        using var resultDocument = IntegrationTestHelpers.ParseJson(result);
        Assert.Equal("Ok", resultDocument.RootElement.GetProperty("status").GetString());

        using var updatedDocument = IntegrationTestHelpers.ParseJson(await client.GetEntityFields(roleId.ToString(), "Guid", "Type", "ImportedGroups"));
        var updated = IntegrationTestHelpers.GetResult(updatedDocument.RootElement);
        Assert.Equal(roleId, updated.GetProperty("Guid").GetGuid());
        Assert.Equal("ActiveDirectory", updated.GetProperty("Type").GetString());
        Assert.Equal(original.GetProperty("ImportedGroups").GetRawText(), updated.GetProperty("ImportedGroups").GetRawText());
    }

}
