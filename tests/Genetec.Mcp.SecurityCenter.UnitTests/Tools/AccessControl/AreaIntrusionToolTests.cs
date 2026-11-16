using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class AreaIntrusionToolTests
{
    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<AreaIntrusionAction>()
        .Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsBeforeMutation(AreaIntrusionAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AreaIntrusionTool.Execute(TestHelpers.FakeManager(handler),
            new AreaIntrusionRequest(action, IntrusionAreaId: id, DeviceId: id, Delay: 30, ArmingDelay: 10)));
        Assert.Equal(action is AreaIntrusionAction.BypassInputDevice or AreaIntrusionAction.ClearInputDeviceBypass ? "device_id" : "intrusion_area_id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }


    // ── Validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData(null, 10, "delay")]
    [InlineData(30, null, "arming_delay")]
    public async Task PostponeIntrusionArming_MissingDelay_RejectsBeforeHttp(int? delay, int? armingDelay, string parameterName)
    {
        var request = new AreaIntrusionRequest(AreaIntrusionAction.PostponeIntrusionArming,
            IntrusionAreaId: Guid.NewGuid(), Delay: delay, ArmingDelay: armingDelay);
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaIntrusionTool.Execute(TestHelpers.FakeManager(), request));
        Assert.Equal(parameterName, error.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PostponeIntrusionArming_InvalidArmingDelay_IsRejected(int armingDelay)
    {
        var request = new AreaIntrusionRequest(AreaIntrusionAction.PostponeIntrusionArming,
            IntrusionAreaId: Guid.NewGuid(), Delay: 30, ArmingDelay: armingDelay);
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(AreaIntrusionRequest.ArmingDelay)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void PostponeIntrusionArming_ArmingDelayBoundaries_AreAccepted(int armingDelay)
    {
        var request = new AreaIntrusionRequest(AreaIntrusionAction.PostponeIntrusionArming,
            IntrusionAreaId: Guid.NewGuid(), Delay: 30, ArmingDelay: armingDelay);
        Assert.True(Validator.TryValidateObject(request, new ValidationContext(request), null, true));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(65)]
    [InlineData(90061)]
    public void PostponeIntrusionArming_ProductionBindingRetainsSeconds(int seconds)
    {
        var id = Guid.NewGuid();
        var request = JsonSerializer.Deserialize<AreaIntrusionRequest>(
            $$"""{"action":"postpone_intrusion_arming","intrusion_area_id":"{{id}}","delay":{{seconds}},"arming_delay":{{seconds}}}""",
            NumericStringConverter.ToolOptions)!;
        Assert.Equal(AreaIntrusionAction.PostponeIntrusionArming, request.Action);
        Assert.Equal(id, request.IntrusionAreaId);
        Assert.Equal(seconds, request.Delay);
        Assert.Equal(seconds, request.ArmingDelay);
    }

    [Theory]
    [InlineData("delay", "\"not-a-number\"")]
    [InlineData("delay", "1.5")]
    [InlineData("delay", "true")]
    [InlineData("arming_delay", "\"not-a-number\"")]
    [InlineData("arming_delay", "1.5")]
    [InlineData("arming_delay", "true")]
    public void PostponeIntrusionArming_ProductionBindingRejectsNonIntegerDelay(string field, string value)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AreaIntrusionRequest>(
            $"{{\"action\":\"postpone_intrusion_arming\",\"{field}\":{value}}}", NumericStringConverter.ToolOptions));

    [Theory]
    [InlineData("delay")]
    [InlineData("arming_delay")]
    public void Discovery_DescribesConditionalDelayRequirementAndSeconds(string fieldName)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            SearchToolsTool.Execute(new SearchToolsRequest(Query: "area_intrusion", Detail: ToolDetailLevel.Full)),
            NumericStringConverter.ToolOptions));
        var tool = Assert.Single(document.RootElement.EnumerateArray(), item => item.GetProperty("name").GetString() == "area_intrusion");
        var request = Assert.Single(tool.GetProperty("parameters").EnumerateArray());
        var delay = Assert.Single(request.GetProperty("fields").EnumerateArray(), field => field.GetProperty("name").GetString() == fieldName);
        Assert.Contains("seconds", delay.GetProperty("description").GetString());
        Assert.Contains("required for postpone_intrusion_arming", delay.GetProperty("description").GetString());
        Assert.False(delay.GetProperty("required").GetBoolean());
        Assert.Equal("Int32", delay.GetProperty("type").GetString());
        if (fieldName == "arming_delay")
            Assert.Equal(1, delay.GetProperty("min").GetDouble());
    }

    [Fact]
    public async Task BypassInputDevice_NullDeviceId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaIntrusionTool.Execute(TestHelpers.FakeManager(),
                new AreaIntrusionRequest(AreaIntrusionAction.BypassInputDevice, DeviceId: null)));

        Assert.Contains("device_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AreaIntrusionAction.PostponeIntrusionArming)]
    [InlineData(AreaIntrusionAction.TriggerIntrusionAlarm)]
    [InlineData(AreaIntrusionAction.SilenceIntrusionAlarm)]
    [InlineData(AreaIntrusionAction.AcknowledgeIntrusionAlarm)]
    public async Task IntrusionAreaAction_NullIntrusionAreaId_ThrowsArgumentException(AreaIntrusionAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaIntrusionTool.Execute(TestHelpers.FakeManager(),
                new AreaIntrusionRequest(action)));

        Assert.Contains("intrusion_area_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClearInputDeviceBypass_NullDeviceId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaIntrusionTool.Execute(TestHelpers.FakeManager(),
                new AreaIntrusionRequest(AreaIntrusionAction.ClearInputDeviceBypass)));

        Assert.Contains("device_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
