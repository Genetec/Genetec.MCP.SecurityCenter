using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.VideoSurveillance;

public class VideoUnitUpdateToolTests
{
    private static readonly Guid UnitId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void Update_EmptyItems_FailsValidation()
    {
        var request = new VideoUnitUpdateRequest([]);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        Assert.False(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            request, new System.ComponentModel.DataAnnotations.ValidationContext(request), results, true));
    }

    [Fact]
    public async Task Update_Single_UsesTypedItemAndOmitsSelectorFromWebSdkFields()
    {
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(UnitId, "VideoUnit");
        var result = await VideoUnitUpdateTool.Execute(TestHelpers.FakeManager(handler),
            new VideoUnitUpdateRequest([new VideoUnitUpdateItem(UnitId, Name: "Unit Name", Password: "secret")]));

        Assert.IsType<string>(result);
        Assert.Single(handler.Requests, request => request.Method == HttpMethod.Get);
        var query = Uri.UnescapeDataString(Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post).Uri.ToString());
        Assert.Contains($"entity={UnitId},Name=Unit Name,Password=secret", query, StringComparison.Ordinal);
        Assert.DoesNotContain("Id=", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_Batch_SendsEveryTypedItem()
    {
        var handler = new EntityMetadataTestHandler();
        var secondId = Guid.Parse("22222222-3333-4444-5555-666666666666");
        handler.AddEntity(UnitId, "VideoUnit");
        handler.AddEntity(secondId, "VideoUnit");

        var result = await VideoUnitUpdateTool.Execute(TestHelpers.FakeManager(handler),
            new VideoUnitUpdateRequest([
                new VideoUnitUpdateItem(UnitId, Name: "Unit One"),
                new VideoUnitUpdateItem(secondId, Password: "secret")
            ]));

        Assert.IsType<string>(result);
        Assert.Equal(2, handler.Requests.Count(request => request.Method == HttpMethod.Get));
        var updateRequests = handler.Requests.Where(request => request.Method == HttpMethod.Post).ToArray();
        Assert.Equal(2, updateRequests.Length);
        Assert.All(updateRequests, request =>
            Assert.DoesNotContain("Id=", Uri.UnescapeDataString(request.Uri.ToString()), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Update_SingleWrongEntityType_RejectsBeforeUpdate()
    {
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(UnitId, "UserGroup");

        var error = await Assert.ThrowsAsync<ArgumentException>(() => VideoUnitUpdateTool.Execute(
            TestHelpers.FakeManager(handler),
            new VideoUnitUpdateRequest([new VideoUnitUpdateItem(UnitId, Name: "Should not update")])));

        Assert.Equal("items", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task Update_MixedBatch_OnlyUpdatesVideoUnit()
    {
        var validId = UnitId;
        var wrongId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(validId, "VideoUnit");
        handler.AddEntity(wrongId, "UserGroup");

        var result = await VideoUnitUpdateTool.Execute(TestHelpers.FakeManager(handler),
            new VideoUnitUpdateRequest([
                new VideoUnitUpdateItem(validId, Name: "Allowed"),
                new VideoUnitUpdateItem(wrongId, Name: "Rejected")
            ]));
        using var document = System.Text.Json.JsonDocument.Parse(Assert.IsType<string>(result));
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("success_count").GetInt32());
        Assert.Equal(1, root.GetProperty("failure_count").GetInt32());
        var updates = handler.Requests.Where(request => request.Method == HttpMethod.Post).ToArray();
        var update = Assert.Single(updates);
        Assert.Contains(validId.ToString(), Uri.UnescapeDataString(update.Uri.ToString()), StringComparison.Ordinal);
        Assert.DoesNotContain(wrongId.ToString(), Uri.UnescapeDataString(update.Uri.ToString()), StringComparison.Ordinal);
    }
}
