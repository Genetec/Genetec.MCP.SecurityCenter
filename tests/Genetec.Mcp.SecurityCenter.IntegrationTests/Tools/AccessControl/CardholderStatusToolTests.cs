using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class CardholderStatusToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    private const string ActivationDate = "2030-01-01T00:00:00Z";
    private const string ExpirationDate = "2031-01-01T00:00:00Z";
    private const int UnusedDays = 30;
    private const int FirstUseDays = 45;

    public static IEnumerable<object[]> Actions => Enum.GetValues<CardholderStatusAction>()
        .Select(action => new object[] { action });

    public static IEnumerable<object[]> MutationCases => Enum.GetValues<CardholderStatusAction>()
        .SelectMany(action => new[] { new object[] { action, false }, new object[] { action, true } });

    private static CardholderStatusRequest Request(CardholderStatusAction action, List<Guid> items)
        => new(action, items, ActivationDate, ExpirationDate, UnusedDays, FirstUseDays);

    private async Task<Guid> CreateEntityAsync()
    {
        var result = Assert.IsType<EntityCreateResult>(await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem($"MCP_Status_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(result.Guid);
        return result.Guid;
    }

    private Task AssertStatusAsync(Guid id, CardholderStatusAction action)
        => IntegrationTestHelpers.AssertStatusMutationAsync(sc, id, "Cardholder", action.ToString(),
            ActivationDate, ExpirationDate, UnusedDays, FirstUseDays);

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task WrongEntityType_RejectsAndPreservesUserGroup(CardholderStatusAction action)
    {
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CardholderStatusTool.Execute(sc.Manager, Request(action, [wrong.Id])));
        Assert.Equal("items", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrong.Id, "UserGroup", wrong.Name);
    }

    [Theory]
    [MemberData(nameof(MutationCases))]
    public async Task Mutation_SingleAndBatch_ReadBackStatus(CardholderStatusAction action, bool batch)
    {
        var ids = new List<Guid> { await CreateEntityAsync() };
        if (batch) ids.Add(await CreateEntityAsync());
        // Start from a different status so no-op behavior cannot satisfy the assertions.
        var setup = action == CardholderStatusAction.Activate ? CardholderStatusAction.Deactivate
            : action == CardholderStatusAction.SetExpirationNever ? CardholderStatusAction.DeactivateOnDate : CardholderStatusAction.Activate;
        foreach (var id in ids)
            await CardholderStatusTool.Execute(sc.Manager, Request(setup, [id]));
        var result = await CardholderStatusTool.Execute(sc.Manager, Request(action, ids));
        if (batch)
        {
            var response = Assert.IsType<BatchMethodResult>(result);
            Assert.Equal(2, response.RequestedCount);
            Assert.Equal(2, response.SuccessCount);
            Assert.Equal(0, response.FailureCount);
            Assert.Equal(ids.Select(id => (Guid?)id), response.Results.Select(item => item.Id));
            Assert.All(response.Results, item => Assert.True(item.Success, item.Error));
        }
        foreach (var id in ids) await AssertStatusAsync(id, action);
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task MixedBatch_RejectsWrongTypeAndReadsBackSuccessfulStatus(CardholderStatusAction action)
    {
        var id = await CreateEntityAsync();
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var setup = action == CardholderStatusAction.Activate ? CardholderStatusAction.Deactivate
            : action == CardholderStatusAction.SetExpirationNever ? CardholderStatusAction.DeactivateOnDate : CardholderStatusAction.Activate;
        await CardholderStatusTool.Execute(sc.Manager, Request(setup, [id]));
        var response = Assert.IsType<BatchMethodResult>(await CardholderStatusTool.Execute(sc.Manager, Request(action, [wrong.Id, id])));
        Assert.Equal(2, response.RequestedCount);
        Assert.Equal(1, response.SuccessCount);
        Assert.Equal(1, response.FailureCount);
        Assert.Equal(wrong.Id, response.Results[0].Id);
        Assert.False(response.Results[0].Success);
        Assert.Contains("Cardholder", response.Results[0].Error);
        Assert.Equal(id, response.Results[1].Id);
        Assert.True(response.Results[1].Success, response.Results[1].Error);
        await AssertStatusAsync(id, action);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrong.Id, "UserGroup", wrong.Name);
    }
}
