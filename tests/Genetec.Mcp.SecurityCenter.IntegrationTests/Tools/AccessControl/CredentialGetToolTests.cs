using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class CredentialGetToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Get_ExistingCredential_ReturnsJson()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Cred_{Guid.NewGuid():N}";

        var createResult = await CredentialCreateTool.Execute(sc.Manager,
            new CredentialCreateRequest([new CredentialItem(name, CredentialFormatType.Pin, Pin: "5678")]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);

        var result = await CredentialGetTool.Execute(sc.Manager, new CredentialGetTool.CredentialGetRequest(created.Guid));

        Assert.NotNull(result);
        Assert.Contains(name, (string)result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_UserGroupGuid_RejectsCredentialLookupWithoutChangingUserGroup()
    {
        var wrongTarget = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => CredentialGetTool.Execute(sc.Manager,
            new CredentialGetTool.CredentialGetRequest(wrongTarget.Id)));

        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongTarget.Id, "UserGroup", wrongTarget.Name);
    }
}
