using System.Diagnostics;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.VideoSurveillance;

public class ArchiverManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task Move_RejectsWrongDestinationType()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var units = await sc.Manager.GetClient().ListEntities("VideoUnit", 1, 1);
        var unitId = ScIntegrationFixture.ExtractFirstGuid(units);
        Assert.NotNull(unitId);
        await IntegrationTestHelpers.AssertWrongRelatedEntityTypeAsync(sc, unitId.Value, "VideoUnit", relatedId =>
            ArchiverManagementTool.Execute(sc.Manager, new ArchiverManagementRequest(ArchiverAction.MoveVideoUnit,
                VideoUnitId: unitId.Value, TargetArchiverId: relatedId)), "target_archiver_id");
    }

    [Fact]
    public async Task Move_RejectsWrongVideoUnitType()
        => await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => ArchiverManagementTool.Execute(sc.Manager,
            new ArchiverManagementRequest(ArchiverAction.MoveVideoUnit,
                VideoUnitId: id, TargetArchiverId: Guid.NewGuid())), "video_unit_id");

    [Theory]
    [InlineData(ArchiverAction.GetCameras)]
    [InlineData(ArchiverAction.GetAgents)]
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity(ArchiverAction action)
        => await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => ArchiverManagementTool.Execute(sc.Manager,
            new ArchiverManagementRequest(action, Id: id)), "id", "UserGroup");

    [Theory]
    [InlineData(ArchiverAction.GetCameras)]
    [InlineData(ArchiverAction.GetAgents)]
    public async Task Action_MissingArchiverId_RejectsBeforeMutation(ArchiverAction action)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => ArchiverManagementTool.Execute(sc.Manager,
            new ArchiverManagementRequest(action)));
        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ArchiverAction.GetCameras, "Cameras", "Camera")]
    [InlineData(ArchiverAction.GetAgents, "ArchiveSources", "Agent")]
    public async Task Get_Archiver_ReturnsMatchingEntityIdsAndPreservesCollection(ArchiverAction action, string field, string entityType)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var client = sc.Manager.GetClient();
        var archiverId = await GetArchiverIdAsync();
        var before = await ReadRelatedIdsAsync(archiverId, field);
        Assert.NotEmpty(before);
        using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(await ArchiverManagementTool.Execute(sc.Manager,
            new ArchiverManagementRequest(action, Id: archiverId))));
        var ids = IntegrationTestHelpers.GetResult(document.RootElement).GetProperty(field).EnumerateArray()
            .Select(value => value.GetGuid()).ToList();
        Assert.Equal(before, ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
        foreach (var id in ids.Take(3))
        {
            var fields = entityType == "Agent"
                ? new[] { "Guid", "EntityType", "AgentType", "RoleId", "ServerId" }
                : new[] { "Guid", "EntityType" };
            using var entityDocument = IntegrationTestHelpers.ParseJson(await client.GetEntityFields(id.ToString(), fields));
            var entity = IntegrationTestHelpers.GetResult(entityDocument.RootElement);
            Assert.Equal(id, entity.GetProperty("Guid").GetGuid());
            Assert.Equal(entityType, entity.GetProperty("EntityType").GetString());
            if (entityType == "Agent")
            {
                Assert.Equal("Archiver", entity.GetProperty("AgentType").GetString());
                Assert.Equal(archiverId, entity.GetProperty("RoleId").GetGuid());
                var serverId = entity.GetProperty("ServerId").GetGuid();
                Assert.NotEmpty(await IntegrationTestHelpers.GetEntityNameAsync(sc, serverId, "Server"));
            }
        }
        Assert.Equal(before, await ReadRelatedIdsAsync(archiverId, field));
    }

    [Fact]
    public async Task MoveVideoUnit_ChangesOwnerArchiverAndRestoresOriginalOwner()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var client = sc.Manager.GetClient();
        using var unitsDocument = IntegrationTestHelpers.ParseJson(await client.ListEntities(
            "VideoUnit", 10, 1, ["Name", "OwnerRole"]));
        var unit = IntegrationTestHelpers.GetRows(unitsDocument.RootElement).EnumerateArray().FirstOrDefault(item =>
            item.GetProperty("Name").GetString()!.StartsWith("GUSTAV", StringComparison.Ordinal));
        Assert.True(unit.ValueKind == System.Text.Json.JsonValueKind.Object,
            "Move integration requires a GUSTAV simulated video unit in the first 10 units.");
        var unitId = unit.GetProperty("Guid").GetGuid();
        var originalArchiverId = unit.GetProperty("OwnerRole").GetGuid();
        var destinationId = await GetArchiverIdAsync(originalArchiverId);
        var destinationCameras = await ReadRelatedIdsAsync(destinationId, "Cameras");
        Assert.Empty(destinationCameras);
        try
        {
            await ArchiverManagementTool.Execute(sc.Manager, new ArchiverManagementRequest(ArchiverAction.MoveVideoUnit,
                VideoUnitId: unitId, TargetArchiverId: destinationId));
            await AssertOwnerEventuallyAsync(unitId, destinationId);
        }
        finally
        {
            await ArchiverManagementTool.Execute(sc.Manager, new ArchiverManagementRequest(ArchiverAction.MoveVideoUnit,
                VideoUnitId: unitId, TargetArchiverId: originalArchiverId));
            await AssertOwnerEventuallyAsync(unitId, originalArchiverId);
            Assert.Equal(destinationCameras, await ReadRelatedIdsAsync(destinationId, "Cameras"));
        }
    }

    private async Task<Guid> GetArchiverIdAsync(Guid? excludedArchiverId = null)
    {
        using var document = IntegrationTestHelpers.ParseJson(await sc.Manager.GetClient().RunPagedReport(
            "EntityConfiguration", "EntityTypeFilter.Add(Role,BQ==),ReturnFields@Name@Type", pageSize: 10, page: 1));
        var archiver = IntegrationTestHelpers.GetRows(document.RootElement).EnumerateArray().FirstOrDefault(item =>
            item.GetProperty("Type").GetString() == "Archiver" &&
            (excludedArchiverId is null ||
             item.GetProperty("Guid").GetGuid() != excludedArchiverId &&
             item.GetProperty("Name").GetString()!.StartsWith("MCP_Archiver_", StringComparison.Ordinal)));
        Assert.True(archiver.ValueKind == System.Text.Json.JsonValueKind.Object,
            "Archiver integration requires an Archiver; move requires a spare MCP_Archiver_ test role in the first 10 results.");
        return archiver.GetProperty("Guid").GetGuid();
    }

    private async Task<List<Guid>> ReadRelatedIdsAsync(Guid archiverId, string field)
    {
        using var document = IntegrationTestHelpers.ParseJson(await sc.Manager.GetClient().GetEntityFields(
            archiverId.ToString(), "Guid", "EntityType", "Type", field));
        var archiver = IntegrationTestHelpers.GetResult(document.RootElement);
        Assert.Equal(archiverId, archiver.GetProperty("Guid").GetGuid());
        Assert.Equal("Role", archiver.GetProperty("EntityType").GetString());
        Assert.Equal("Archiver", archiver.GetProperty("Type").GetString());
        return archiver.GetProperty(field).EnumerateArray().Select(value => value.GetGuid()).ToList();
    }

    private async Task AssertOwnerEventuallyAsync(Guid unitId, Guid expectedArchiverId)
    {
        var elapsed = Stopwatch.StartNew();
        Guid actualOwner;
        do
        {
            using var document = IntegrationTestHelpers.ParseJson(await sc.Manager.GetClient().GetEntityFields(
                unitId.ToString(), "Guid", "EntityType", "OwnerRole"));
            var unit = IntegrationTestHelpers.GetResult(document.RootElement);
            Assert.Equal(unitId, unit.GetProperty("Guid").GetGuid());
            Assert.Equal("VideoUnit", unit.GetProperty("EntityType").GetString());
            actualOwner = unit.GetProperty("OwnerRole").GetGuid();
            if (actualOwner == expectedArchiverId) return;
            await Task.Delay(250);
        } while (elapsed.Elapsed < TimeSpan.FromSeconds(10));
        Assert.Equal(expectedArchiverId, actualOwner);
    }
}
