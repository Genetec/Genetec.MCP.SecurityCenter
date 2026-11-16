using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class CardholderOperationsToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(CardholderOperationsAction.AddCredential)]
    [InlineData(CardholderOperationsAction.RemoveCredential)]
    public async Task MixedCredentialBatch_RejectsWrongTypeAndReadsBackSuccessfulChange(CardholderOperationsAction action)
    {
        var holder = Assert.IsType<EntityCreateResult>(await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem($"MCP_MixedCred_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(holder.Guid);
        var credential = Assert.IsType<EntityCreateResult>(await CredentialCreateTool.Execute(sc.Manager,
            new CredentialCreateRequest([new CredentialItem($"MCP_MixedCred_{Guid.NewGuid():N}", CredentialFormatType.Pin,
                Pin: Random.Shared.Next(1000000, 9999999).ToString())])));
        sc.TrackForCleanup(credential.Guid);
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        if (action == CardholderOperationsAction.RemoveCredential)
            await CardholderOperationsTool.Execute(sc.Manager,
                new CardholderOperationsRequest(CardholderOperationsAction.AddCredential, Id: holder.Guid, CredentialIds: [credential.Guid]));
        var batch = Assert.IsType<Genetec.Mcp.SecurityCenter.Tools.BatchCreateResult>(await CardholderOperationsTool.Execute(sc.Manager,
            new CardholderOperationsRequest(action, Id: holder.Guid, CredentialIds: [credential.Guid, wrong.Id])));
        Assert.Equal(1, batch.SuccessCount);
        Assert.Equal(1, batch.FailureCount);
        Assert.True(batch.Results[0].Success);
        Assert.False(batch.Results[1].Success);
        var membership = Assert.IsType<string>(await CardholderOperationsTool.Execute(sc.Manager,
            new CardholderOperationsRequest(CardholderOperationsAction.GetCredentials, Id: holder.Guid)));
        var ids = IntegrationTestHelpers.ExtractGuids(membership);
        if (action == CardholderOperationsAction.AddCredential) Assert.Contains(credential.Guid, ids);
        else Assert.DoesNotContain(credential.Guid, ids);
        Assert.DoesNotContain(wrong.Id, ids);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrong.Id, "UserGroup", wrong.Name);
    }

    [Theory]
    [InlineData(CardholderOperationsAction.AddCredential, "credential_ids")]
    [InlineData(CardholderOperationsAction.RemoveCredential, "credential_ids")]
    public async Task WrongLinkedType_RejectsAndPreservesPrimary(CardholderOperationsAction action, string parameter)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var created = Assert.IsType<EntityCreateResult>(await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem($"MCP_Linked_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(created.Guid);
        var id = created.Guid;
        await IntegrationTestHelpers.AssertWrongRelatedEntityTypeAsync(sc, id, "Cardholder", wrong => (action, parameter) switch
        {
            (CardholderOperationsAction.AddCredential, "credential_ids") => CardholderOperationsTool.Execute(sc.Manager, new CardholderOperationsRequest(action, Id: id, CredentialIds: [wrong])),
            (CardholderOperationsAction.RemoveCredential, "credential_ids") => CardholderOperationsTool.Execute(sc.Manager, new CardholderOperationsRequest(action, Id: id, CredentialIds: [wrong])),
            _ => throw new InvalidOperationException()
        }, parameter);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<CardholderOperationsAction>()
        .Where(action => action != CardholderOperationsAction.ActivityReport && action != CardholderOperationsAction.SearchByStatus && action != CardholderOperationsAction.SearchByExpiration).Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsAndPreservesUserGroup(CardholderOperationsAction action)
    {
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var id = wrong.Id;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CardholderOperationsTool.Execute(sc.Manager,
            new CardholderOperationsRequest(action, Id: id, CredentialIds: [id])));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", wrong.Name);
    }


    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task GetCredentials_ExistingCardholder_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Cardholder_{Guid.NewGuid():N}";

        var createResult = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);

        var result = await CardholderOperationsTool.Execute(sc.Manager,
            new CardholderOperationsRequest(
                CardholderOperationsAction.GetCredentials,
                Id: created.Guid));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetPicture_ExistingCardholder_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Cardholder_{Guid.NewGuid():N}";
        var createResult = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await CardholderOperationsTool.Execute(sc.Manager,
            new CardholderOperationsRequest(
                CardholderOperationsAction.GetPicture,
                Id: created.Guid));

        Assert.NotNull(result);
    }

    public static IEnumerable<object[]> AccessStatuses()
        => Enum.GetValues<AccessStatus>().Select(status => new object[] { status });

    [Theory]
    [MemberData(nameof(AccessStatuses))]
    public async Task SearchByStatus_AllAccessStatusValues_ReturnsResult(AccessStatus status)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await CardholderOperationsTool.Execute(sc.Manager,
            new CardholderOperationsRequest(
                CardholderOperationsAction.SearchByStatus,
                Status: status));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task SearchByExpiration_NoFilter_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await CardholderOperationsTool.Execute(sc.Manager,
            new CardholderOperationsRequest(
                CardholderOperationsAction.SearchByExpiration));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ActivityReport_ExistingCardholder_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Cardholder_{Guid.NewGuid():N}";
        var createResult = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await CardholderOperationsTool.Execute(sc.Manager,
            new CardholderOperationsRequest(
                CardholderOperationsAction.ActivityReport,
                Id: created.Guid));

        Assert.NotNull(result);

        var timeStart = DateTimeOffset.UtcNow.AddDays(-1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var timeEnd = DateTimeOffset.UtcNow.AddDays(1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var boundedResult = await CardholderOperationsTool.Execute(sc.Manager,
            new CardholderOperationsRequest(
                CardholderOperationsAction.ActivityReport,
                Id: created.Guid,
                TimeStart: timeStart,
                TimeEnd: timeEnd));

        Assert.NotNull(boundedResult);
    }

    [Fact]
    public async Task AddCredential_AndRemoveCredential_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var cardholderResult = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem($"MCP_Cardholder_{Guid.NewGuid():N}")]));
        var cardholder = Assert.IsType<EntityCreateResult>(cardholderResult);
        sc.TrackForCleanup(cardholder.Guid);

        var credentialResult = await CredentialCreateTool.Execute(sc.Manager,
            new CredentialCreateRequest([new CredentialItem($"MCP_Credential_{Guid.NewGuid():N}",
                CredentialFormatType.Pin, Pin: "2468")]));
        var credential = Assert.IsType<EntityCreateResult>(credentialResult);
        sc.TrackForCleanup(credential.Guid);

        var add = await CardholderOperationsTool.Execute(sc.Manager,
            new CardholderOperationsRequest(CardholderOperationsAction.AddCredential,
                Id: cardholder.Guid, CredentialIds: [credential.Guid]));
        var remove = await CardholderOperationsTool.Execute(sc.Manager,
            new CardholderOperationsRequest(CardholderOperationsAction.RemoveCredential,
                Id: cardholder.Guid, CredentialIds: [credential.Guid]));

        Assert.NotNull(add);
        Assert.NotNull(remove);
    }
}
