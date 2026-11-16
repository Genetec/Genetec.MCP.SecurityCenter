using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class UserGroupUpdateToolTests
{
    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Update_EmptyItems_FailsValidation()
    {
        var request = new UserGroupUpdateRequest([]);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
    }

    [Fact]
    public async Task Update_WrongEntityType_RejectsSingleBeforeMutation()
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "Camera");
        var json = JsonSerializer.Serialize(new { items = new[] { new { id, name = "Must not update" } } });
        var request = JsonSerializer.Deserialize<UserGroupUpdateRequest>(json, NumericStringConverter.ToolOptions)!;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => UserGroupUpdateTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal("items", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Fact]
    public async Task Update_MixedEntityTypes_PreservesValidBatchItem()
    {
        var validId = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(validId, "UserGroup");
        handler.AddEntity(wrongId, "Camera");
        var result = await UserGroupUpdateTool.Execute(TestHelpers.FakeManager(handler),
            new UserGroupUpdateRequest([new UserGroupUpdateItem(validId, Name: "Valid"), new UserGroupUpdateItem(wrongId, Name: "Must not update")]));
        using var document = JsonDocument.Parse(Assert.IsType<string>(result));
        Assert.Equal(1, document.RootElement.GetProperty("success_count").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("failure_count").GetInt32());
        Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
    }
}
