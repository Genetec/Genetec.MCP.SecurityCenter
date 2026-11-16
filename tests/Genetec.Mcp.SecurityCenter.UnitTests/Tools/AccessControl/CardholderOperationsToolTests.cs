using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class CardholderOperationsToolTests
{
    [Theory]
    [InlineData(CardholderOperationsAction.AddCredential)]
    [InlineData(CardholderOperationsAction.RemoveCredential)]
    public async Task MixedCredentialBatch_RejectsWrongTypeAndKeepsSuccessfulItem(CardholderOperationsAction action)
    {
        var holder = Guid.NewGuid();
        var valid = Guid.NewGuid();
        var wrong = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(holder, "Cardholder");
        handler.AddEntity(valid, "Credential");
        handler.AddEntity(wrong, "UserGroup");
        var batch = Assert.IsType<Genetec.Mcp.SecurityCenter.Tools.BatchCreateResult>(await CardholderOperationsTool.Execute(TestHelpers.FakeManager(handler),
            new CardholderOperationsRequest(action, Id: holder, CredentialIds: [valid, wrong])));
        Assert.Equal(1, batch.SuccessCount);
        Assert.Equal(1, batch.FailureCount);
        Assert.True(batch.Results[0].Success);
        Assert.False(batch.Results[1].Success);
        Assert.Contains("UserGroup", batch.Results[1].Error);
        var mutation = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
        Assert.Contains(valid.ToString(), Uri.UnescapeDataString(mutation.Uri.Query));
        Assert.DoesNotContain(wrong.ToString(), Uri.UnescapeDataString(mutation.Uri.Query));
    }

    [Theory]
    [InlineData(CardholderOperationsAction.AddCredential, "credential_ids")]
    [InlineData(CardholderOperationsAction.RemoveCredential, "credential_ids")]
    public async Task WrongLinkedType_RejectsBeforeMutation(CardholderOperationsAction action, string parameter)
    {
        var id = Guid.NewGuid();
        var wrong = Guid.NewGuid();
        var escort = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "Cardholder");
        handler.AddEntity(wrong, "UserGroup");
        handler.AddEntity(escort, "Cardholder");
        var request = (action, parameter) switch
        {
            (CardholderOperationsAction.AddCredential, "credential_ids") => new CardholderOperationsRequest(action, Id: id, CredentialIds: [wrong]),
            (CardholderOperationsAction.RemoveCredential, "credential_ids") => new CardholderOperationsRequest(action, Id: id, CredentialIds: [wrong]),
            _ => throw new InvalidOperationException()
        };
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CardholderOperationsTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal(parameter, error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<CardholderOperationsAction>()
        .Where(action => action != CardholderOperationsAction.ActivityReport && action != CardholderOperationsAction.SearchByStatus && action != CardholderOperationsAction.SearchByExpiration).Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsBeforeMutation(CardholderOperationsAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CardholderOperationsTool.Execute(TestHelpers.FakeManager(handler),
            new CardholderOperationsRequest(action, Id: id, CredentialIds: [id])));
        Assert.Equal("id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }


    [Fact]
    public void AccessStatus_ContainsOnlySupportedCardholderStates()
    {
        Assert.Equal(["Active", "Inactive", "Expired"], Enum.GetNames<AccessStatus>());
    }

    [Fact]
    public async Task ActivityReport_WithoutTimeRange_PreservesEntityFilter()
    {
        var handler = new Tools.ToolTestHandler();
        var id = Guid.NewGuid();

        await CardholderOperationsTool.Execute(TestHelpers.FakeManager(handler),
            new CardholderOperationsRequest(CardholderOperationsAction.ActivityReport, Id: id));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            $"/WebSdk/report/CardholderActivity?q=Cardholders@{id}",
            Uri.UnescapeDataString(request.AbsolutePath + request.Query));
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task SearchByStatus_NullStatus_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CardholderOperationsTool.Execute(TestHelpers.FakeManager(),
                new CardholderOperationsRequest(
                    CardholderOperationsAction.SearchByStatus)));

        Assert.Contains("status", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddCredential_NullCredentialIdAndNullCredentialIds_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CardholderOperationsTool.Execute(TestHelpers.FakeManager(),
                new CardholderOperationsRequest(
                    CardholderOperationsAction.AddCredential,
                    Id: Guid.NewGuid())));

        Assert.Contains("credential_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(CardholderOperationsAction.GetCredentials)]
    [InlineData(CardholderOperationsAction.GetPicture)]
    [InlineData(CardholderOperationsAction.ActivityReport)]
    public async Task IdAction_MissingId_ThrowsArgumentException(CardholderOperationsAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CardholderOperationsTool.Execute(TestHelpers.FakeManager(),
                new CardholderOperationsRequest(action)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddCredential_EmptyCredentialIds_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CardholderOperationsTool.Execute(TestHelpers.FakeManager(),
                new CardholderOperationsRequest(CardholderOperationsAction.AddCredential,
                    Id: Guid.NewGuid(), CredentialIds: [])));

        Assert.Contains("credential_ids", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task RemoveCredential_MissingCredentialIds_ThrowsArgumentException(int? count)
    {
        List<Guid>? ids = count is null ? null : [];
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CardholderOperationsTool.Execute(TestHelpers.FakeManager(),
                new CardholderOperationsRequest(CardholderOperationsAction.RemoveCredential,
                    Id: Guid.NewGuid(), CredentialIds: ids)));

        Assert.Contains("credential_ids", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddCredential_MissingCardholderId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CardholderOperationsTool.Execute(TestHelpers.FakeManager(),
                new CardholderOperationsRequest(CardholderOperationsAction.AddCredential,
                    CredentialIds: [Guid.NewGuid()])));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveCredential_MissingCardholderId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CardholderOperationsTool.Execute(TestHelpers.FakeManager(),
                new CardholderOperationsRequest(CardholderOperationsAction.RemoveCredential,
                    CredentialIds: [Guid.NewGuid()])));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActivityReport_WithTimeRange_UsesTimeFilter()
    {
        var handler = new Tools.ToolTestHandler();
        var id = Guid.NewGuid();

        await CardholderOperationsTool.Execute(TestHelpers.FakeManager(handler),
            new CardholderOperationsRequest(CardholderOperationsAction.ActivityReport,
                Id: id, TimeStart: "2024-01-01T00:00:00Z", TimeEnd: "2024-01-02T00:00:00Z"));

        var uri = Uri.UnescapeDataString(Assert.Single(handler.Requests).ToString());
        Assert.Contains(
            $"report/CardholderActivity?q=Cardholders@{id},TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z)",
            uri,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActivityReport_WithOnlyOneTimeBoundary_ThrowsArgumentException(bool provideStart)
    {
        var request = new CardholderOperationsRequest(CardholderOperationsAction.ActivityReport,
            Id: Guid.NewGuid(),
            TimeStart: provideStart ? "2024-01-01T00:00:00Z" : null,
            TimeEnd: provideStart ? null : "2024-01-02T00:00:00Z");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CardholderOperationsTool.Execute(TestHelpers.FakeManager(), request));

        Assert.Contains("time_start", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("time_end", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
