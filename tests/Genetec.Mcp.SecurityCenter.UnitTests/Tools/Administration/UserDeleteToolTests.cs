using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class UserDeleteToolTests
{
    [Fact]
    public async Task Delete_SingleWrongEntityType_RejectsBeforeDeleting()
    {
        await TestHelpers.AssertWrongEntityTypeAsync(
            (manager, id) => UserDeleteTool.Execute(manager, new UserDeleteRequest([id])),
            parameterName: "items");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_MixedBatch_OnlyDeletesExpectedEntity(bool invalidFirst)
    {
        await TestHelpers.AssertMixedDeleteTypesAsync("User",
            (manager, ids) => UserDeleteTool.Execute(manager, new UserDeleteRequest(ids)),
            invalidFirst);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Delete_EmptyItemsList_FailsValidation()
    {
        var request = new UserDeleteRequest(Items: []);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
    }
}
