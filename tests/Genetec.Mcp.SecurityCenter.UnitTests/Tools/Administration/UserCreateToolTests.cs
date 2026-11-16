using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class UserCreateToolTests
{
    // ── Validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void UserItem_InvalidName_FailsValidation(string? name)
    {
        var item = new UserItem(name!);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(item, new ValidationContext(item), results, validateAllProperties: true));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UserItem.Name)));
    }
}
