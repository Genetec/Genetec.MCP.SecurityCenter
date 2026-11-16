using System.Net;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class ReaderConfigurationToolTests
{
    [Fact]
    public void Discovery_ReferencesAssuranceProfileLookupAndGuidInput()
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            SearchToolsTool.Execute(new SearchToolsRequest(Query: "reader_configuration", Detail: ToolDetailLevel.Full)),
            NumericStringConverter.ToolOptions));
        var tool = Assert.Single(document.RootElement.EnumerateArray(),
            item => item.GetProperty("name").GetString() == "reader_configuration");
        var request = Assert.Single(tool.GetProperty("parameters").EnumerateArray());
        var profile = Assert.Single(request.GetProperty("fields").EnumerateArray(),
            field => field.GetProperty("name").GetString() == "assurance_profile_id");
        Assert.Equal("Guid", profile.GetProperty("type").GetString());
        Assert.Contains("ProfileId from assurance_profile_list", profile.GetProperty("description").GetString());
        Assert.Contains("required for enable_assurance_profile", profile.GetProperty("description").GetString());
    }

    [Fact]
    public void EnableAssuranceProfile_ProductionBindingPreservesProfileId()
    {
        var readerId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var request = JsonSerializer.Deserialize<ReaderConfigRequest>(
            $$"""{"id":"{{readerId}}","action":"enable_assurance_profile","assurance_profile_id":"{{profileId}}"}""",
            NumericStringConverter.ToolOptions)!;
        Assert.Equal(readerId, request.Id);
        Assert.Equal(ReaderConfigAction.EnableAssuranceProfile, request.Action);
        Assert.Equal(profileId, request.AssuranceProfileId);
    }

    [Fact]
    public async Task EnableCardAndPin_WrongScheduleType_RejectsBeforeMutation()
    {
        var id = Guid.NewGuid();
        var wrong = Guid.NewGuid();
        var handler = new ReaderConfigurationHandler(
            $"{{\"Guid\":\"{id}\",\"EntityType\":\"Device\",\"DeviceType\":\"Reader\"}}", scheduleId: wrong, scheduleType: "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ReaderConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new ReaderConfigRequest(id, ReaderConfigAction.EnableCardAndPin, ActivationScheduleId: wrong, FailureTimeout: TimeSpan.FromSeconds(10))));
        Assert.Equal("activation_schedule_id", error.ParamName);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<ReaderConfigAction>()
        .Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task MalformedAccessPointDevice_RejectsBeforeMutation(ReaderConfigAction action)
    {
        var id = Guid.NewGuid();
        var handler = new ReaderConfigurationHandler(
            $"{{\"Guid\":\"{id}\",\"EntityType\":\"AccessPoint\",\"AccessPointType\":\"CardReader\",\"Device\":\"not-a-guid\"}}");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ReaderConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new ReaderConfigRequest(id, action, AssuranceProfileId: Guid.NewGuid(), ActivationScheduleId: Guid.NewGuid(), FailureTimeout: TimeSpan.FromSeconds(10))));
        Assert.IsType<System.Text.Json.JsonException>(error.InnerException);
        Assert.Equal(HttpMethod.Get, Assert.Single(handler.Requests).Method);
    }

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsBeforeMutation(ReaderConfigAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ReaderConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new ReaderConfigRequest(id, action, AssuranceProfileId: id, ActivationScheduleId: id, FailureTimeout: TimeSpan.FromSeconds(10))));
        Assert.Equal("id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }


    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task EnableAssuranceProfile_NullProfileId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReaderConfigurationTool.Execute(TestHelpers.FakeManager(),
                new ReaderConfigRequest(Id: Guid.NewGuid(), Action: ReaderConfigAction.EnableAssuranceProfile, AssuranceProfileId: null)));

        Assert.Contains("assurance_profile_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnableCardAndPin_NullActivationSchedule_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReaderConfigurationTool.Execute(TestHelpers.FakeManager(),
                new ReaderConfigRequest(
                    Id: Guid.NewGuid(),
                    Action: ReaderConfigAction.EnableCardAndPin,
                    ActivationScheduleId: null,
                    FailureTimeout: TimeSpan.FromSeconds(30))));

        Assert.Contains("activation_schedule_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnableCardAndPin_NullFailureTimeout_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReaderConfigurationTool.Execute(TestHelpers.FakeManager(),
                new ReaderConfigRequest(
                    Id: Guid.NewGuid(),
                    Action: ReaderConfigAction.EnableCardAndPin,
                    ActivationScheduleId: Guid.NewGuid(),
                    FailureTimeout: null)));

        Assert.Contains("failure_timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AllSupportedActions_DispatchToSecurityCenter()
    {
        var id = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var handler = new ToolTestHandler
        {
            ResponseFactory = request => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(request.Method != HttpMethod.Get ? "{}" :
                    Uri.UnescapeDataString(request.RequestUri!.Query).Contains(scheduleId.ToString(), StringComparison.Ordinal)
                        ? $"{{\"Guid\":\"{scheduleId}\",\"EntityType\":\"Schedule\"}}"
                        : $"{{\"Guid\":\"{id}\",\"EntityType\":\"Device\",\"DeviceType\":\"Reader\"}}")
            }
        };
        var manager = TestHelpers.FakeManager(handler);

        var requests = new[]
        {
            new ReaderConfigRequest(id, ReaderConfigAction.Get),
            new ReaderConfigRequest(id, ReaderConfigAction.EnableCardAndPin,
                ActivationScheduleId: scheduleId, FailureTimeout: TimeSpan.FromSeconds(10)),
            new ReaderConfigRequest(id, ReaderConfigAction.EnableCardOrPin),
            new ReaderConfigRequest(id, ReaderConfigAction.DisableCardAndPin),
            new ReaderConfigRequest(id, ReaderConfigAction.EnableAssuranceProfile,
                AssuranceProfileId: Guid.NewGuid())
        };

        foreach (var request in requests)
            Assert.NotNull(await ReaderConfigurationTool.Execute(manager, request));

        Assert.Equal(requests.Length * 2 + 1, handler.Requests.Count);
        Assert.Contains(handler.Requests, uri =>
            Uri.UnescapeDataString(uri.Query).Contains("EnableCardAndPin(" + requests[1].ActivationScheduleId + ",00:00:10)", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, uri => Uri.UnescapeDataString(uri.Query).Contains("EnableCardAndPin", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, uri => Uri.UnescapeDataString(uri.Query).Contains("EnableCardOrPin", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, uri => Uri.UnescapeDataString(uri.Query).Contains("DisableCardAndPin", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, uri => Uri.UnescapeDataString(uri.Query).Contains("EnableAssuranceProfile", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, uri => uri.Query.Contains("%3D", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AccessPointId_ResolvesReaderDeviceBeforeEnableCardAndPin()
    {
        var accessPointId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var readerDeviceId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var scheduleId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var handler = new ReaderConfigurationHandler(
            $"{{\"Guid\":\"{accessPointId}\",\"EntityType\":\"AccessPoint\",\"AccessPointType\":\"CardReader\",\"Device\":\"{readerDeviceId}\"}}", readerDeviceId, scheduleId);

        await ReaderConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new ReaderConfigRequest(accessPointId, ReaderConfigAction.EnableCardAndPin,
                ActivationScheduleId: scheduleId, FailureTimeout: TimeSpan.FromSeconds(10)));

        Assert.Equal(4, handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Contains($"entity/{accessPointId}", handler.Requests[0].RequestUri!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpMethod.Post, handler.Requests[3].Method);
        var postUri = handler.Requests[3].RequestUri!;
        var post = Uri.UnescapeDataString(postUri.ToString());
        Assert.Contains($"entity={readerDeviceId},EnableCardAndPin({scheduleId},00:00:10)", post, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"entity={accessPointId},EnableCardAndPin", post, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%3D", postUri.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AccessPointWithoutReaderDevice_ThrowsHelpfulError()
    {
        var accessPointId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var handler = new ReaderConfigurationHandler(
            $"{{\"Guid\":\"{accessPointId}\",\"EntityType\":\"AccessPoint\",\"AccessPointType\":\"CardReader\",\"Device\":\"00000000-0000-0000-0000-000000000000\"}}");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ReaderConfigurationTool.Execute(TestHelpers.FakeManager(handler),
                new ReaderConfigRequest(accessPointId, ReaderConfigAction.EnableCardOrPin)));

        Assert.Contains("no associated reader device", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000)]
    public async Task EnableCardAndPin_InvalidFailureTimeout_ThrowsArgumentException(int seconds)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReaderConfigurationTool.Execute(TestHelpers.FakeManager(),
                new ReaderConfigRequest(
                    Guid.NewGuid(),
                    ReaderConfigAction.EnableCardAndPin,
                    ActivationScheduleId: Guid.NewGuid(),
                    FailureTimeout: TimeSpan.FromSeconds(seconds))));

        Assert.Contains("failure_timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ReaderConfigurationHandler(string entityResponse, Guid? readerId = null, Guid? scheduleId = null, string scheduleType = "Schedule") : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var body = request.Method != HttpMethod.Get ? "{}" :
                scheduleId is not null && Uri.UnescapeDataString(request.RequestUri!.Query).Contains(scheduleId.Value.ToString(), StringComparison.Ordinal)
                    ? $"{{\"Guid\":\"{scheduleId}\",\"EntityType\":\"{scheduleType}\"}}" :
                readerId is not null && request.RequestUri!.AbsolutePath.EndsWith(readerId.Value.ToString(), StringComparison.OrdinalIgnoreCase)
                    ? $"{{\"Guid\":\"{readerId}\",\"EntityType\":\"Device\",\"DeviceType\":\"Reader\"}}" : entityResponse;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        }
    }

}
