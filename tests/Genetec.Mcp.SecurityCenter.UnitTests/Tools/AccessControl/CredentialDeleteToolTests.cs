using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class CredentialDeleteToolTests
{
    [Fact]
    public void Request_EmptyItems_FailsValidation()
    {
        var request = new CredentialDeleteRequest([]);
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    [Fact]
    public async Task Delete_SingleWrongEntityType_RejectsBeforeDeleting()
    {
        var id = Guid.NewGuid();
        var handler = new Tools.EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");

        var error = await Assert.ThrowsAsync<ArgumentException>(() => CredentialDeleteTool.Execute(
            TestHelpers.FakeManager(handler), new CredentialDeleteRequest([id])));

        Assert.Equal("items", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task Delete_MixedBatch_OnlyDeletesCredential()
    {
        var credentialId = Guid.NewGuid();
        var userGroupId = Guid.NewGuid();
        var handler = new Tools.EntityMetadataTestHandler();
        handler.AddEntity(credentialId, "Credential");
        handler.AddEntity(userGroupId, "UserGroup");

        var result = await CredentialDeleteTool.Execute(TestHelpers.FakeManager(handler),
            new CredentialDeleteRequest([credentialId, userGroupId]));
        using var document = System.Text.Json.JsonDocument.Parse(Assert.IsType<string>(result));
        var root = document.RootElement;

        Assert.Equal(2, root.GetProperty("requested_count").GetInt32());
        Assert.Equal(1, root.GetProperty("success_count").GetInt32());
        Assert.Equal(1, root.GetProperty("failure_count").GetInt32());
        Assert.Equal(credentialId.ToString(), handler.Requests.Single(request => request.Method == HttpMethod.Delete).Uri.Segments[^1]);
    }
}
