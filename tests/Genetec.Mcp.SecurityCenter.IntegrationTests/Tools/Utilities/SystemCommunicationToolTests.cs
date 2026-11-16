using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Utilities;

[Trait("Category", "Integration")]
public class SystemCommunicationToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(CommunicationAction.SendMessage)]
    [InlineData(CommunicationAction.SendEmail)]
    [InlineData(CommunicationAction.PlaySound)]
    public async Task WrongRecipientType_IsRejectedWithoutChangingEntity(CommunicationAction action)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var id = Guid.Parse("00000000-0000-0000-0000-000000000007");
        var before = await sc.Manager.GetClient().GetEntityFields(id.ToString(), "Guid", "Name");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => SystemCommunicationTool.Execute(sc.Manager,
            new SystemCommunicationRequest(action, RecipientId: id, Message: "test", SoundId: id)));
        Assert.Equal("recipient_id", error.ParamName);
        Assert.Contains("User", error.Message);
        Assert.Equal(before, await sc.Manager.GetClient().GetEntityFields(id.ToString(), "Guid", "Name"));
    }

    [Fact]
    public async Task PlaySound_WrongSoundType_IsRejected()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var userId = await GetFirstEntityIdAsync("User");
        Assert.NotNull(userId);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => SystemCommunicationTool.Execute(sc.Manager,
            new SystemCommunicationRequest(CommunicationAction.PlaySound, RecipientId: userId, SoundId: userId)));
        Assert.Equal("sound_id", error.ParamName);
        Assert.Contains("File", error.Message);
    }

    // ── Integration ─────────────────────────────────────────────────
    // Verifies the WebSdk endpoint accepts each call. Actual delivery
    // (email receipt, sound playback, message display) is not validated.

    [Fact]
    public async Task SendEmail_ValidUser_WebSdkAcceptsCall()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var userId = await GetFirstEntityIdAsync("User");
        Assert.NotNull(userId);

        var result = await SystemCommunicationTool.Execute(sc.Manager,
            new SystemCommunicationRequest(CommunicationAction.SendEmail,
                RecipientId: userId,
                Message: "MCP integration test email."));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task SendMessage_ValidUser_WebSdkAcceptsCall()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var userId = await GetFirstEntityIdAsync("User");
        Assert.NotNull(userId);

        var result = await SystemCommunicationTool.Execute(sc.Manager,
            new SystemCommunicationRequest(CommunicationAction.SendMessage,
                RecipientId: userId,
                Message: "MCP integration test message."));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task PlaySound_ValidUserAndFile_WebSdkAcceptsCall()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var userId = await GetFirstEntityIdAsync("User");
        Assert.NotNull(userId);
        var fileId = await GetFirstEntityIdAsync("File");
        Assert.NotNull(fileId);

        var result = await SystemCommunicationTool.Execute(sc.Manager,
            new SystemCommunicationRequest(CommunicationAction.PlaySound,
                RecipientId: userId,
                SoundId: fileId));

        Assert.NotNull(result);
    }

    private async Task<Guid?> GetFirstEntityIdAsync(string entityType)
    {
        var json = await sc.Manager.GetClient().ListEntities(entityType, 1, 1);
        return ScIntegrationFixture.ExtractFirstGuid(json);
    }
}
