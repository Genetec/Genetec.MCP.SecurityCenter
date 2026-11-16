using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class AlarmCreateToolTests
{
    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Create_EmptyName_FailsValidation()
    {
        var item = new AlarmItem(Name: "");
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(item, new ValidationContext(item), results, validateAllProperties: true));
    }

    [Fact]
    public void Create_EmptyItemsList_FailsValidation()
    {
        var request = new AlarmCreateRequest(Items: []);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
    }
}
