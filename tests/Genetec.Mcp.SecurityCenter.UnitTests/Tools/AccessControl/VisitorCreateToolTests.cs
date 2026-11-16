using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class VisitorCreateToolTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Item_InvalidName_FailsValidation(string? name)
    {
        var item = new VisitorItem(name!);
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(item, new ValidationContext(item), errors, true));
    }

    [Fact]
    public void Request_EmptyItems_FailsValidation()
    {
        var request = new VisitorCreateRequest([]);
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }
}
