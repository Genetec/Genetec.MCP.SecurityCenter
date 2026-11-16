using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class CardholderUpdateToolTests
{
    [Fact]
    public void Update_EmptyItems_FailsValidation()
    {
        var request = new CardholderUpdateRequest([]);
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, true));
    }

    [Fact]
    public async Task Update_Single_UsesWebSdkPropertyNamesAndOmitsSelector()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var handler = new EntityMetadataTestHandler();

        handler.AddEntity(id, "Cardholder");
        var result = await CardholderUpdateTool.Execute(TestHelpers.FakeManager(handler),
            new CardholderUpdateRequest([new CardholderUpdateItem(
                id, "Cardholder Name", "First", "Last", "person@example.com")]));

        Assert.IsType<string>(result);
        var query = Uri.UnescapeDataString(Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post).Uri.ToString());
        Assert.Contains($@"entity={id},Name=Cardholder Name,FirstName=First,LastName=Last,EmailAddress=person\@example.com", query, StringComparison.Ordinal);
        Assert.DoesNotContain("Id=", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_Batch_SendsEveryTypedItem()
    {
        var firstId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var secondId = Guid.Parse("22222222-3333-4444-5555-666666666666");
        var handler = new EntityMetadataTestHandler();

        handler.AddEntity(firstId, "Cardholder");
        handler.AddEntity(secondId, "Cardholder");
        var result = await CardholderUpdateTool.Execute(TestHelpers.FakeManager(handler),
            new CardholderUpdateRequest([
                new CardholderUpdateItem(firstId, Name: "One"),
                new CardholderUpdateItem(secondId, Name: "Two")
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
        var request = JsonSerializer.Deserialize<CardholderUpdateRequest>(json, NumericStringConverter.ToolOptions)!;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CardholderUpdateTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal("items", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Fact]
    public async Task Update_MixedEntityTypes_PreservesValidBatchItem()
    {
        var validId = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(validId, "Cardholder");
        handler.AddEntity(wrongId, "UserGroup");
        var result = await CardholderUpdateTool.Execute(TestHelpers.FakeManager(handler),
            new CardholderUpdateRequest([new CardholderUpdateItem(validId, Name: "Valid"), new CardholderUpdateItem(wrongId, Name: "Must not update")]));
        using var document = JsonDocument.Parse(Assert.IsType<string>(result));
        Assert.Equal(1, document.RootElement.GetProperty("success_count").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("failure_count").GetInt32());
        Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
    }
}
