using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class ActivityTrailToolTests
{
    [Theory]
    [InlineData(null, "2026-01-01T00:00:00Z")]
    [InlineData("2026-01-01T00:00:00Z", null)]
    [InlineData("not-a-date", "2026-01-01T00:00:00Z")]
    public void Request_InvalidTimeRange_FailsValidation(string? start, string? end)
    {
        var request = new ActivityTrailRequest(start!, end!);
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
    }
}
