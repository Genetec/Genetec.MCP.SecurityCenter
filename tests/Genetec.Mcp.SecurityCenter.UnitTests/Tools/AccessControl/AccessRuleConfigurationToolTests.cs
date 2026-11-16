using System.Net;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class AccessRuleConfigurationToolTests
{
    [Fact]
    public async Task IsMember_MixedTypes_KeepsValidResultAndRejectsWrongType()
    {
        var wrong = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(RuleId, "AccessRule");
        handler.AddEntity(CardholderId, "Cardholder");
        handler.AddEntity(wrong, "UserGroup");
        var result = await AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new AccessRuleConfigRequest(AccessRuleConfigAction.IsMember, RuleId, CardholderIds: [CardholderId, wrong]));
        var rows = Assert.IsType<CardholderMembershipCheckResult[]>(result);
        Assert.Equal(CardholderId, rows[0].CardholderId);
        Assert.Null(rows[0].Error);
        Assert.Equal(wrong, rows[1].CardholderId);
        Assert.Contains("UserGroup", rows[1].Error);
        Assert.DoesNotContain(handler.Requests, request => Uri.UnescapeDataString(request.Uri.Query)
            .Contains($"IsMember({wrong})", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(AccessRuleConfigAction.AddCardholder, "cardholder_ids")]
    [InlineData(AccessRuleConfigAction.RemoveCardholder, "cardholder_ids")]
    [InlineData(AccessRuleConfigAction.IsMember, "cardholder_ids")]
    [InlineData(AccessRuleConfigAction.SetSchedule, "schedule_id")]
    public async Task WrongLinkedType_RejectsBeforeMutation(AccessRuleConfigAction action, string parameter)
    {
        var id = Guid.NewGuid();
        var wrong = Guid.NewGuid();
        var escort = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "AccessRule");
        handler.AddEntity(wrong, "UserGroup");
        handler.AddEntity(escort, "Cardholder");
        var request = (action, parameter) switch
        {
            (AccessRuleConfigAction.AddCardholder, "cardholder_ids") => new AccessRuleConfigRequest(action, id, CardholderIds: [wrong]),
            (AccessRuleConfigAction.RemoveCardholder, "cardholder_ids") => new AccessRuleConfigRequest(action, id, CardholderIds: [wrong]),
            (AccessRuleConfigAction.IsMember, "cardholder_ids") => new AccessRuleConfigRequest(action, id, CardholderIds: [wrong]),
            (AccessRuleConfigAction.SetSchedule, "schedule_id") => new AccessRuleConfigRequest(action, id, ScheduleId: wrong),
            _ => throw new InvalidOperationException()
        };
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal(parameter, error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<AccessRuleConfigAction>()
        .Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsBeforeMutation(AccessRuleConfigAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new AccessRuleConfigRequest(action, Id: id, CardholderIds: [id], ScheduleId: id, IsDenyingRule: true, GroupIds: [id])));
        Assert.Equal("id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }


    private static readonly Guid RuleId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid CardholderId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid SecondCardholderId = Guid.Parse("12345678-1234-1234-1234-123456789012");
    private static readonly Guid ThirdCardholderId = Guid.Parse("23456789-2345-2345-2345-234567890123");
    private static readonly Guid ScheduleId = Guid.Parse("87654321-4321-4321-4321-210987654321");
    private static readonly Guid GroupId = Guid.Parse("fedcba98-7654-3210-fedc-ba9876543210");
    private static readonly Guid SecondGroupId = Guid.Parse("edcbaf98-7654-3210-fedc-ba9876543210");

    [Theory]
    [InlineData(AccessRuleConfigAction.AddCardholderGroup)]
    [InlineData(AccessRuleConfigAction.RemoveCardholderGroup)]
    public async Task GroupActions_ExpandedMembersOverLimit_RejectBeforeAnyMutation(AccessRuleConfigAction action)
    {
        var members = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToArray();
        var handler = new CapturingHandler
        {
            GetResponseBody = _ => JsonSerializer.Serialize(new { Members = members })
        };
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(handler),
                new AccessRuleConfigRequest(action, RuleId, GroupIds: [GroupId])));
        Assert.Equal("group_ids", error.ParamName);
        Assert.Contains("51 operations", error.Message, StringComparison.Ordinal);
        Assert.NotEmpty(handler.Requests);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    [Theory]
    [InlineData(AccessRuleConfigAction.AddCardholderGroup)]
    [InlineData(AccessRuleConfigAction.RemoveCardholderGroup)]
    public async Task GroupActions_LaterGroupExceedsCumulativeBudget_RejectBeforeAnyMutation(AccessRuleConfigAction action)
    {
        var firstMembers = Enumerable.Range(0, 25).Select(_ => Guid.NewGuid()).ToArray();
        var secondMembers = Enumerable.Range(0, 26).Select(_ => Guid.NewGuid()).ToArray();
        var handler = new CapturingHandler
        {
            GetResponseBody = request => JsonSerializer.Serialize(new
            {
                Members = Unescape(request).Contains(GroupId.ToString(), StringComparison.Ordinal)
                    ? firstMembers : secondMembers
            })
        };
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(handler),
                new AccessRuleConfigRequest(action, RuleId, GroupIds: [GroupId, SecondGroupId])));
        Assert.Equal("group_ids", error.ParamName);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    [Theory]
    [InlineData(AccessRuleConfigAction.AddCardholderGroup, false)]
    [InlineData(AccessRuleConfigAction.RemoveCardholderGroup, false)]
    [InlineData(AccessRuleConfigAction.AddCardholderGroup, true)]
    [InlineData(AccessRuleConfigAction.RemoveCardholderGroup, true)]
    public async Task GroupActions_FiftyMembersAndNonMutationResult_KeepFullBudgetAndResultOrder(
        AccessRuleConfigAction action, bool failedGroupRead)
    {
        var members = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToArray();
        var handler = new CapturingHandler
        {
            GetResponseBody = request => JsonSerializer.Serialize(new
            {
                Members = Unescape(request).Contains($"entity={GroupId},Members", StringComparison.Ordinal)
                    ? members : Array.Empty<Guid>()
            }),
            GetStatusCode = request => failedGroupRead &&
                Unescape(request).Contains($"entity={SecondGroupId},Members", StringComparison.Ordinal)
                ? HttpStatusCode.InternalServerError : HttpStatusCode.OK
        };

        var result = await AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new AccessRuleConfigRequest(action, RuleId, GroupIds: [GroupId, SecondGroupId]));

        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(51, batch.RequestedCount);
        Assert.Equal(failedGroupRead ? 50 : 51, batch.SuccessCount);
        Assert.Equal(failedGroupRead ? 1 : 0, batch.FailureCount);
        Assert.Equal(members.Select(id => (Guid?)id), batch.Results.Take(50).Select(item => item.Guid));
        Assert.Equal(Enumerable.Range(0, 51), batch.Results.Select(item => item.Index));
        var placeholder = batch.Results[50];
        Assert.Equal(!failedGroupRead, placeholder.Success);
        if (failedGroupRead)
            Assert.Contains("Failed to get members", placeholder.Error, StringComparison.Ordinal);
        else
        {
            Assert.Equal(SecondGroupId, placeholder.Guid);
            Assert.Contains("Group has no members", placeholder.Warning, StringComparison.Ordinal);
        }
        Assert.Equal(50, handler.Requests.Count(request => request.Method == HttpMethod.Post));
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public Func<HttpRequestMessage, string>? GetResponseBody { get; set; }
        public Func<HttpRequestMessage, HttpStatusCode>? GetStatusCode { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var uri = Unescape(request);
            var entityId = new[] { RuleId, GroupId, SecondGroupId, CardholderId, SecondCardholderId, ThirdCardholderId, ScheduleId }
                .FirstOrDefault(id => uri.Contains($"entity={id},Guid,EntityType", StringComparison.Ordinal));
            var entityType = entityId == RuleId ? "AccessRule" : entityId == ScheduleId ? "Schedule" :
                entityId == GroupId || entityId == SecondGroupId ? "CardholderGroup" : "Cardholder";
            var body = request.Method != HttpMethod.Get
                ? "{}"
                : entityId != Guid.Empty
                    ? JsonSerializer.Serialize(new { Guid = entityId, EntityType = entityType })
                    : GetResponseBody?.Invoke(request) ?? $"{{\"Members\":[\"{CardholderId}\"]}}";
            return Task.FromResult(new HttpResponseMessage(GetStatusCode?.Invoke(request) ?? HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        }
    }

    // ── Validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData(AccessRuleConfigAction.AddCardholder)]
    [InlineData(AccessRuleConfigAction.RemoveCardholder)]
    [InlineData(AccessRuleConfigAction.IsMember)]
    public async Task CardholderActions_MissingCardholderIds_ThrowsArgumentException(
        AccessRuleConfigAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(),
                new AccessRuleConfigRequest(action, RuleId)));

        Assert.Contains("cardholder_ids", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AccessRuleConfigAction.AddCardholderGroup)]
    [InlineData(AccessRuleConfigAction.RemoveCardholderGroup)]
    public async Task GroupActions_MissingGroupIds_ThrowsArgumentException(
        AccessRuleConfigAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(),
                new AccessRuleConfigRequest(action, RuleId)));

        Assert.Contains("group_ids", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetSchedule_MissingScheduleId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(),
                new AccessRuleConfigRequest(AccessRuleConfigAction.SetSchedule, RuleId)));

        Assert.Contains("schedule_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetIsDenying_MissingValue_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(),
                new AccessRuleConfigRequest(AccessRuleConfigAction.SetIsDenying, RuleId)));

        Assert.Contains("is_denying_rule", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownAction_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(),
                new AccessRuleConfigRequest((AccessRuleConfigAction)999, RuleId)));

        Assert.Contains("Unknown action", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Web SDK route and query construction ────────────────────────

    [Theory]
    [InlineData(AccessRuleConfigAction.GetCardholders, "Cardholders")]
    [InlineData(AccessRuleConfigAction.GetAccessPoints, "RelatedAccessPoints")]
    [InlineData(AccessRuleConfigAction.GetSchedule, "Schedule")]
    [InlineData(AccessRuleConfigAction.GetIsDenying, "IsDenyingRule")]
    public async Task ReadActions_UseExpectedWebSdkFieldQuery(
        AccessRuleConfigAction action, string field)
    {
        var handler = new CapturingHandler();

        var result = await AccessRuleConfigurationTool.Execute(
            TestHelpers.FakeManager(handler), new AccessRuleConfigRequest(action, RuleId));

        Assert.NotNull(result);
        var request = Assert.Single(handler.Requests, observed => !Unescape(observed).EndsWith(",Guid,EntityType", StringComparison.Ordinal));
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains($"entity={RuleId},{field}", Unescape(request), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AccessRuleConfigAction.AddCardholder, "AddCardholders")]
    [InlineData(AccessRuleConfigAction.RemoveCardholder, "RemoveCardholders")]
    public async Task CardholderMutationActions_UseCollectionInitializationQuery(
        AccessRuleConfigAction action, string method)
    {
        var handler = new CapturingHandler();

        var result = await AccessRuleConfigurationTool.Execute(
            TestHelpers.FakeManager(handler),
            new AccessRuleConfigRequest(action, RuleId,
                CardholderIds: [CardholderId, SecondCardholderId]));

        Assert.NotNull(result);
        var request = Assert.Single(handler.Requests, observed => !Unescape(observed).EndsWith(",Guid,EntityType", StringComparison.Ordinal));
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains(
            $"entity={RuleId},{method}({{{CardholderId},{SecondCardholderId}}})",
            Unescape(request), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClearCardholders_UsesParameterlessWebSdkMethod()
    {
        var handler = new CapturingHandler();

        await AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new AccessRuleConfigRequest(AccessRuleConfigAction.ClearCardholders, RuleId));

        var request = Assert.Single(handler.Requests, observed => !Unescape(observed).EndsWith(",Guid,EntityType", StringComparison.Ordinal));
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains($"entity={RuleId},ClearCardholders()", Unescape(request), StringComparison.Ordinal);
    }

    [Fact]
    public async Task IsMember_SingleCardholder_UsesFieldMethodQuery()
    {
        var handler = new CapturingHandler();

        await AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new AccessRuleConfigRequest(AccessRuleConfigAction.IsMember, RuleId,
                CardholderIds: [CardholderId]));

        var request = Assert.Single(handler.Requests, observed => !Unescape(observed).EndsWith(",Guid,EntityType", StringComparison.Ordinal));
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains($"entity={RuleId},IsMember({CardholderId})",
            Unescape(request), StringComparison.Ordinal);
    }

    [Fact]
    public async Task IsMember_MultipleCardholders_UsesOneQueryPerCardholder()
    {
        var handler = new CapturingHandler();

        await AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new AccessRuleConfigRequest(AccessRuleConfigAction.IsMember, RuleId,
                CardholderIds: [CardholderId, SecondCardholderId]));

        Assert.Equal(5, handler.Requests.Count);
        Assert.Contains(handler.Requests, request =>
            Unescape(request).Contains($"entity={RuleId},IsMember({CardholderId})", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, request =>
            Unescape(request).Contains($"entity={RuleId},IsMember({SecondCardholderId})", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SetSchedule_UsesScheduleAssignmentQuery()
    {
        var handler = new CapturingHandler();

        await AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new AccessRuleConfigRequest(AccessRuleConfigAction.SetSchedule, RuleId,
                ScheduleId: ScheduleId));

        var request = Assert.Single(handler.Requests, observed => !Unescape(observed).EndsWith(",Guid,EntityType", StringComparison.Ordinal));
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains($"entity={RuleId},Schedule={ScheduleId}", Unescape(request), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetIsDenying_UsesBooleanAssignmentQuery(bool isDenying)
    {
        var handler = new CapturingHandler();

        await AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new AccessRuleConfigRequest(AccessRuleConfigAction.SetIsDenying, RuleId,
                IsDenyingRule: isDenying));

        var request = Assert.Single(handler.Requests, observed => !Unescape(observed).EndsWith(",Guid,EntityType", StringComparison.Ordinal));
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains($"entity={RuleId},IsDenyingRule={isDenying.ToString().ToLowerInvariant()}",
            Unescape(request), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AccessRuleConfigAction.AddCardholderGroup, "AddCardholders")]
    [InlineData(AccessRuleConfigAction.RemoveCardholderGroup, "RemoveCardholders")]
    public async Task GroupActions_ResolveMembersAndInvokeCardholderMethod(
        AccessRuleConfigAction action, string method)
    {
        var handler = new CapturingHandler();

        var result = await AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new AccessRuleConfigRequest(action, RuleId, GroupIds: [GroupId]));

        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(1, batch.RequestedCount);
        Assert.Equal(1, batch.SuccessCount);
        Assert.Equal(0, batch.FailureCount);
        Assert.Equal("{}", batch.Results[0].Response);
        Assert.Equal(4, handler.Requests.Count);
        Assert.Contains(handler.Requests, request =>
            Unescape(request).Contains($"entity={GroupId},Guid,EntityType", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, request =>
            Unescape(request).Contains($"entity={GroupId},Members", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, request =>
            Unescape(request).Contains($"entity={RuleId},{method}({CardholderId})", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(AccessRuleConfigAction.AddCardholderGroup, "AddCardholders")]
    [InlineData(AccessRuleConfigAction.RemoveCardholderGroup, "RemoveCardholders")]
    public async Task GroupActions_DeduplicateRepeatedGroupsAndOverlappingMembers(
        AccessRuleConfigAction action, string method)
    {
        var handler = new CapturingHandler
        {
            GetResponseBody = request =>
            {
                var uri = Unescape(request);
                var members = uri.Contains($"entity={GroupId},Members", StringComparison.Ordinal)
                    ? new[] { CardholderId, CardholderId, SecondCardholderId }
                    : new[] { SecondCardholderId, ThirdCardholderId };
                return JsonSerializer.Serialize(new { Members = members });
            }
        };

        var result = await AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new AccessRuleConfigRequest(action, RuleId,
                GroupIds: [GroupId, GroupId, SecondGroupId, SecondGroupId]));

        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(3, batch.RequestedCount);
        Assert.Equal(3, batch.SuccessCount);
        Assert.Equal(0, batch.FailureCount);
        Assert.Equal(
            new Guid?[] { CardholderId, SecondCardholderId, ThirdCardholderId },
            batch.Results.Select(item => item.Guid).ToArray());
        Assert.Equal(2, handler.Requests.Count(request =>
            request.Method == HttpMethod.Get && Unescape(request).Contains(",Members", StringComparison.Ordinal)));
        Assert.Equal(3, handler.Requests.Count(request =>
            request.Method == HttpMethod.Post && Unescape(request).Contains($"entity={RuleId},{method}(", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(AccessRuleConfigAction.AddCardholderGroup, "AddCardholders")]
    [InlineData(AccessRuleConfigAction.RemoveCardholderGroup, "RemoveCardholders")]
    public async Task GroupActions_KeepOneFailureResultForOverlappingMember(
        AccessRuleConfigAction action, string method)
    {
        var handler = new CapturingHandler
        {
            GetResponseBody = request =>
            {
                var uri = Unescape(request);
                var members = uri.Contains($"entity={GroupId},Members", StringComparison.Ordinal)
                    ? new[] { CardholderId, SecondCardholderId }
                    : new[] { SecondCardholderId, ThirdCardholderId };
                return JsonSerializer.Serialize(new { Members = members });
            },
            GetStatusCode = request =>
            {
                var uri = Unescape(request);
                return request.Method == HttpMethod.Post &&
                    uri.Contains($"entity={RuleId},{method}({SecondCardholderId})", StringComparison.Ordinal)
                    ? HttpStatusCode.InternalServerError
                    : HttpStatusCode.OK;
            }
        };

        var result = await AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new AccessRuleConfigRequest(action, RuleId,
                GroupIds: [GroupId, SecondGroupId]));

        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(3, batch.RequestedCount);
        Assert.Equal(2, batch.SuccessCount);
        Assert.Equal(1, batch.FailureCount);
        var failedResult = Assert.Single(batch.Results, item => item.Guid == SecondCardholderId);
        Assert.False(failedResult.Success);
        Assert.Contains("HTTP 500", failedResult.Error, StringComparison.Ordinal);
        Assert.Equal(3, handler.Requests.Count(request =>
            request.Method == HttpMethod.Post && Unescape(request).Contains($"entity={RuleId},{method}(", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(AccessRuleConfigAction.AddCardholderGroup)]
    [InlineData(AccessRuleConfigAction.RemoveCardholderGroup)]
    public async Task GroupActions_WrongEntityType_ReturnsFailureBeforeReadingMembers(
        AccessRuleConfigAction action)
    {
        var userGroupId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(userGroupId, "UserGroup");
        handler.AddEntity(RuleId, "AccessRule");

        var result = await AccessRuleConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new AccessRuleConfigRequest(action, RuleId, GroupIds: [userGroupId]));

        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(1, batch.RequestedCount);
        Assert.Equal(0, batch.SuccessCount);
        Assert.Equal(1, batch.FailureCount);
        Assert.Contains("must identify CardholderGroup", batch.Results[0].Error, StringComparison.Ordinal);
        Assert.Contains("UserGroup", batch.Results[0].Error, StringComparison.Ordinal);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, observed => Assert.Equal(HttpMethod.Get, observed.Method));
    }

    private static string Unescape(HttpRequestMessage request)
        => Uri.UnescapeDataString(request.RequestUri!.ToString());
}
