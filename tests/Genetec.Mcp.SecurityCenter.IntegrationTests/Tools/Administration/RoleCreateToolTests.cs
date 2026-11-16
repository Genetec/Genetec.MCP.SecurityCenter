using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Administration;
using static Genetec.Mcp.SecurityCenter.Tools.Administration.ScRoleType;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class RoleCreateToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    public static IEnumerable<object[]> RoleTypes()
        => Enum.GetValues<ScRoleType>().Select(type => new object[] { type });
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task SingleCreate_AccessControlRole_ReturnsValidGuid()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var name = $"MCP_Role_{Guid.NewGuid():N}";
        var result = await RoleCreateTool.Execute(sc.Manager,
            new RoleCreateRequest(Items: [new RoleItem(Name: name, RoleType: AccessManager)]));

        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
    }

    [Fact]
    public async Task BatchCreate_ReturnsExpectedCounts()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        const int count = 2;
        var items = Enumerable.Range(1, count)
            .Select(i => new RoleItem(Name: $"MCP_RoleBatch_{i}_{Guid.NewGuid():N}", RoleType: AccessManager))
            .ToList();

        var result = await RoleCreateTool.Execute(sc.Manager, new RoleCreateRequest(Items: items));

        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(count, batch.RequestedCount);
        Assert.Equal(count, batch.SuccessCount);
        Assert.All(batch.Results, r =>
        {
            Assert.True(r.Success);
            Assert.True(r.Guid.HasValue);
            sc.TrackForCleanup(r.Guid!);
        });
    }

    [Theory]
    [MemberData(nameof(RoleTypes))]
    public async Task EveryRoleType_ValidInputReachesSecurityCenter(ScRoleType roleType)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var exception = await Record.ExceptionAsync(async () =>
        {
            var result = Assert.IsType<EntityCreateResult>(await RoleCreateTool.Execute(sc.Manager,
                new RoleCreateRequest(Items: [new RoleItem($"MCP_Role_{roleType}_{Guid.NewGuid():N}", roleType)])));
            Assert.NotEqual(Guid.Empty, result.Guid);
            sc.TrackForCleanup(result.Guid);
            Assert.NotNull(await RoleGetTool.Execute(sc.Manager, new RoleGetTool.RoleGetRequest(result.Guid)));
        });

        Assert.True(exception is null or not ArgumentException, exception?.ToString());
    }
}
