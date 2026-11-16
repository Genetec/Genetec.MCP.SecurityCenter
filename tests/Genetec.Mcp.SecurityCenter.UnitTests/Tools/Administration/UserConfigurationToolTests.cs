using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Administration;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class UserConfigurationToolTests
{
    [Theory]
    [InlineData(UserConfigurationAction.AddToGroup)]
    [InlineData(UserConfigurationAction.RemoveFromGroup)]
    [InlineData(UserConfigurationAction.EnableWatermarking)]
    [InlineData(UserConfigurationAction.DisableWatermarking)]
    [InlineData(UserConfigurationAction.AddPtzPriority)]
    [InlineData(UserConfigurationAction.RemovePtzPriority)]
    [InlineData(UserConfigurationAction.SetDomain)]
    [InlineData(UserConfigurationAction.SetExternalId)]
    [InlineData(UserConfigurationAction.GetSecurityLevelForCamera)]
    public async Task User_WrongEntityType_RejectsBeforeMutation(UserConfigurationAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var request = new UserConfigurationRequest(action, Id: id, UserGroupId: Guid.NewGuid(),
            PtzEntityId: Guid.NewGuid(), PtzPriority: 50, Domain: "test", ExternalId: "test", TargetEntityId: Guid.NewGuid());
        var error = await Assert.ThrowsAsync<ArgumentException>(() => UserConfigurationTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal("id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Theory]
    [InlineData(UserConfigurationAction.AddToGroup, "user_group_id")]
    [InlineData(UserConfigurationAction.RemoveFromGroup, "user_group_id")]
    [InlineData(UserConfigurationAction.GetSecurityLevelForCamera, "target_entity_id")]
    public async Task LinkedEntity_WrongType_RejectsBeforeMutation(UserConfigurationAction action, string parameter)
    {
        var userId = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(userId, "User");
        handler.AddEntity(wrongId, "Area");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => UserConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new UserConfigurationRequest(action, Id: userId, UserGroupId: wrongId, TargetEntityId: wrongId)));
        Assert.Equal(parameter, error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Theory]
    [InlineData("activated,Name=Injected")]
    [InlineData("activated@deactivated")]
    [InlineData("unknown")]
    public void Search_StatusGrammar_RejectsProductionBinding(string status)
    {
        var json = JsonSerializer.Serialize(new { action = "search", status = new[] { status } });
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UserConfigurationRequest>(json, NumericStringConverter.ToolOptions));
    }

    [Fact]
    public void Search_Status_BindsClosedEnumCollection()
    {
        const string json = """{"action":"search","status":["activated","deactivated"]}""";
        var request = JsonSerializer.Deserialize<UserConfigurationRequest>(json, NumericStringConverter.ToolOptions)!;
        Assert.Equal(new[] { GeneralUserStatus.Activated, GeneralUserStatus.Deactivated }, request.Status);
        var result = SearchToolsTool.Execute(new SearchToolsRequest("user_configuration", Detail: ToolDetailLevel.Full));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result, NumericStringConverter.ToolOptions));
        var tool = Assert.Single(document.RootElement.EnumerateArray());
        var fields = Assert.Single(tool.GetProperty("parameters").EnumerateArray()).GetProperty("fields");
        var status = Assert.Single(fields.EnumerateArray(), field => field.GetProperty("name").GetString() == "status");
        Assert.Equal("List<GeneralUserStatus>", status.GetProperty("type").GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ResourceLimits.MaxBatchItems + 1)]
    public void Search_Status_BindingValidatesCollectionBounds(int count)
    {
        var json = JsonSerializer.Serialize(new { action = "search", status = Enumerable.Repeat("activated", count).ToArray() });
        Assert.Throws<ValidationException>(() => JsonSerializer.Deserialize<UserConfigurationRequest>(json, NumericStringConverter.ToolOptions));
    }

    [Fact]
    public async Task Search_UndefinedStatus_RejectsBeforeSdk()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => UserConfigurationTool.Execute(TestHelpers.FakeManager(),
            new UserConfigurationRequest(UserConfigurationAction.Search, Status: [(GeneralUserStatus)999])));
        Assert.Equal("status", error.ParamName);
    }
    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task AddPtzPriority_NullPtzEntityId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserConfigurationTool.Execute(TestHelpers.FakeManager(),
                new UserConfigurationRequest(UserConfigurationAction.AddPtzPriority,
                    Id: Guid.NewGuid(), PtzEntityId: null, PtzPriority: 5)));

        Assert.Contains("ptz_entity_id", ex.Message);
    }

    [Fact]
    public async Task AddPtzPriority_NullPtzPriority_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserConfigurationTool.Execute(TestHelpers.FakeManager(),
                new UserConfigurationRequest(UserConfigurationAction.AddPtzPriority,
                    Id: Guid.NewGuid(), PtzEntityId: Guid.NewGuid(), PtzPriority: null)));

        Assert.Contains("ptz_priority", ex.Message);
    }

    [Theory]
    [InlineData(UserConfigurationAction.AddPtzPriority)]
    [InlineData(UserConfigurationAction.RemovePtzPriority)]
    public async Task PtzPriorityActions_UseWebSdkSignature(UserConfigurationAction action)
    {
        var handler = new EntityMetadataTestHandler();
        var manager = TestHelpers.FakeManager(handler);
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        handler.AddEntity(userId, "User");

        var result = await UserConfigurationTool.Execute(manager,
            new UserConfigurationRequest(
                action,
                Id: userId,
                PtzEntityId: entityId,
                PtzPriority: 50));

        Assert.IsType<string>(result);
        var request = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post).Uri;
        var expectedMethod = action == UserConfigurationAction.AddPtzPriority
            ? "AddPtzPriority"
            : "RemovePtzPriority";
        Assert.Equal(
            $"?q=entity={userId},{expectedMethod}(PtzPriority({entityId},50))",
            Uri.UnescapeDataString(request.Query));
        Assert.DoesNotContain("\\,", Uri.UnescapeDataString(request.Query));
    }

    [Theory]
    [InlineData(UserConfigurationAction.SetDomain, "SetDomain")]
    [InlineData(UserConfigurationAction.SetExternalId, "SetExternalUniqueId")]
    public async Task LiteralTextActions_EscapePunctuationBeforeUrlEncoding(
        UserConfigurationAction action, string method)
    {
        const string value = @"corp,(test)@east&west+100%50\leaf";
        var handler = new EntityMetadataTestHandler();
        var manager = TestHelpers.FakeManager(handler);
        var userId = Guid.NewGuid();
        handler.AddEntity(userId, "User");

        await UserConfigurationTool.Execute(manager,
            new UserConfigurationRequest(action, Id: userId,
                Domain: action == UserConfigurationAction.SetDomain ? value : null,
                ExternalId: action == UserConfigurationAction.SetExternalId ? value : null));

        var request = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post).Uri;
        var expected = $"?q=entity={userId},{method}({SecurityCenterRestClient.EscapeQueryValue(value)})";
        Assert.Equal(expected, Uri.UnescapeDataString(request.Query));
        Assert.Contains("%26", request.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%2B", request.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%25", request.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("%2526", request.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_NameSearchModeWithoutName_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserConfigurationTool.Execute(TestHelpers.FakeManager(),
                new UserConfigurationRequest(UserConfigurationAction.Search,
                    NameSearchMode: EntityNameSearchMode.Contains, Name: null)));

        Assert.Contains("name", ex.Message);
    }

    [Fact]
    public async Task AddToGroup_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserConfigurationTool.Execute(TestHelpers.FakeManager(),
                new UserConfigurationRequest(UserConfigurationAction.AddToGroup,
                    Id: null, UserGroupId: Guid.NewGuid())));

        Assert.Contains("id", ex.Message);
    }

    [Fact]
    public async Task AddToGroup_NullUserGroupId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserConfigurationTool.Execute(TestHelpers.FakeManager(),
                new UserConfigurationRequest(UserConfigurationAction.AddToGroup,
                    Id: Guid.NewGuid(), UserGroupId: null)));

        Assert.Contains("user_group_id", ex.Message);
    }

    [Fact]
    public async Task SetDomain_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserConfigurationTool.Execute(TestHelpers.FakeManager(),
                new UserConfigurationRequest(UserConfigurationAction.SetDomain,
                    Id: null, Domain: "corp.local")));

        Assert.Contains("id", ex.Message);
    }

    [Fact]
    public async Task SetExternalId_NullExternalId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserConfigurationTool.Execute(TestHelpers.FakeManager(),
                new UserConfigurationRequest(UserConfigurationAction.SetExternalId,
                    Id: Guid.NewGuid(), ExternalId: null)));

        Assert.Contains("external_id", ex.Message);
    }

    [Fact]
    public async Task GetSecurityLevelForCamera_NullTargetEntityId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserConfigurationTool.Execute(TestHelpers.FakeManager(),
                new UserConfigurationRequest(UserConfigurationAction.GetSecurityLevelForCamera,
                    Id: Guid.NewGuid(), TargetEntityId: null)));

        Assert.Contains("target_entity_id", ex.Message);
    }
}
