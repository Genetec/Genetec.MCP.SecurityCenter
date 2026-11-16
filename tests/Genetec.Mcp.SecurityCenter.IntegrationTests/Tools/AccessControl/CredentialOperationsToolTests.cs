using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class CredentialOperationsToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(CredentialOperationsAction.AssignToCardholder, "cardholder_id")]
    [InlineData(CredentialOperationsAction.RemoveFromCardholder, "cardholder_id")]
    public async Task WrongLinkedType_RejectsAndPreservesPrimary(CredentialOperationsAction action, string parameter)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var created = Assert.IsType<EntityCreateResult>(await CredentialCreateTool.Execute(sc.Manager,
            new CredentialCreateRequest([new CredentialItem($"MCP_Linked_{Guid.NewGuid():N}", CredentialFormatType.Pin, Pin: "7777")])));
        sc.TrackForCleanup(created.Guid);
        var id = created.Guid;
        await IntegrationTestHelpers.AssertWrongRelatedEntityTypeAsync(sc, id, "Credential", wrong => (action, parameter) switch
        {
            (CredentialOperationsAction.AssignToCardholder, "cardholder_id") => CredentialOperationsTool.Execute(sc.Manager, new CredentialOperationsRequest(action, Id: id, CardholderId: wrong)),
            (CredentialOperationsAction.RemoveFromCardholder, "cardholder_id") => CredentialOperationsTool.Execute(sc.Manager, new CredentialOperationsRequest(action, Id: id, CardholderId: wrong)),
            _ => throw new InvalidOperationException()
        }, parameter);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<CredentialOperationsAction>()
        .Where(action => action != CredentialOperationsAction.ActivityReport).Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsAndPreservesUserGroup(CredentialOperationsAction action)
    {
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var id = wrong.Id;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CredentialOperationsTool.Execute(sc.Manager,
            new CredentialOperationsRequest(action, Id: id, CardholderId: id)));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", wrong.Name);
    }


    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task AssignToCardholder_AndRemove_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var credCreate = await CredentialCreateTool.Execute(sc.Manager,
            new CredentialCreateRequest([new CredentialItem($"MCP_Cred_{Guid.NewGuid():N}", CredentialFormatType.Pin, Pin: "8888")]));
        var cred = Assert.IsType<EntityCreateResult>(credCreate);
        sc.TrackForCleanup(cred.Guid);

        var chCreate = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem($"MCP_CH_{Guid.NewGuid():N}")]));
        var ch = Assert.IsType<EntityCreateResult>(chCreate);
        sc.TrackForCleanup(ch.Guid);

        var assignResult = await CredentialOperationsTool.Execute(sc.Manager,
            new CredentialOperationsRequest(CredentialOperationsAction.AssignToCardholder,
                Id: cred.Guid, CardholderId: ch.Guid));
        Assert.NotNull(assignResult);

        var removeResult = await CredentialOperationsTool.Execute(sc.Manager,
            new CredentialOperationsRequest(CredentialOperationsAction.RemoveFromCardholder,
                Id: cred.Guid, CardholderId: ch.Guid));
        Assert.NotNull(removeResult);
    }

    [Fact]
    public async Task ActivityReport_ExistingCredential_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var credCreate = await CredentialCreateTool.Execute(sc.Manager,
            new CredentialCreateRequest([new CredentialItem($"MCP_Cred_{Guid.NewGuid():N}", CredentialFormatType.Pin, Pin: "5555")]));
        var cred = Assert.IsType<EntityCreateResult>(credCreate);
        sc.TrackForCleanup(cred.Guid);

        var result = await CredentialOperationsTool.Execute(sc.Manager,
            new CredentialOperationsRequest(CredentialOperationsAction.ActivityReport, Id: cred.Guid));

        Assert.NotNull(result);

        var now = DateTimeOffset.UtcNow;
        var timeStart = now.AddDays(-1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var timeEnd = now.AddDays(1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var boundedResult = await CredentialOperationsTool.Execute(sc.Manager,
            new CredentialOperationsRequest(CredentialOperationsAction.ActivityReport,
                Id: cred.Guid, TimeStart: timeStart, TimeEnd: timeEnd));

        Assert.NotNull(boundedResult);
    }
}
