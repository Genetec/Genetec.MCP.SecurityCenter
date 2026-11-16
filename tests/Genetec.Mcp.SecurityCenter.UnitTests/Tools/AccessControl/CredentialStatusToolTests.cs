using Genetec.Mcp.SecurityCenter.Client;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class CredentialStatusToolTests
{
    [Fact]
    public async Task EmptyBatch_PreservesExistingRejection()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CredentialStatusTool.Execute(TestHelpers.FakeManager(),
            new CredentialStatusRequest(CredentialStatusAction.Activate, Items: [])));
    }

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task MixedBatch_RejectsWrongTypeAndKeepsSuccessfulItem(CredentialStatusAction action)
    {
        var validId = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(validId, "Credential");
        handler.AddEntity(wrongId, "UserGroup");
        var result = Assert.IsType<BatchMethodResult>(await CredentialStatusTool.Execute(TestHelpers.FakeManager(handler),
            new CredentialStatusRequest(action, Items: [wrongId, validId], ActivationDate: "2030-01-01T00:00:00Z",
                ExpirationDate: "2031-01-01T00:00:00Z", UnusedDays: 30, FirstUseDays: 30)));
        var batch = result;
        Assert.Equal(2, batch.RequestedCount);
        Assert.Equal(1, batch.SuccessCount);
        Assert.Equal(1, batch.FailureCount);
        var failed = batch.Results[0];
        Assert.False(failed.Success);
        Assert.Equal(wrongId, failed.Id);
        Assert.Contains("must identify Credential", failed.Error);
        Assert.True(batch.Results[1].Success);
        var mutation = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
        Assert.Contains(validId.ToString(), Uri.UnescapeDataString(mutation.Uri.Query));
        Assert.DoesNotContain(wrongId.ToString(), Uri.UnescapeDataString(mutation.Uri.Query));
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<CredentialStatusAction>()
        .Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsBeforeMutation(CredentialStatusAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CredentialStatusTool.Execute(TestHelpers.FakeManager(handler),
            new CredentialStatusRequest(action, Items: [id], ActivationDate: "2030-01-01T00:00:00Z", ExpirationDate: "2031-01-01T00:00:00Z", UnusedDays: 30, FirstUseDays: 30)));
        Assert.Equal("items", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }


    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task ActivateOnDate_NullActivationDate_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialStatusTool.Execute(TestHelpers.FakeManager(),
                new CredentialStatusRequest(CredentialStatusAction.ActivateOnDate,
                    Items: [Guid.NewGuid()])));

        Assert.Contains("activation_date", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActivateDateRange_NullActivationDate_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialStatusTool.Execute(TestHelpers.FakeManager(),
                new CredentialStatusRequest(CredentialStatusAction.ActivateDateRange,
                    Items: [Guid.NewGuid()],
                    ExpirationDate: "2025-12-31T00:00:00Z")));

        Assert.Contains("activation_date", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActivateDateRange_NullExpirationDate_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialStatusTool.Execute(TestHelpers.FakeManager(),
                new CredentialStatusRequest(CredentialStatusAction.ActivateDateRange,
                    Items: [Guid.NewGuid()],
                    ActivationDate: "2025-01-01T00:00:00Z")));

        Assert.Contains("expiration_date", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeactivateOnDate_NullExpirationDate_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialStatusTool.Execute(TestHelpers.FakeManager(),
                new CredentialStatusRequest(CredentialStatusAction.DeactivateOnDate,
                    Items: [Guid.NewGuid()])));

        Assert.Contains("expiration_date", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetExpirationIfUnused_NullUnusedDays_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialStatusTool.Execute(TestHelpers.FakeManager(),
                new CredentialStatusRequest(CredentialStatusAction.SetExpirationIfUnused,
                    Items: [Guid.NewGuid()])));

        Assert.Contains("unused_days", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetExpirationUponFirstUse_NullFirstUseDays_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialStatusTool.Execute(TestHelpers.FakeManager(),
                new CredentialStatusRequest(CredentialStatusAction.SetExpirationUponFirstUse,
                    Items: [Guid.NewGuid()])));

        Assert.Contains("first_use_days", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
