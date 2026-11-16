using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Utilities;

public class CustomFieldManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(CustomFieldAction.GetValue)]
    [InlineData(CustomFieldAction.SetValue)]
    public async Task Batch_MixedOutcomes_PreservesSuccessfulValueAndOriginalIndices(CustomFieldAction action)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var fieldName = $"MCP_{Guid.NewGuid():N}"[..20];
        await CustomFieldManagementTool.Execute(sc.Manager, new CustomFieldRequest(CustomFieldAction.CreateField,
            EntityType: CustomFieldEntityType.Cardholder, FieldName: fieldName, DataType: CustomFieldDataType.Text));
        try
        {
            var cardholder = Assert.IsType<EntityCreateResult>(await CardholderCreateTool.Execute(sc.Manager,
                new CardholderCreateRequest([new CardholderItem($"MCP_CH_{Guid.NewGuid():N}")])));
            sc.TrackForCleanup(cardholder.Guid);
            await CustomFieldManagementTool.Execute(sc.Manager, new CustomFieldRequest(CustomFieldAction.SetValue,
                EntityId: cardholder.Guid, FieldName: fieldName, Value: "Before"));

            if (action == CustomFieldAction.SetValue)
            {
                await Assert.ThrowsAsync<ArgumentException>(() => CustomFieldManagementTool.Execute(sc.Manager,
                    new CustomFieldRequest(action, Items: [
                        new CustomFieldBatchItem(cardholder.Guid, fieldName, "Unexpected"),
                        new CustomFieldBatchItem(cardholder.Guid, fieldName)])));
                using var unchanged = JsonDocument.Parse(Assert.IsType<string>(await CustomFieldManagementTool.Execute(sc.Manager,
                    new CustomFieldRequest(CustomFieldAction.GetValue, EntityId: cardholder.Guid, FieldName: fieldName))));
                Assert.Equal("Before", unchanged.RootElement.GetProperty("Value").GetString());
            }

            var result = Assert.IsType<CustomFieldBatchResult>(await CustomFieldManagementTool.Execute(sc.Manager,
                new CustomFieldRequest(action, Items: [
                    new CustomFieldBatchItem(cardholder.Guid, fieldName, "After"),
                    new CustomFieldBatchItem(Guid.NewGuid(), fieldName, "After")])));
            Assert.Equal(2, result.RequestedCount);
            Assert.Equal(1, result.SuccessCount);
            Assert.Equal(1, result.FailureCount);
            Assert.Equal(0, result.Items[0].Index);
            Assert.Equal(cardholder.Guid.ToString(), result.Items[0].Key.EntityId);
            Assert.True(result.Items[0].Success);
            Assert.Equal(1, result.Items[1].Index);
            Assert.False(result.Items[1].Success);
            Assert.False(string.IsNullOrWhiteSpace(result.Items[1].Error));
            using var readback = JsonDocument.Parse(Assert.IsType<string>(await CustomFieldManagementTool.Execute(sc.Manager,
                new CustomFieldRequest(CustomFieldAction.GetValue, EntityId: cardholder.Guid, FieldName: fieldName))));
            Assert.Equal(action == CustomFieldAction.SetValue ? "After" : "Before", readback.RootElement.GetProperty("Value").GetString());
        }
        finally
        {
            await CustomFieldManagementTool.Execute(sc.Manager, new CustomFieldRequest(CustomFieldAction.DeleteField,
                EntityType: CustomFieldEntityType.Cardholder, FieldName: fieldName));
        }
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateGetSetValue_AndDeleteField_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var fieldName = $"MCP ({Guid.NewGuid():N})"[..20];

        var createResult = await CustomFieldManagementTool.Execute(sc.Manager,
            new CustomFieldRequest(CustomFieldAction.CreateField,
                EntityType: CustomFieldEntityType.Cardholder,
                FieldName: fieldName,
                DataType: CustomFieldDataType.Text));
        Assert.NotNull(createResult);

        var cardholderResult = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem($"MCP_CH_{Guid.NewGuid():N}")]));
        var cardholder = Assert.IsType<EntityCreateResult>(cardholderResult);
        sc.TrackForCleanup(cardholder.Guid);

        var setValue = await CustomFieldManagementTool.Execute(sc.Manager,
            new CustomFieldRequest(CustomFieldAction.SetValue,
                EntityId: cardholder.Guid, FieldName: fieldName, Value: "TestValue"));
        Assert.NotNull(setValue);

        var getValue = await CustomFieldManagementTool.Execute(sc.Manager,
            new CustomFieldRequest(CustomFieldAction.GetValue,
                EntityId: cardholder.Guid, FieldName: fieldName));
        using var readback = JsonDocument.Parse(Assert.IsType<string>(getValue));
        Assert.Equal("TestValue", readback.RootElement.GetProperty("Value").GetString());

        await CustomFieldManagementTool.Execute(sc.Manager,
            new CustomFieldRequest(CustomFieldAction.DeleteField,
                EntityType: CustomFieldEntityType.Cardholder, FieldName: fieldName));
    }

    // ── Entity type support discovery ───────────────────────────────
    // Run this theory against a live SC to discover which entity types
    // actually support custom fields. Green = supported, Red = not supported.
    // Once the full list is known, prune CustomFieldEntityType accordingly.

    public static IEnumerable<object[]> AllEntityTypes =>
        Enum.GetValues<CustomFieldEntityType>().Select(e => new object[] { e });

    [Theory]
    [MemberData(nameof(AllEntityTypes))]
    public async Task CreateAndDeleteCustomField_EntityTypeIsSupported(CustomFieldEntityType entityType)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var fieldName = $"McpTest_{Guid.NewGuid():N}"[..20];

        await CustomFieldManagementTool.Execute(sc.Manager,
            new CustomFieldRequest(CustomFieldAction.CreateField,
                EntityType: entityType,
                FieldName: fieldName,
                DataType: CustomFieldDataType.Text));

        await CustomFieldManagementTool.Execute(sc.Manager,
            new CustomFieldRequest(CustomFieldAction.DeleteField,
                EntityType: entityType, FieldName: fieldName));
    }
}
