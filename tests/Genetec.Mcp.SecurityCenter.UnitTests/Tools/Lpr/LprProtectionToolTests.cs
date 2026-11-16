using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Lpr;

public class LprProtectionToolTests
{
    private static readonly Guid RecordId = Guid.NewGuid();
    private static readonly Guid SourceId = Guid.NewGuid();
    private const string Timestamp = "2026-08-20T00:00:00.000Z";

    [Theory]
    [InlineData(LprProtectionAction.ProtectHit, "hit_id")]
    [InlineData(LprProtectionAction.UnprotectHit, "hit_id")]
    [InlineData(LprProtectionAction.ProtectRead, "read_id")]
    [InlineData(LprProtectionAction.UnprotectRead, "read_id")]
    public async Task MissingRecordId_ThrowsArgumentException(
        LprProtectionAction action,
        string parameterName)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            LprProtectionTool.Execute(TestHelpers.FakeManager(),
                BuildRequest(action, sourceId: SourceId, timestamp: Timestamp)));

        Assert.Contains(parameterName, ex.Message);
    }

    [Theory]
    [InlineData(LprProtectionAction.ProtectHit)]
    [InlineData(LprProtectionAction.UnprotectHit)]
    [InlineData(LprProtectionAction.ProtectRead)]
    [InlineData(LprProtectionAction.UnprotectRead)]
    public async Task MissingSourceId_ThrowsArgumentException(LprProtectionAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            LprProtectionTool.Execute(TestHelpers.FakeManager(),
                BuildRequest(action, recordId: RecordId, timestamp: Timestamp)));

        Assert.Contains("source_id", ex.Message);
    }

    [Theory]
    [InlineData(LprProtectionAction.ProtectHit, "hit_timestamp_utc")]
    [InlineData(LprProtectionAction.UnprotectHit, "hit_timestamp_utc")]
    [InlineData(LprProtectionAction.ProtectRead, "read_timestamp_utc")]
    [InlineData(LprProtectionAction.UnprotectRead, "read_timestamp_utc")]
    public async Task MissingTimestamp_ThrowsArgumentException(
        LprProtectionAction action,
        string parameterName)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            LprProtectionTool.Execute(TestHelpers.FakeManager(),
                BuildRequest(action, recordId: RecordId, sourceId: SourceId)));

        Assert.Contains(parameterName, ex.Message);
    }

    [Theory]
    [InlineData(LprProtectionAction.ProtectHit, "HitTimestampUtc")]
    [InlineData(LprProtectionAction.UnprotectHit, "HitTimestampUtc")]
    [InlineData(LprProtectionAction.ProtectRead, "ReadTimestampUtc")]
    [InlineData(LprProtectionAction.UnprotectRead, "ReadTimestampUtc")]
    public void InvalidTimestamp_FailsValidation(
        LprProtectionAction action,
        string propertyName)
    {
        var request = BuildRequest(action, RecordId, SourceId, "not-a-date");
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(
            request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Contains(results, result => result.MemberNames.Contains(propertyName));
    }

    private static LprProtectionRequest BuildRequest(
        LprProtectionAction action,
        Guid? recordId = null,
        Guid? sourceId = null,
        string? timestamp = null)
    {
        return action switch
        {
            LprProtectionAction.ProtectHit => new LprProtectionRequest(
                action, HitId: recordId, SourceId: sourceId, HitTimestampUtc: timestamp),
            LprProtectionAction.UnprotectHit => new LprProtectionRequest(
                action, HitId: recordId, SourceId: sourceId, HitTimestampUtc: timestamp),
            LprProtectionAction.ProtectRead => new LprProtectionRequest(
                action, ReadId: recordId, SourceId: sourceId, ReadTimestampUtc: timestamp),
            LprProtectionAction.UnprotectRead => new LprProtectionRequest(
                action, ReadId: recordId, SourceId: sourceId, ReadTimestampUtc: timestamp),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };
    }
}
