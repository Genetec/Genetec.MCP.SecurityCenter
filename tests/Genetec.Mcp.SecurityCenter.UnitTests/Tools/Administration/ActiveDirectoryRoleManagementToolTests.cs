using Genetec.Mcp.SecurityCenter.Tools.Administration;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using System.Text.Json;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class ActiveDirectoryRoleManagementToolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sync_ProductionBindingRetainsImageOption(bool synchronizeImages)
    {
        var id = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new { action = "sync", id, synchronize_images = synchronizeImages });
        var request = JsonSerializer.Deserialize<ActiveDirectoryRoleManagementRequest>(json, NumericStringConverter.ToolOptions)!;
        Assert.Equal(ActiveDirectoryRoleAction.Sync, request.Action);
        Assert.Equal(id, request.Id);
        Assert.Equal(synchronizeImages, request.SynchronizeImages);
    }

    [Fact]
    public void Sync_ProductionBindingDefaultsToSkippingImages()
    {
        var request = JsonSerializer.Deserialize<ActiveDirectoryRoleManagementRequest>(
            "{\"action\":\"sync\"}", NumericStringConverter.ToolOptions)!;
        Assert.False(request.SynchronizeImages);
    }

    [Theory]
    [InlineData("\"true\"")]
    [InlineData("1")]
    [InlineData("null")]
    public void Sync_ProductionBindingRejectsNonBooleanImageOption(string value)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ActiveDirectoryRoleManagementRequest>(
            $"{{\"action\":\"sync\",\"synchronize_images\":{value}}}", NumericStringConverter.ToolOptions));

    [Fact]
    public void Discovery_ExposesTypedImageSynchronizationOption()
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            SearchToolsTool.Execute(new SearchToolsRequest(Query: "active_directory_role_management", Detail: ToolDetailLevel.Full)),
            NumericStringConverter.ToolOptions));
        var tool = Assert.Single(document.RootElement.EnumerateArray(), item => item.GetProperty("name").GetString() == "active_directory_role_management");
        var request = Assert.Single(tool.GetProperty("parameters").EnumerateArray());
        var field = Assert.Single(request.GetProperty("fields").EnumerateArray(), item => item.GetProperty("name").GetString() == "synchronize_images");
        Assert.Equal("Boolean", field.GetProperty("type").GetString());
        Assert.Contains("default false", field.GetProperty("description").GetString());
    }

    public static IEnumerable<object[]> CurrentEntityActions() => Enum.GetValues<ActiveDirectoryRoleAction>().Where(action => action is ActiveDirectoryRoleAction.Get or ActiveDirectoryRoleAction.Sync).Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(CurrentEntityActions))]
    public async Task CurrentEntityTargets_RejectWrongEntityType(ActiveDirectoryRoleAction action)
    {
        await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => ActiveDirectoryRoleManagementTool.Execute(manager, new ActiveDirectoryRoleManagementRequest(action, Id: id)), "id");
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_MissingId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ActiveDirectoryRoleManagementTool.Execute(TestHelpers.FakeManager(),
                new ActiveDirectoryRoleManagementRequest(ActiveDirectoryRoleAction.Get)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_InvalidItemName_ThrowsArgumentException(string? name)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ActiveDirectoryRoleManagementTool.Execute(TestHelpers.FakeManager(),
                new ActiveDirectoryRoleManagementRequest(ActiveDirectoryRoleAction.Create, Items: [name!])));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_MissingItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ActiveDirectoryRoleManagementTool.Execute(TestHelpers.FakeManager(),
                new ActiveDirectoryRoleManagementRequest(ActiveDirectoryRoleAction.Create)));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sync_MissingId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ActiveDirectoryRoleManagementTool.Execute(TestHelpers.FakeManager(),
                new ActiveDirectoryRoleManagementRequest(ActiveDirectoryRoleAction.Sync)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
