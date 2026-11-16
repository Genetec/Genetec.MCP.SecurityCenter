using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class AlarmDeleteToolTests
{
    [Fact]
    public async Task Delete_SingleWrongEntityType_RejectsBeforeDeleting()
    {
        await TestHelpers.AssertWrongEntityTypeAsync(
            (manager, id) => AlarmDeleteTool.Execute(manager, new AlarmDeleteRequest([id])),
            parameterName: "items");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_MixedBatch_OnlyDeletesExpectedEntity(bool invalidFirst)
    {
        await TestHelpers.AssertMixedDeleteTypesAsync("Alarm",
            (manager, ids) => AlarmDeleteTool.Execute(manager, new AlarmDeleteRequest(ids)),
            invalidFirst);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Delete_EmptyItemsList_FailsValidation()
    {
        var request = new AlarmDeleteRequest(Items: []);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
    }
}
