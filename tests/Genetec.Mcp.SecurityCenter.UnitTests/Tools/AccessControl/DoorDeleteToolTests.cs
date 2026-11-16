using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class DoorDeleteToolTests
{
    [Fact]
    public async Task Delete_SingleWrongEntityType_RejectsBeforeDeleting()
    {
        await TestHelpers.AssertWrongEntityTypeAsync(
            (manager, id) => DoorDeleteTool.Execute(manager, new DoorDeleteRequest([id])),
            parameterName: "items");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_MixedBatch_OnlyDeletesExpectedEntity(bool invalidFirst)
    {
        await TestHelpers.AssertMixedDeleteTypesAsync("Door",
            (manager, ids) => DoorDeleteTool.Execute(manager, new DoorDeleteRequest(ids)),
            invalidFirst);
    }

    [Fact]
    public void Request_EmptyItems_FailsValidation()
    {
        var request = new DoorDeleteRequest([]);
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }
}
