using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Lpr;

public class LprProtectionToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    private static readonly Guid RecordId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SourceId =
        Guid.Parse("00000000-0000-0000-0000-000000000002");
    private const string Timestamp = "2026-08-20T00:00:00.000Z";

    [Theory]
    [InlineData(LprProtectionAction.ProtectHit)]
    [InlineData(LprProtectionAction.UnprotectHit)]
    [InlineData(LprProtectionAction.ProtectRead)]
    [InlineData(LprProtectionAction.UnprotectRead)]
    public async Task UnknownRecord_UsesWebSdkActionSignature(LprProtectionAction action)
    {
        var result = await LprProtectionTool.Execute(sc.Manager,
            BuildRequest(action));

        Assert.Equal("[]", Assert.IsType<string>(result));
    }

    private static LprProtectionRequest BuildRequest(LprProtectionAction action)
    {
        return action switch
        {
            LprProtectionAction.ProtectHit => new LprProtectionRequest(
                action, HitId: RecordId, SourceId: SourceId, HitTimestampUtc: Timestamp),
            LprProtectionAction.UnprotectHit => new LprProtectionRequest(
                action, HitId: RecordId, SourceId: SourceId, HitTimestampUtc: Timestamp),
            LprProtectionAction.ProtectRead => new LprProtectionRequest(
                action, ReadId: RecordId, SourceId: SourceId, ReadTimestampUtc: Timestamp),
            LprProtectionAction.UnprotectRead => new LprProtectionRequest(
                action, ReadId: RecordId, SourceId: SourceId, ReadTimestampUtc: Timestamp),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };
    }
}
