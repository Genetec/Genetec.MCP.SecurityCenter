using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class CardholderUpdateToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Update_Name_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Cardholder_{Guid.NewGuid():N}";
        var createResult = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await CardholderUpdateTool.Execute(sc.Manager,
            new CardholderUpdateRequest([new CardholderUpdateItem(created.Guid, Name: $"{name}_Updated")]));
        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task Update_OptionalFields_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Cardholder_{Guid.NewGuid():N}";
        var createResult = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await CardholderUpdateTool.Execute(sc.Manager,
            new CardholderUpdateRequest([new CardholderUpdateItem(created.Guid,
                FirstName: "Jane", LastName: "Smith", EmailAddress: "jane.smith@example.com")]));
        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task Update_Batch_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name1 = $"MCP_Cardholder_{Guid.NewGuid():N}";
        var name2 = $"MCP_Cardholder_{Guid.NewGuid():N}";

        var create1 = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem(name1)]));
        var created1 = Assert.IsType<EntityCreateResult>(create1);
        sc.TrackForCleanup(created1.Guid);

        var create2 = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem(name2)]));
        var created2 = Assert.IsType<EntityCreateResult>(create2);
        sc.TrackForCleanup(created2.Guid);

        var result = await CardholderUpdateTool.Execute(sc.Manager,
            new CardholderUpdateRequest([
                new CardholderUpdateItem(created1.Guid, Name: $"{name1}_Updated"),
                new CardholderUpdateItem(created2.Guid, Name: $"{name2}_Updated")]));
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Update_WrongEntityType_RejectsSingleAndBatchAndPreservesEntity(int count)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var request = new CardholderUpdateRequest(Enumerable.Range(0, count)
            .Select(_ => new CardholderUpdateItem(id, Name: "Must not update")).ToList());
        if (count == 1)
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => CardholderUpdateTool.Execute(sc.Manager, request));
            Assert.Equal("items", error.ParamName);
        }
        else
        {
            using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(await CardholderUpdateTool.Execute(sc.Manager, request)));
            var result = IntegrationTestHelpers.GetResult(document.RootElement);
            Assert.Equal(0, result.GetProperty("success_count").GetInt32());
            Assert.Equal(count, result.GetProperty("failure_count").GetInt32());
        }
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
