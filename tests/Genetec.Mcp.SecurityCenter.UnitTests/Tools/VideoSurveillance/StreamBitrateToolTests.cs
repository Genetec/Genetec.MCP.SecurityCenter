using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;
using System.ComponentModel.DataAnnotations;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.VideoSurveillance;

public class StreamBitrateToolTests
{
    [Fact]
    public async Task RelatedEntityTargets_RejectWrongEntityType()
    {
        await TestHelpers.AssertWrongRelatedEntityTypeAsync("Stream", (manager, primaryId, relatedId) => StreamBitrateTool.Execute(manager, new StreamBitrateRequest(primaryId, 512, relatedId)), "schedule_id", "UserGroup");
    }

    [Fact]
    public async Task CurrentEntityTargets_RejectWrongEntityType()
    {
        await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => StreamBitrateTool.Execute(manager, new StreamBitrateRequest(id, 512)), "id", "UserGroup");
    }

    [Fact]
    public void BitRate_Zero_FailsValidation()
    {
        var request = new StreamBitrateRequest(Guid.NewGuid(), 0);
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(
            request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(StreamBitrateRequest.BitRate)));
    }

    [Fact]
    public void BitRate_Negative_FailsValidation()
    {
        var request = new StreamBitrateRequest(Guid.NewGuid(), -1);
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(
            request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(StreamBitrateRequest.BitRate)));
    }
}
