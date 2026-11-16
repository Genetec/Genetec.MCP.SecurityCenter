using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class UserGroupDeleteToolTests
{
    [Fact]
    public async Task Delete_SingleWrongEntityType_RejectsBeforeDeleting()
    {
        await TestHelpers.AssertWrongEntityTypeAsync(
            (manager, id) => UserGroupDeleteTool.Execute(manager, new UserGroupDeleteRequest([id])),
            parameterName: "items", wrongType: "Partition");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_MixedBatch_OnlyDeletesExpectedEntity(bool invalidFirst)
    {
        await TestHelpers.AssertMixedDeleteTypesAsync("UserGroup",
            (manager, ids) => UserGroupDeleteTool.Execute(manager, new UserGroupDeleteRequest(ids)),
            invalidFirst, wrongType: "Partition");
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Delete_EmptyItems_FailsValidation()
    {
        var request = new UserGroupDeleteRequest([]);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
    }
}
