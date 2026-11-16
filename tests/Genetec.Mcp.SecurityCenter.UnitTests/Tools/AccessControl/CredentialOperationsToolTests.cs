using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class CredentialOperationsToolTests
{
    [Theory]
    [InlineData(CredentialOperationsAction.AssignToCardholder, "cardholder_id")]
    [InlineData(CredentialOperationsAction.RemoveFromCardholder, "cardholder_id")]
    public async Task WrongLinkedType_RejectsBeforeMutation(CredentialOperationsAction action, string parameter)
    {
        var id = Guid.NewGuid();
        var wrong = Guid.NewGuid();
        var escort = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "Credential");
        handler.AddEntity(wrong, "UserGroup");
        handler.AddEntity(escort, "Cardholder");
        var request = (action, parameter) switch
        {
            (CredentialOperationsAction.AssignToCardholder, "cardholder_id") => new CredentialOperationsRequest(action, Id: id, CardholderId: wrong),
            (CredentialOperationsAction.RemoveFromCardholder, "cardholder_id") => new CredentialOperationsRequest(action, Id: id, CardholderId: wrong),
            _ => throw new InvalidOperationException()
        };
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CredentialOperationsTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal(parameter, error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<CredentialOperationsAction>()
        .Where(action => action != CredentialOperationsAction.ActivityReport).Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsBeforeMutation(CredentialOperationsAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CredentialOperationsTool.Execute(TestHelpers.FakeManager(handler),
            new CredentialOperationsRequest(action, Id: id, CardholderId: id)));
        Assert.Equal("id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }



    [Fact]
    public async Task ActivityReport_WithoutTimeRange_PreservesEntityFilter()
    {
        var handler = new Tools.ToolTestHandler();
        var id = Guid.NewGuid();

        await CredentialOperationsTool.Execute(TestHelpers.FakeManager(handler),
            new CredentialOperationsRequest(CredentialOperationsAction.ActivityReport, Id: id));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            $"/WebSdk/report/CredentialActivity?q=Credentials@{id}",
            Uri.UnescapeDataString(request.AbsolutePath + request.Query));
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task AssignToCardholder_NullCardholderId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialOperationsTool.Execute(TestHelpers.FakeManager(),
                new CredentialOperationsRequest(
                    CredentialOperationsAction.AssignToCardholder,
                    Id: Guid.NewGuid(),
                    CardholderId: null)));

        Assert.Contains("cardholder_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssignToCardholder_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialOperationsTool.Execute(TestHelpers.FakeManager(),
                new CredentialOperationsRequest(
                    CredentialOperationsAction.AssignToCardholder,
                    Id: null,
                    CardholderId: Guid.NewGuid())));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActivityReport_WithTimeRange_UsesTimeFilter()
    {
        var handler = new Tools.ToolTestHandler();
        var id = Guid.NewGuid();

        await CredentialOperationsTool.Execute(TestHelpers.FakeManager(handler),
            new CredentialOperationsRequest(CredentialOperationsAction.ActivityReport,
                Id: id, TimeStart: "2024-01-01T00:00:00Z", TimeEnd: "2024-01-02T00:00:00Z"));

        var uri = Uri.UnescapeDataString(Assert.Single(handler.Requests).ToString());
        Assert.Contains(
            $"report/CredentialActivity?q=Credentials@{id},TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z)",
            uri,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActivityReport_WithOnlyOneTimeBoundary_ThrowsArgumentException(bool provideStart)
    {
        var request = new CredentialOperationsRequest(CredentialOperationsAction.ActivityReport,
            Id: Guid.NewGuid(),
            TimeStart: provideStart ? "2024-01-01T00:00:00Z" : null,
            TimeEnd: provideStart ? null : "2024-01-02T00:00:00Z");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialOperationsTool.Execute(TestHelpers.FakeManager(), request));

        Assert.Contains("time_start", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("time_end", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
