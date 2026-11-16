using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class UserGroupCreateToolTests
{
    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Create_EmptyItems_FailsValidation()
    {
        var request = new UserGroupCreateRequest([]);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_FailsValidation(string? name)
    {
        var item = new UserGroupItem(name!);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(item, new ValidationContext(item), results, validateAllProperties: true));
    }
}
