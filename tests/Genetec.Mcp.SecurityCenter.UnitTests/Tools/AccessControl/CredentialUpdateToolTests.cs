using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class CredentialUpdateToolTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Item_InvalidName_FailsValidation(string? name)
    {
        var item = new CredentialUpdateItem(Guid.NewGuid(), name!);
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(item, new ValidationContext(item), errors, true));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CredentialUpdateItem.Name)));
    }

    [Fact]
    public void Request_EmptyItems_FailsValidation()
    {
        var request = new CredentialUpdateRequest([]);
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    [Fact]
    public async Task Update_WrongEntityType_RejectsSingleBeforeMutation()
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var json = JsonSerializer.Serialize(new { items = new[] { new { id, name = "Must not update" } } });
        var request = JsonSerializer.Deserialize<CredentialUpdateRequest>(json, NumericStringConverter.ToolOptions)!;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CredentialUpdateTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal("items", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Fact]
    public async Task Update_MixedEntityTypes_PreservesValidBatchItem()
    {
        var validId = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(validId, "Credential");
        handler.AddEntity(wrongId, "UserGroup");
        var result = await CredentialUpdateTool.Execute(TestHelpers.FakeManager(handler),
            new CredentialUpdateRequest([new CredentialUpdateItem(validId, Name: "Valid"), new CredentialUpdateItem(wrongId, Name: "Must not update")]));
        using var document = JsonDocument.Parse(Assert.IsType<string>(result));
        Assert.Equal(1, document.RootElement.GetProperty("success_count").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("failure_count").GetInt32());
        Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
    }
}
