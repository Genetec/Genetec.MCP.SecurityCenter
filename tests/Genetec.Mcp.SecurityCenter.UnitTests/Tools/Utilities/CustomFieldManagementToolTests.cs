using Genetec.Mcp.SecurityCenter.Tools.Utilities;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using System.Text.Json;
using System.Reflection;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Utilities;

public class CustomFieldManagementToolTests
{
    [Fact]
    public void Metadata_AdvertisesDestructiveCapability()
    {
        var metadata = typeof(CustomFieldManagementTool).GetMethod(nameof(CustomFieldManagementTool.Execute))!
            .GetCustomAttribute<McpServerToolAttribute>()!;

        Assert.True(metadata.Destructive);
    }

    [Fact]
    public async Task SetValue_InvalidLaterItem_RejectsBeforeAnyMutation()
    {
        var handler = new Tools.ToolTestHandler();
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            CustomFieldManagementTool.Execute(TestHelpers.FakeManager(handler),
                new CustomFieldRequest(CustomFieldAction.SetValue, Items: [
                    new CustomFieldBatchItem(Guid.NewGuid(), "Field", "First value"),
                    new CustomFieldBatchItem(Guid.NewGuid(), "Field")])));
        Assert.Equal("value", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(CustomFieldAction.GetValue)]
    [InlineData(CustomFieldAction.SetValue)]
    public async Task ValueBatch_OneRequestFails_PreservesOtherResultsAndOriginalIndices(CustomFieldAction action)
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var handler = new Tools.ToolTestHandler
        {
            ResponseFactory = request => new System.Net.Http.HttpResponseMessage(
                request.RequestUri!.AbsolutePath.Contains(secondId.ToString(), StringComparison.Ordinal)
                    ? System.Net.HttpStatusCode.InternalServerError : System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"Value\":\"First value\"}")
            }
        };
        var result = await CustomFieldManagementTool.Execute(TestHelpers.FakeManager(handler),
            new CustomFieldRequest(action, Items: [
                new CustomFieldBatchItem(firstId, "Field", "First value"),
                new CustomFieldBatchItem(secondId, "Field", "Second value")]));
        var batch = Assert.IsType<CustomFieldBatchResult>(result);
        Assert.Equal(2, batch.RequestedCount);
        Assert.Equal(1, batch.SuccessCount);
        Assert.Equal(1, batch.FailureCount);
        Assert.Equal(new[] { 0, 1 }, batch.Items.Select(item => item.Index));
        Assert.True(batch.Items[0].Success);
        Assert.Equal(firstId.ToString(), batch.Items[0].Key.EntityId);
        Assert.Contains("First value", batch.Items[0].Result, StringComparison.Ordinal);
        Assert.False(batch.Items[1].Success);
        Assert.Equal(secondId.ToString(), batch.Items[1].Key.EntityId);
        Assert.Contains("HTTP 500", batch.Items[1].Error, StringComparison.Ordinal);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task SetValue_BothSingleAndBatchParams_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CustomFieldManagementTool.Execute(TestHelpers.FakeManager(),
                new CustomFieldRequest(CustomFieldAction.SetValue,
                    EntityId: Guid.NewGuid(),
                    Items: [new CustomFieldBatchItem(Guid.NewGuid(), "Field")])));
        Assert.Contains("either", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateField_MissingEntityType_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CustomFieldManagementTool.Execute(TestHelpers.FakeManager(),
                new CustomFieldRequest(CustomFieldAction.CreateField,
                    EntityType: null, FieldName: "MyField", DataType: CustomFieldDataType.Text)));
        Assert.Contains("entity_type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteField_MissingEntityType_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CustomFieldManagementTool.Execute(TestHelpers.FakeManager(),
                new CustomFieldRequest(CustomFieldAction.DeleteField,
                    EntityType: null, FieldName: "MyField")));
        Assert.Contains("entity_type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetValue_MissingEntityId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CustomFieldManagementTool.Execute(TestHelpers.FakeManager(),
                new CustomFieldRequest(CustomFieldAction.SetValue, FieldName: "Field", Value: "Value")));

        Assert.Contains("entity_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(CustomFieldAction.SetValue)]
    [InlineData(CustomFieldAction.GetValue)]
    public async Task SingleValueAction_MissingFieldName_ThrowsArgumentException(CustomFieldAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CustomFieldManagementTool.Execute(TestHelpers.FakeManager(),
                new CustomFieldRequest(action, EntityId: Guid.NewGuid(), Value: "Value")));

        Assert.Contains("field_name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetValue_BatchItemMissingValue_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CustomFieldManagementTool.Execute(TestHelpers.FakeManager(),
                new CustomFieldRequest(CustomFieldAction.SetValue,
                    Items: [new CustomFieldBatchItem(Guid.NewGuid(), "Field", Value: null)])));

        Assert.Contains("value", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateField_MissingDataType_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CustomFieldManagementTool.Execute(TestHelpers.FakeManager(),
                new CustomFieldRequest(CustomFieldAction.CreateField,
                    EntityType: CustomFieldEntityType.Camera, FieldName: "Field")));

        Assert.Contains("data_type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateField_MissingFieldName_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CustomFieldManagementTool.Execute(TestHelpers.FakeManager(),
                new CustomFieldRequest(CustomFieldAction.CreateField,
                    EntityType: CustomFieldEntityType.Camera, DataType: CustomFieldDataType.Text)));

        Assert.Contains("field_name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteField_MissingFieldName_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CustomFieldManagementTool.Execute(TestHelpers.FakeManager(),
                new CustomFieldRequest(CustomFieldAction.DeleteField,
                    EntityType: CustomFieldEntityType.Camera)));

        Assert.Contains("field_name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(CustomFieldAction.GetValue, "..")]
    [InlineData(CustomFieldAction.GetValue, "../Name")]
    [InlineData(CustomFieldAction.SetValue, "..")]
    [InlineData(CustomFieldAction.SetValue, "../Name")]
    public async Task ValueActions_RejectTraversalFieldNames(CustomFieldAction action, string fieldName)
    {
        var handler = new Genetec.Mcp.SecurityCenter.UnitTests.Tools.ToolTestHandler();
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            CustomFieldManagementTool.Execute(TestHelpers.FakeManager(handler),
                new CustomFieldRequest(action, EntityId: Guid.NewGuid(), FieldName: fieldName, Value: "value")));

        Assert.Equal("field_name", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(CustomFieldAction.GetValue, false)]
    [InlineData(CustomFieldAction.SetValue, false)]
    [InlineData(CustomFieldAction.CreateField, false)]
    [InlineData(CustomFieldAction.DeleteField, false)]
    [InlineData(CustomFieldAction.GetValue, true)]
    [InlineData(CustomFieldAction.SetValue, true)]
    public async Task LiteralPercentFieldName_BindsAndEscapesOnce(CustomFieldAction action, bool batch)
    {
        var entityId = Guid.NewGuid();
        var input = batch
            ? new CustomFieldRequest(action, Items: [new CustomFieldBatchItem(entityId, "%252e%252e", "value")])
            : new CustomFieldRequest(action, EntityId: entityId, FieldName: "%252e%252e", Value: "value",
                EntityType: CustomFieldEntityType.Camera, DataType: CustomFieldDataType.Text, DefaultValue: "default");
        var request = JsonSerializer.Deserialize<CustomFieldRequest>(
            JsonSerializer.Serialize(input, NumericStringConverter.ToolOptions), NumericStringConverter.ToolOptions)!;
        var handler = new Tools.ToolTestHandler();

        await CustomFieldManagementTool.Execute(TestHelpers.FakeManager(handler), request);

        Assert.Contains("/%25252e%25252e", Assert.Single(handler.Requests).AbsolutePath, StringComparison.Ordinal);
    }
}
