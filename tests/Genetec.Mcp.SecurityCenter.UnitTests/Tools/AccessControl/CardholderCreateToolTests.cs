using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class CardholderCreateToolTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Item_InvalidName_FailsValidation(string? name)
    {
        var item = new CardholderItem(name!);
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(item, new ValidationContext(item), errors, true));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CardholderItem.Name)));
    }

    [Fact]
    public void Request_EmptyItems_FailsValidation()
    {
        var request = new CardholderCreateRequest([]);
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }
}
