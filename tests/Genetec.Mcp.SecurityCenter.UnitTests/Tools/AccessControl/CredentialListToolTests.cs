using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class CredentialListToolTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(101)]
    public void Request_InvalidPageSize_IsRejected(int pageSize)
    {
        var request = new CredentialListRequest(PageSize: pageSize);
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
        Assert.Contains(errors, error => error.MemberNames.Contains("PageSize"));
    }

    [Fact]
    public void Request_HasExpectedDefaults()
    {
        var request = new CredentialListRequest();
        Assert.Equal(50, request.PageSize);
        Assert.Equal(1, request.Page);
    }
}
