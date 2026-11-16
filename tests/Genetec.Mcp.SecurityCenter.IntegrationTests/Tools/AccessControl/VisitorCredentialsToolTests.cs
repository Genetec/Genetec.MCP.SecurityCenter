using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class VisitorCredentialsToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(VisitorCredentialsAction.AddCredential, "credential_id")]
    [InlineData(VisitorCredentialsAction.RemoveCredential, "credential_id")]
    public async Task WrongLinkedType_RejectsAndPreservesPrimary(VisitorCredentialsAction action, string parameter)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var id = await CreateVisitor();
        await IntegrationTestHelpers.AssertWrongRelatedEntityTypeAsync(sc, id, "Visitor", wrong => (action, parameter) switch
        {
            (VisitorCredentialsAction.AddCredential, "credential_id") => VisitorCredentialsTool.Execute(sc.Manager, new VisitorCredentialsRequest(action, id, CredentialId: wrong)),
            (VisitorCredentialsAction.RemoveCredential, "credential_id") => VisitorCredentialsTool.Execute(sc.Manager, new VisitorCredentialsRequest(action, id, CredentialId: wrong)),
            _ => throw new InvalidOperationException()
        }, parameter);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<VisitorCredentialsAction>()
        .Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsAndPreservesUserGroup(VisitorCredentialsAction action)
    {
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var id = wrong.Id;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => VisitorCredentialsTool.Execute(sc.Manager,
            new VisitorCredentialsRequest(action, Id: id, CredentialId: id)));
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

    private async Task<Guid> CreatePinCredential()
    {
        var credCreate = await CredentialCreateTool.Execute(sc.Manager,
            new CredentialCreateRequest([new CredentialItem($"MCP_Cred_{Guid.NewGuid():N}", CredentialFormatType.Pin, Pin: "7890")]));
        var cred = Assert.IsType<EntityCreateResult>(credCreate);
        sc.TrackForCleanup(cred.Guid);
        return cred.Guid;
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task GetCredentials_ExistingVisitor_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var visitorId = await CreateVisitor();

        var result = await VisitorCredentialsTool.Execute(sc.Manager,
            new VisitorCredentialsRequest(VisitorCredentialsAction.GetCredentials, Id: visitorId));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task AddCredential_AndRemove_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var visitorId = await CreateVisitor();
        var credId = await CreatePinCredential();

        var addResult = await VisitorCredentialsTool.Execute(sc.Manager,
            new VisitorCredentialsRequest(VisitorCredentialsAction.AddCredential,
                Id: visitorId, CredentialId: credId));
        Assert.NotNull(addResult);

        var removeResult = await VisitorCredentialsTool.Execute(sc.Manager,
            new VisitorCredentialsRequest(VisitorCredentialsAction.RemoveCredential,
                Id: visitorId, CredentialId: credId));
        Assert.NotNull(removeResult);
    }

    [Fact]
    public async Task GetPicture_ExistingVisitor_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var visitorId = await CreateVisitor();

        var result = await VisitorCredentialsTool.Execute(sc.Manager,
            new VisitorCredentialsRequest(VisitorCredentialsAction.GetPicture, Id: visitorId));

        Assert.NotNull(result);
    }

}
