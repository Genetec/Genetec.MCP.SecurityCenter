using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Utilities;

public class SystemCommunicationToolTests
{
    [Theory]
    [InlineData(CommunicationAction.SendMessage)]
    [InlineData(CommunicationAction.SendEmail)]
    [InlineData(CommunicationAction.PlaySound)]
    public async Task EmptyRecipient_IsRejected(CommunicationAction action)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => SystemCommunicationTool.Execute(
            TestHelpers.FakeManager(), new SystemCommunicationRequest(action,
                RecipientId: Guid.Empty, Message: "test", SoundId: Guid.NewGuid())));
        Assert.Equal("recipient_id", error.ParamName);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task SendMessage_MissingRecipientId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemCommunicationTool.Execute(TestHelpers.FakeManager(),
                new SystemCommunicationRequest(CommunicationAction.SendMessage, Message: "hi")));
        Assert.Contains("recipient_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendMessage_MissingMessage_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemCommunicationTool.Execute(TestHelpers.FakeManager(),
                new SystemCommunicationRequest(CommunicationAction.SendMessage, RecipientId: Guid.NewGuid())));
        Assert.Contains("message", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendEmail_MissingRecipientId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemCommunicationTool.Execute(TestHelpers.FakeManager(),
                new SystemCommunicationRequest(CommunicationAction.SendEmail, Message: "body")));
        Assert.Contains("recipient_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendEmail_MissingMessage_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemCommunicationTool.Execute(TestHelpers.FakeManager(),
                new SystemCommunicationRequest(CommunicationAction.SendEmail, RecipientId: Guid.NewGuid())));
        Assert.Contains("message", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlaySound_MissingRecipientId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemCommunicationTool.Execute(TestHelpers.FakeManager(),
                new SystemCommunicationRequest(CommunicationAction.PlaySound, SoundId: Guid.NewGuid())));
        Assert.Contains("recipient_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlaySound_MissingSoundId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemCommunicationTool.Execute(TestHelpers.FakeManager(),
                new SystemCommunicationRequest(CommunicationAction.PlaySound, RecipientId: Guid.NewGuid())));
        Assert.Contains("sound_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
