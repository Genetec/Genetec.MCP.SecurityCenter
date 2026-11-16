using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class VisitorCredentialsToolTests
{
    [Theory]
    [InlineData(VisitorCredentialsAction.AddCredential, "credential_id")]
    [InlineData(VisitorCredentialsAction.RemoveCredential, "credential_id")]
    public async Task WrongLinkedType_RejectsBeforeMutation(VisitorCredentialsAction action, string parameter)
    {
        var id = Guid.NewGuid();
        var wrong = Guid.NewGuid();
        var escort = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "Visitor");
        handler.AddEntity(wrong, "UserGroup");
        handler.AddEntity(escort, "Cardholder");
        var request = (action, parameter) switch
        {
            (VisitorCredentialsAction.AddCredential, "credential_id") => new VisitorCredentialsRequest(action, id, CredentialId: wrong),
            (VisitorCredentialsAction.RemoveCredential, "credential_id") => new VisitorCredentialsRequest(action, id, CredentialId: wrong),
            _ => throw new InvalidOperationException()
        };
        var error = await Assert.ThrowsAsync<ArgumentException>(() => VisitorCredentialsTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal(parameter, error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<VisitorCredentialsAction>()
        .Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsBeforeMutation(VisitorCredentialsAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => VisitorCredentialsTool.Execute(TestHelpers.FakeManager(handler),
            new VisitorCredentialsRequest(action, Id: id, CredentialId: id)));
        Assert.Equal("id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }


    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task AddCredential_NullCredentialId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            VisitorCredentialsTool.Execute(TestHelpers.FakeManager(),
                new VisitorCredentialsRequest(VisitorCredentialsAction.AddCredential, Id: Guid.NewGuid(), CredentialId: null)));

        Assert.Contains("credential_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

}
