using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class UserUpdateToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Update_CreatedUser_ReturnsStatusOk()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var created = (EntityCreateResult)await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem($"mcp_upd_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(created.Guid);

        const string firstName = "MCP, (updated) @person";
        var result = await UserUpdateTool.Execute(sc.Manager,
            new UserUpdateRequest([new UserUpdateItem(Id: created.Guid, FirstName: firstName, LastName: "User")]));

        Assert.IsType<string>(result);

        using var readback = JsonDocument.Parse(await sc.Manager.GetClient()
            .GetEntityFields(created.Guid.ToString(), "FirstName"));
        Assert.Equal(firstName, readback.RootElement.GetProperty("FirstName").GetString());

        await UserDeleteTool.Execute(sc.Manager, new UserDeleteRequest(Items: [created.Guid]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Update_WrongEntityType_RejectsSingleAndBatchAndPreservesEntity(int count)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var request = new UserUpdateRequest(Enumerable.Range(0, count)
            .Select(_ => new UserUpdateItem(id, Name: "Must not update")).ToList());
        if (count == 1)
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => UserUpdateTool.Execute(sc.Manager, request));
            Assert.Equal("items", error.ParamName);
        }
        else
        {
            using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(await UserUpdateTool.Execute(sc.Manager, request)));
            var result = IntegrationTestHelpers.GetResult(document.RootElement);
            Assert.Equal(0, result.GetProperty("success_count").GetInt32());
            Assert.Equal(count, result.GetProperty("failure_count").GetInt32());
        }
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
