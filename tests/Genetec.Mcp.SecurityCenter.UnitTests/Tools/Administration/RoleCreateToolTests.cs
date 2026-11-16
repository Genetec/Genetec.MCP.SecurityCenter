using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class RoleCreateToolTests
{
    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Create_EmptyName_FailsValidation()
    {
        var item = new RoleItem(Name: "");
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(item, new ValidationContext(item), results, validateAllProperties: true));
    }

    [Fact]
    public void Create_EmptyItemsList_FailsValidation()
    {
        var request = new RoleCreateRequest(Items: []);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
    }
}
