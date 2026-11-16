using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using Genetec.Mcp.SecurityCenter.Tools.Administration;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools;

public sealed class EntityDeletionRequestTests
{
    public static IEnumerable<object[]> SharedHelperInvalidIds()
    {
        var collectionRequests = new[]
        {
            typeof(AccessRuleDeleteRequest), typeof(AreaDeleteRequest), typeof(CardholderDeleteRequest),
            typeof(CredentialDeleteRequest), typeof(DoorDeleteRequest), typeof(VisitorDeleteRequest),
            typeof(AlarmDeleteRequest), typeof(UserDeleteRequest), typeof(UserGroupDeleteRequest)
        };
        var managementRequests = new[]
        {
            typeof(CardholderGroupManagementRequest), typeof(PermitManagementRequest), typeof(LprHotlistManagementRequest)
        };
        foreach (var id in new[] { "not-a-guid", "../action", "%2e%2e/action", "11111111-2222-3333-4444-555555555555/../../action" })
        {
            foreach (var type in collectionRequests)
                yield return [type, JsonSerializer.Serialize(new { items = new[] { id } })];
            foreach (var type in managementRequests)
            {
                yield return [type, JsonSerializer.Serialize(new { action = "delete", id })];
                yield return [type, JsonSerializer.Serialize(new { action = "delete", items = new[] { new { id } } })];
            }
        }
    }

    [Theory]
    [MemberData(nameof(SharedHelperInvalidIds))]
    public void Delete_SharedHelperProductionRequestsRejectInvalidOrTraversalId(Type requestType, string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, requestType, NumericStringConverter.ToolOptions));
    }

    public static IEnumerable<object[]> InvalidIds()
    {
        foreach (var tool in new[] { "macro", "partition", "intrusion_unit", "elevator", "door_template" })
        foreach (var id in new[] { "not-a-guid", "../action", "%2e%2e/action", "11111111-2222-3333-4444-555555555555/../../action" })
            yield return [tool, id];
    }

    [Theory]
    [MemberData(nameof(InvalidIds))]
    public async Task Delete_ProductionBindingRejectsInvalidOrTraversalIdBeforeHttp(string tool, string id)
    {
        var handler = new EntityMetadataTestHandler();
        var json = JsonSerializer.Serialize(new { action = "delete", items = new[] { new { id } } });

        await Assert.ThrowsAsync<JsonException>(() => Execute(tool, TestHelpers.FakeManager(handler), json));

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("macro")]
    [InlineData("partition")]
    [InlineData("intrusion_unit")]
    [InlineData("elevator")]
    [InlineData("door_template")]
    public void Delete_ProductionBindingRetainsTypedGuid(string tool)
    {
        var id = Guid.Parse("ABCDEF01-2345-6789-ABCD-EF0123456789");
        var json = JsonSerializer.Serialize(new { action = "delete", items = new[] { new { id = "ABCDEF01-2345-6789-ABCD-EF0123456789" } } });
        var boundId = tool switch
        {
            "macro" => Bind<MacroManagementRequest>(json).Items![0].Id,
            "partition" => Bind<PartitionManagementRequest>(json).Items![0].Id,
            "intrusion_unit" => Bind<IntrusionUnitManagementRequest>(json).Items![0].Id,
            "elevator" => Bind<ElevatorManagementRequest>(json).Items![0].Id,
            "door_template" => Bind<DoorTemplateManagementRequest>(json).Items![0].Id,
            _ => throw new ArgumentException("Unknown tool", nameof(tool))
        };

        Assert.Equal(id, boundId);
    }

    [Theory]
    [InlineData("macro")]
    [InlineData("partition")]
    [InlineData("intrusion_unit")]
    public async Task Delete_MissingIdRejectsBeforeHttp(string tool)
    {
        var handler = new EntityMetadataTestHandler();

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            Execute(tool, TestHelpers.FakeManager(handler), "{\"action\":\"delete\"}"));

        Assert.Equal("items", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    private static Task<object> Execute(string tool, SecurityCenterSessionManager manager, string json)
        => tool switch
        {
            "macro" => MacroManagementTool.Execute(manager, Bind<MacroManagementRequest>(json)),
            "partition" => PartitionManagementTool.Execute(manager, Bind<PartitionManagementRequest>(json)),
            "intrusion_unit" => IntrusionUnitManagementTool.Execute(manager, Bind<IntrusionUnitManagementRequest>(json)),
            "elevator" => ElevatorManagementTool.Execute(manager, Bind<ElevatorManagementRequest>(json)),
            "door_template" => DoorTemplateManagementTool.Execute(manager, Bind<DoorTemplateManagementRequest>(json)),
            _ => throw new ArgumentException("Unknown tool", nameof(tool))
        };

    private static T Bind<T>(string json) => JsonSerializer.Deserialize<T>(json, NumericStringConverter.ToolOptions)!;
}
