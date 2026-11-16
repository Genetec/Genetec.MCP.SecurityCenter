using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.VideoSurveillance;

public class CameraUpdateToolTests
{
    private static readonly Guid CameraId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void Update_EmptyItems_FailsValidation()
    {
        var request = new CameraUpdateRequest([]);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        Assert.False(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            request, new System.ComponentModel.DataAnnotations.ValidationContext(request), results, true));
    }

    [Fact]
    public async Task Update_Single_UsesTypedItemAndOmitsSelectorFromWebSdkFields()
    {
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(CameraId, "Camera");
        var result = await CameraUpdateTool.Execute(TestHelpers.FakeManager(handler),
            new CameraUpdateRequest([new CameraUpdateItem(CameraId, "Camera Name")]));

        Assert.IsType<string>(result);
        var query = Uri.UnescapeDataString(Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post).Uri.ToString());
        Assert.Contains($"entity={CameraId},Name=Camera Name", query, StringComparison.Ordinal);
        Assert.DoesNotContain("Id=", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_Batch_SendsEveryTypedItem()
    {
        var handler = new EntityMetadataTestHandler();
        var secondId = Guid.Parse("22222222-3333-4444-5555-666666666666");

        handler.AddEntity(CameraId, "Camera");
        handler.AddEntity(secondId, "Camera");
        var result = await CameraUpdateTool.Execute(TestHelpers.FakeManager(handler),
            new CameraUpdateRequest([
                new CameraUpdateItem(CameraId, "Camera One"),
                new CameraUpdateItem(secondId, "Camera Two")
            ]));

        Assert.IsType<string>(result);
        Assert.Equal(2, handler.Requests.Count(request => request.Method == HttpMethod.Get));
        var updates = handler.Requests.Where(request => request.Method == HttpMethod.Post).ToArray();
        Assert.Equal(2, updates.Length);
        Assert.All(updates, request =>
            Assert.DoesNotContain("Id=", Uri.UnescapeDataString(request.Uri.ToString()), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Update_WrongEntityType_RejectsSingleBeforeMutation()
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var json = JsonSerializer.Serialize(new { items = new[] { new { id, name = "Must not update" } } });
        var request = JsonSerializer.Deserialize<CameraUpdateRequest>(json, NumericStringConverter.ToolOptions)!;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CameraUpdateTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal("items", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Fact]
    public async Task Update_MixedEntityTypes_PreservesValidBatchItem()
    {
        var validId = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(validId, "Camera");
        handler.AddEntity(wrongId, "UserGroup");
        var result = await CameraUpdateTool.Execute(TestHelpers.FakeManager(handler),
            new CameraUpdateRequest([new CameraUpdateItem(validId, Name: "Valid"), new CameraUpdateItem(wrongId, Name: "Must not update")]));
        using var document = JsonDocument.Parse(Assert.IsType<string>(result));
        Assert.Equal(1, document.RootElement.GetProperty("success_count").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("failure_count").GetInt32());
        Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
    }
}
