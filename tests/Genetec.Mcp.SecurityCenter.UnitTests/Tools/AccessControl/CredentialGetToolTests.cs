using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class CredentialGetToolTests
{
    [Fact]
    public void Request_IdIsRequired()
        => TestHelpers.AssertRequiredProperty<CredentialGetTool.CredentialGetRequest>(nameof(CredentialGetTool.CredentialGetRequest.Id));

    [Fact]
    public async Task Get_WrongEntityType_RejectsCredentialLookup()
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");

        var error = await Assert.ThrowsAsync<ArgumentException>(() => CredentialGetTool.Execute(
            TestHelpers.FakeManager(handler), new CredentialGetTool.CredentialGetRequest(id)));

        Assert.Equal("id", error.ParamName);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Contains($"entity/{id}", handler.Requests[0].Uri.ToString(), StringComparison.Ordinal);
    }
}
