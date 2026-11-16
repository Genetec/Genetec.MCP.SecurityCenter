using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class UserUpdateToolTests
{
    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Update_EmptyItemsList_FailsValidation()
    {
        var request = new UserUpdateRequest(Items: []);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
    }

    [Fact]
    public async Task Update_Single_UsesTypedItemAndOmitsSelectorFromWebSdkFields()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var handler = new EntityMetadataTestHandler();

        handler.AddEntity(id, "User");
        var result = await UserUpdateTool.Execute(TestHelpers.FakeManager(handler),
            new UserUpdateRequest([new UserUpdateItem(id, "user.name", "First", "Last")]));

        Assert.IsType<string>(result);
        var query = Uri.UnescapeDataString(Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post).Uri.ToString());
        Assert.Contains($"entity={id},Name=user.name,FirstName=First,LastName=Last", query, StringComparison.Ordinal);
        Assert.DoesNotContain("Id=", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_Batch_SendsEveryTypedItem()
    {
        var firstId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var secondId = Guid.Parse("22222222-3333-4444-5555-666666666666");
        var handler = new EntityMetadataTestHandler();

        handler.AddEntity(firstId, "User");
        handler.AddEntity(secondId, "User");
        var result = await UserUpdateTool.Execute(TestHelpers.FakeManager(handler),
            new UserUpdateRequest([
                new UserUpdateItem(firstId, FirstName: "First"),
                new UserUpdateItem(secondId, LastName: "Last")
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
        var request = JsonSerializer.Deserialize<UserUpdateRequest>(json, NumericStringConverter.ToolOptions)!;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => UserUpdateTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal("items", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Fact]
    public async Task Update_MixedEntityTypes_PreservesValidBatchItem()
    {
        var validId = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(validId, "User");
        handler.AddEntity(wrongId, "UserGroup");
        var result = await UserUpdateTool.Execute(TestHelpers.FakeManager(handler),
            new UserUpdateRequest([new UserUpdateItem(validId, Name: "Valid"), new UserUpdateItem(wrongId, Name: "Must not update")]));
        using var document = JsonDocument.Parse(Assert.IsType<string>(result));
        Assert.Equal(1, document.RootElement.GetProperty("success_count").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("failure_count").GetInt32());
        Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
    }
}
