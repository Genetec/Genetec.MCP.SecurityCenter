using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class CardholderDeleteToolTests
{
    [Fact]
    public async Task Delete_SingleWrongEntityType_RejectsBeforeDeleting()
    {
        await TestHelpers.AssertWrongEntityTypeAsync(
            (manager, id) => CardholderDeleteTool.Execute(manager, new CardholderDeleteRequest([id])),
            parameterName: "items");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_MixedBatch_OnlyDeletesExpectedEntity(bool invalidFirst)
    {
        await TestHelpers.AssertMixedDeleteTypesAsync("Cardholder",
            (manager, ids) => CardholderDeleteTool.Execute(manager, new CardholderDeleteRequest(ids)),
            invalidFirst);
    }

    [Fact]
    public void Request_EmptyItems_FailsValidation()
    {
        var request = new CardholderDeleteRequest([]);
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }
}
