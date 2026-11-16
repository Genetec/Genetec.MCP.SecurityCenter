using Genetec.Mcp.SecurityCenter.Authentication;
using System.Net;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Configuration;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;
using Genetec.Mcp.SecurityCenter.Tools.Events;
using Microsoft.AspNetCore.WebUtilities;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Client;

public class SecurityCenterRestClientTests
{
    [Fact]
    public void BuildCustomCredentialFormat_IncludesFieldDictionary()
    {
        var formatId = Guid.NewGuid().ToString();
        var format = SecurityCenterRestClient.BuildCustomCredentialFormat(formatId,
            new Dictionary<string, string> { ["Facility"] = "12", ["Card"] = "345" });
        Assert.Equal($"CustomCredentialFormat({formatId},{{{{Facility,12}},{{Card,345}}}})", format);
    }

    [Fact]
    public void BuildCustomCredentialFormat_WithoutFields_UsesEmptyDictionary()
    {
        var formatId = Guid.NewGuid().ToString();
        Assert.Equal($"CustomCredentialFormat({formatId},{{}})",
            SecurityCenterRestClient.BuildCustomCredentialFormat(formatId, null));
    }
    private static SecurityCenterSession CreateSession() =>
        new(new ScConnectionContext
        {
            Host = "localhost",
            Port = 4590,
            User = "admin",
            Password = "password",
        });

    [Fact]
    public void Constructor_SetsSessionBaseUrl()
    {
        var session = CreateSession();
        var client = new SecurityCenterRestClient(session, new HttpClient());
        Assert.Contains("localhost", client.Session.BaseUrl);
    }

    // ── EscapeQueryValue ────────────────────────────────────────────

    [Theory]
    [InlineData("simple", "simple")]
    [InlineData("with,comma", @"with\,comma")]
    [InlineData(@"with\backslash", @"with\\backslash")]
    [InlineData("with@at", @"with\@at")]
    [InlineData("with(paren)", @"with\(paren\)")]
    [InlineData("combined,(test)", @"combined\,\(test\)")]
    public void EscapeQueryValue_EscapesSpecialChars(string input, string expected)
    {
        Assert.Equal(expected, SecurityCenterRestClient.EscapeQueryValue(input));
    }

    [Fact]
    public void EscapeQueryValue_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SecurityCenterRestClient.EscapeQueryValue(string.Empty));
    }

    // ── TryGetErrorMessage ──────────────────────────────────────────

    [Theory]
    [InlineData("""{"error":true,"message":"Not found"}""", true, "Not found")]
    [InlineData("""{"error":true}""", true, null)]
    [InlineData("""{"error":false,"message":"ok"}""", false, null)]
    [InlineData("""{"data":"ok"}""", false, null)]
    [InlineData("not json", false, null)]
    [InlineData("", false, null)]
    public void TryGetErrorMessage_DetectsErrors(
        string json, bool expectedResult, string? expectedMessage)
    {
        var result = SecurityCenterRestClient.TryGetErrorMessage(json, out var message);
        Assert.Equal(expectedResult, result);
        if (expectedMessage is not null)
            Assert.Equal(expectedMessage, message);
    }

    // ── StripEntityNoise ────────────────────────────────────────────

    [Fact]
    public void StripEntityNoise_RemovesPrivilegesFields()
    {
        var json = """{"Name":"Test","AccessPrivileges":["read","write"],"Data":"value"}""";
        var result = SecurityCenterRestClient.StripEntityNoise(json);
        Assert.DoesNotContain("Privileges", result);
        Assert.Contains("Data", result);
        Assert.Contains("Name", result);
    }

    [Fact]
    public void StripEntityNoise_NestedPrivileges_AlsoRemoved()
    {
        var json = """{"Name":"Test","Sub":{"ViewPrivileges":["x"],"Field":"keep"}}""";
        var result = SecurityCenterRestClient.StripEntityNoise(json);
        Assert.DoesNotContain("Privileges", result);
        Assert.Contains("Field", result);
    }

    [Fact]
    public void StripEntityNoise_InvalidJson_ReturnsOriginal()
    {
        var input = "not valid json";
        Assert.Equal(input, SecurityCenterRestClient.StripEntityNoise(input));
    }

    [Fact]
    public void StripEntityNoise_NonObjectRoot_ReturnsOriginal()
    {
        var input = """["a","b"]""";
        Assert.Equal(input, SecurityCenterRestClient.StripEntityNoise(input));
    }

    // ── ListEntities ReturnFields fallback ──────────────────────────

    [Fact]
    public async Task ListEntities_ReturnFieldsRejectedByServer_RetriesWithoutReturnFields()
    {
        var capturedUrls = new List<string>();

        var handler = new CallbackHandler(req =>
        {
            capturedUrls.Add(req.RequestUri!.Query);

            if (req.RequestUri.Query.Contains("ReturnFields"))
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(
                        """{"error":true,"message":"Could not find property 'ReturnFields' from filter: 'ReturnFields@Name'"}""")
                };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[]""")
            };
        });

        var client = new SecurityCenterRestClient(CreateSession(), new HttpClient(handler));
        var result = await client.ListEntities("Camera", returnFields: ["Name"]);

        Assert.Equal(2, capturedUrls.Count);
        Assert.Contains("ReturnFields", capturedUrls[0]);
        Assert.DoesNotContain("ReturnFields", capturedUrls[1]);
        Assert.Equal("[]", result);
    }

    [Fact]
    public async Task ListEntities_ReturnFieldsSupportedByServer_DoesNotRetry()
    {
        var callCount = 0;

        var handler = new CallbackHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[]""")
            };
        });

        var client = new SecurityCenterRestClient(CreateSession(), new HttpClient(handler));
        await client.ListEntities("Camera", returnFields: ["Name"]);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task BatchMutationHelpers_PreserveSdkResponses()
    {
        const string sdkResponse = "{\"operation\":\"completed\"}";
        var handler = new CallbackHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sdkResponse)
        });
        var client = new SecurityCenterRestClient(CreateSession(), new HttpClient(handler));
        const string entityId = "11111111-2222-3333-4444-555555555555";

        var update = await client.UpdateEntitiesBatch(
            new List<(string Id, string Body)> { (entityId, "{\"Name\":\"Updated\"}") });
        var delete = await client.DeleteEntitiesBatch([Guid.Parse(entityId)]);
        var method = await client.ExecuteEntityMethodBatch("Enable", [(Guid.Parse(entityId), (string?)null)]);
        var methodArguments = await client.ExecuteEntityMethodBatchOnSingleEntity(Guid.Parse(entityId), "SetMode", ["Online"]);

        AssertBatchResponse(update, sdkResponse);
        AssertBatchResponse(delete, sdkResponse);
        Assert.Equal(sdkResponse, Assert.Single(method.Results).Response);
        Assert.Equal(1, method.SuccessCount);
        Assert.Equal(sdkResponse, Assert.Single(methodArguments.Results).Response);
        Assert.Equal("Online", Assert.Single(methodArguments.Results).Arg);
        Assert.Equal(1, methodArguments.SuccessCount);
    }

    [Fact]
    public void MethodBatch_ProductionSerializationPreservesEnvelopeAndOmitsMissingFields()
    {
        var result = new BatchMethodResult("batch", 2, 1, 1,
        [
            new(0, true, Id: Guid.Parse("00000000-0000-0000-0000-000000000001"), Response: "{\"Status\":\"Ok\"}"),
            new(1, false, Id: Guid.Parse("00000000-0000-0000-0000-000000000002"), Error: "Wrong entity type.")
        ]);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result, NumericStringConverter.ToolOptions));
        var root = document.RootElement;
        Assert.Equal(2, root.GetProperty("requested_count").GetInt32());
        Assert.Equal(1, root.GetProperty("success_count").GetInt32());
        Assert.Equal(1, root.GetProperty("failure_count").GetInt32());
        var results = root.GetProperty("results");
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), results[0].GetProperty("id").GetGuid());
        Assert.Equal("{\"Status\":\"Ok\"}", results[0].GetProperty("response").GetString());
        Assert.False(results[0].TryGetProperty("error", out _));
        Assert.Equal("Wrong entity type.", results[1].GetProperty("error").GetString());
        Assert.False(results[1].TryGetProperty("response", out _));
        Assert.False(root.TryGetProperty("method", out _));
        Assert.False(results[0].TryGetProperty("arg", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteEntity_GuidUsesCanonicalEntityPath(bool typedResponse)
    {
        var id = Guid.Parse("{ABCDEF01-2345-6789-ABCD-EF0123456789}");
        var handler = new CallbackHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"Status\":\"Ok\"}")
        });
        var client = new SecurityCenterRestClient(CreateSession(), new HttpClient(handler));

        if (typedResponse)
            Assert.Equal("Ok", (await client.DeleteEntity<StatusResult>(id)).Status);
        else
            Assert.Equal("{\"Status\":\"Ok\"}", await client.DeleteEntity(id));

        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
        Assert.Equal("/WebSdk/entity/abcdef01-2345-6789-abcd-ef0123456789",
            handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Equal("", handler.LastRequest.RequestUri.Query);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteEntitiesBatch_PreservesCanonicalIdsAndIndividualHttpFailures(bool failedFirst)
    {
        var validId = Guid.Parse("ABCDEF01-2345-6789-ABCD-EF0123456789");
        var deniedId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var requests = new List<(HttpMethod Method, Uri Uri)>();
        var handler = new CallbackHandler(request =>
        {
            requests.Add((request.Method, request.RequestUri!));
            var denied = request.RequestUri!.AbsolutePath.EndsWith(deniedId.ToString(), StringComparison.Ordinal);
            return new HttpResponseMessage(denied ? HttpStatusCode.Forbidden : HttpStatusCode.OK)
            {
                Content = new StringContent(denied ? "{\"error\":true,\"message\":\"Access denied\"}" : "{\"Status\":\"Ok\"}")
            };
        });
        var client = new SecurityCenterRestClient(CreateSession(), new HttpClient(handler));
        var ids = failedFirst ? new[] { deniedId, validId } : new[] { validId, deniedId };

        using var response = JsonDocument.Parse(await client.DeleteEntitiesBatch(ids));

        var batch = response.RootElement;
        Assert.Equal(2, batch.GetProperty("requested_count").GetInt32());
        Assert.Equal(1, batch.GetProperty("success_count").GetInt32());
        Assert.Equal(1, batch.GetProperty("failure_count").GetInt32());
        var results = batch.GetProperty("results");
        for (var index = 0; index < ids.Length; index++)
        {
            Assert.Equal(index, results[index].GetProperty("index").GetInt32());
            Assert.Equal(ids[index], results[index].GetProperty("id").GetGuid());
            Assert.Equal(ids[index] == validId, results[index].GetProperty("success").GetBoolean());
            if (ids[index] == validId)
                Assert.Equal("{\"Status\":\"Ok\"}", results[index].GetProperty("response").GetString());
            else
                Assert.Contains("Access denied", results[index].GetProperty("error").GetString());
        }
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request =>
        {
            Assert.Equal(HttpMethod.Delete, request.Method);
            Assert.Contains(request.Uri.AbsolutePath, ids.Select(id => $"/WebSdk/entity/{id:D}"));
            Assert.Empty(request.Uri.Query);
        });
    }

    [Fact]
    public async Task UpdateEntity_ObjectBody_OmitsIdSelectorFromWebSdkQuery()
    {
        var handler = new CallbackHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"ok\"}")
        });
        var client = new SecurityCenterRestClient(CreateSession(), new HttpClient(handler));
        var id = "11111111-2222-3333-4444-555555555555";

        await client.UpdateEntity(id, new { Id = id, Name = "Updated" });

        var query = Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains($"entity={id},Name=Updated", query, StringComparison.Ordinal);
        Assert.DoesNotContain("Id=", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MutationQueryBuilders_EncodeCompletedExpressionsOnce()
    {
        const string literal = @"literal, (x) @a&b+c%\end";
        const string name = @"MCP, (query) @a&b+c%\name";
        var handler = new CallbackHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"Guid":"00000000-0000-0000-0000-000000000001"}""")
        });
        var client = new SecurityCenterRestClient(CreateSession(), new HttpClient(handler));
        const string entityId = "11111111-2222-3333-4444-555555555555";
        var escapedLiteral = SecurityCenterRestClient.EscapeQueryValue(literal);
        var escapedName = SecurityCenterRestClient.EscapeQueryValue(name);

        await client.CreateEntity("User", name, new { FirstName = literal });
        AssertEncodedQuery(handler.LastRequest!,
            $"entity=NewEntity(User),Name={escapedName},FirstName={escapedLiteral},Guid");

        await client.CreateEntity<EntityCreateResult>("User", name, new { FirstName = literal });
        AssertEncodedQuery(handler.LastRequest!,
            $"entity=NewEntity(User),Name={escapedName},FirstName={escapedLiteral},Guid");

        await client.CreateEntitiesBatch("User",
            [new SecurityCenterRestClient.BatchCreateItem(name, new { FirstName = literal })]);
        AssertEncodedQuery(handler.LastRequest!,
            $"entity=NewEntity(User),Name={escapedName},FirstName={escapedLiteral},Guid");

        await client.UpdateEntity(entityId, new { FirstName = literal });
        AssertEncodedQuery(handler.LastRequest!, $"entity={entityId},FirstName={escapedLiteral}");

        await client.UpdateEntitiesBatch([(entityId, (object)new { FirstName = literal })]);
        AssertEncodedQuery(handler.LastRequest!, $"entity={entityId},FirstName={escapedLiteral}");

        await client.SetEntityField(entityId, "Description", literal);
        AssertEncodedQuery(handler.LastRequest!, $"entity={entityId},Description={escapedLiteral}");

        await client.ExecuteEntityMethod(entityId, "SetExternalUniqueId", escapedLiteral);
        AssertEncodedQuery(handler.LastRequest!, $"entity={entityId},SetExternalUniqueId({escapedLiteral})");

        await client.ExecuteEntityMethodBatch("SetExternalUniqueId", [(Guid.Parse(entityId), (string?)escapedLiteral)]);
        AssertEncodedQuery(handler.LastRequest!, $"entity={entityId},SetExternalUniqueId({escapedLiteral})");

        await client.ExecuteEntityMethodBatchOnSingleEntity(Guid.Parse(entityId), "SetExternalUniqueId", [escapedLiteral]);
        AssertEncodedQuery(handler.LastRequest!, $"entity={entityId},SetExternalUniqueId({escapedLiteral})");

        await client.CreateCredentialByPath(literal, name);
        AssertEncodedQuery(handler.LastRequest!,
            $"entity=NewEntity(Credential),Format={escapedLiteral},Name={escapedName},Guid");
    }

    [Theory]
    [InlineData("report", "literal, (x) @a&b+c%\\end#tail")]
    [InlineData("report", "Lobby%2CGuidsOnly%3DTrue")]
    [InlineData("paged_report", "literal, (x) @a&b+c%\\end#tail")]
    [InlineData("paged_report", "Lobby%2CGuidsOnly%3DTrue")]
    [InlineData("alarm", "literal, (x) @a&b+c%\\end#tail")]
    [InlineData("alarm", "Lobby%2CGuidsOnly%3DTrue")]
    [InlineData("action", "literal, (x) @a&b+c%\\end#tail")]
    [InlineData("action", "Lobby%2CGuidsOnly%3DTrue")]
    public async Task QueryEndpoints_PreserveSdkExpressionAsOneQueryValue(string endpoint, string value)
    {
        var handler = new CallbackHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]")
        });
        var client = new SecurityCenterRestClient(CreateSession(), new HttpClient(handler));
        var expression = $"Name={SecurityCenterRestClient.EscapeQueryValue(value)}";
        var expected = expression;
        var path = "";
        var method = HttpMethod.Get;

        switch (endpoint)
        {
            case "report":
                path = "/WebSdk/report/EntityConfiguration";
                await client.RunReport("EntityConfiguration", expression);
                break;
            case "paged_report":
                path = "/WebSdk/report/EntityConfiguration";
                expected += ",PageSize=7,Page=2";
                await client.RunPagedReport("EntityConfiguration", expression, 7, 2);
                break;
            case "alarm":
                path = "/WebSdk/alarm";
                await client.AlarmAction(expression);
                break;
            case "action":
                path = "/WebSdk/action";
                method = HttpMethod.Post;
                await client.ExecuteAction(expression);
                break;
        }

        AssertQueryRequest(handler.LastRequest!, path, method, expected);
    }

    [Theory]
    [InlineData("MCP, (name) @a&b+c%\\end#tail")]
    [InlineData("Lobby%2CGuidsOnly%3DTrue")]
    [InlineData("Étage 東京")]
    public async Task SearchEntities_NameRetainsLiteralCharactersAndPaging(string name)
    {
        var handler = new CallbackHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]")
        });
        var client = new SecurityCenterRestClient(CreateSession(), new HttpClient(handler));

        await client.SearchEntities(name, "User", "Is", 7, 2);

        AssertQueryRequest(handler.LastRequest!, "/WebSdk/report/EntityConfiguration", HttpMethod.Get,
            $"EntityTypes@User,Name={SecurityCenterRestClient.EscapeQueryValue(name)},NameSearchMode=Is,PageSize=7,Page=2");
    }

    [Fact]
    public async Task EntityAndEventQueries_PreserveSelectorsAndSdkSyntax()
    {
        var handler = new CallbackHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });
        var client = new SecurityCenterRestClient(CreateSession(), new HttpClient(handler));
        const string id = "11111111-2222-3333-4444-555555555555";
        const string logicalId = "a&b+c%2C#tail";

        await client.GetEntityByLogicalId("Camera", logicalId);
        AssertQueryRequest(handler.LastRequest!, "/WebSdk/entity", HttpMethod.Get,
            $"entity=LogicalID(Camera,{logicalId}),Name,EntityType");

        await client.GetEntityFields(id, "Name", "LogicalId");
        AssertQueryRequest(handler.LastRequest!, "/WebSdk/entity", HttpMethod.Get, $"entity={id},Name,LogicalId");

        await client.GetEntityField(id, "Name");
        AssertQueryRequest(handler.LastRequest!, "/WebSdk/entity", HttpMethod.Get, $"entity={id},Name");

        await client.AddToCollection(id, "Users", id);
        AssertQueryRequest(handler.LastRequest!, "/WebSdk/entity", HttpMethod.Post, $"entity={id},Users@{id}");

        await client.RemoveFromCollection(id, "Users", id);
        AssertQueryRequest(handler.LastRequest!, "/WebSdk/entity", HttpMethod.Post, $"entity={id},Users-{id}");

        var eventType = Enum.GetValues<ScEventType>()[0];
        await client.SubscribeToEvents(id, [eventType]);
        AssertQueryRequest(handler.LastRequest!, "/WebSdk/events/subscribe", HttpMethod.Get, $"event({id},{eventType})");

        await client.UnsubscribeFromEvents(id, [eventType]);
        AssertQueryRequest(handler.LastRequest!, "/WebSdk/events/unsubscribe", HttpMethod.Get, $"event({id},{eventType})");
    }

    [Fact]
    public async Task ThumbnailQuery_PreservesTimestampOffsetsAndCameraRanges()
    {
        var handler = new CallbackHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]")
        });
        var client = new SecurityCenterRestClient(CreateSession(), new HttpClient(handler));
        var cameras = new[] { Guid.NewGuid(), Guid.NewGuid() };
        const string start = "2026-10-30T10:00:00+01:00";
        const string end = "2026-10-30T11:00:00+01:00";

        await client.GetThumbnails(cameras, start, end, 320, 2);

        AssertQueryRequest(handler.LastRequest!, "/WebSdk/report/Thumbnail", HttpMethod.Get,
            string.Join(",", cameras.Select(id => $"AddTimestampRange({id},{start},{end},2,320)")));
    }

    [Fact]
    public async Task CreateEntitiesBatch_EnforcesEncodedQuerySizeBeforeSending()
    {
        var handler = new CallbackHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });
        var client = new SecurityCenterRestClient(CreateSession(), new HttpClient(handler));

        var error = await Assert.ThrowsAsync<ArgumentException>(() => client.CreateEntitiesBatch("User",
            [new SecurityCenterRestClient.BatchCreateItem(new string('&', 11000))]));

        Assert.Contains("32000", error.Message);
        Assert.Null(handler.LastRequest);
    }

    private static void AssertQueryRequest(HttpRequestMessage request, string path, HttpMethod method, string expression)
    {
        Assert.Equal(method, request.Method);
        Assert.Equal(path, request.RequestUri!.AbsolutePath);
        Assert.Empty(request.RequestUri.Fragment);
        var query = QueryHelpers.ParseQuery(request.RequestUri.Query);
        Assert.Single(query);
        Assert.Equal(expression, Assert.Single(query["q"]));
    }

    [Theory]
    [InlineData("My Field (1)", "My%20Field%20%281%29")]
    [InlineData("My%20Field", "My%2520Field")]
    [InlineData("%2e%2e", "%252e%252e")]
    [InlineData("%252e%252e", "%25252e%25252e")]
    [InlineData("%2f..", "%252f..")]
    public async Task CustomFieldMethods_EscapeLiteralFieldNamesOnce(string fieldName, string escapedFieldName)
    {
        var handler = new CallbackHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });
        var client = new SecurityCenterRestClient(CreateSession(), new HttpClient(handler));
        const string entityId = "11111111-2222-3333-4444-555555555555";
        await client.GetCustomFieldValue(entityId, fieldName);
        Assert.EndsWith($"/customField/{entityId}/{escapedFieldName}", handler.LastRequest!.RequestUri!.AbsolutePath,
            StringComparison.Ordinal);

        await client.SetCustomFieldValue(entityId, fieldName, "value/one");
        Assert.EndsWith($"/customField/{entityId}/{escapedFieldName}/value%2Fone", handler.LastRequest!.RequestUri!.AbsolutePath,
            StringComparison.Ordinal);

        await client.CreateCustomField(CustomFieldEntityType.Camera, fieldName, CustomFieldDataType.Text, "default");
        Assert.EndsWith($"/customField/Camera/{escapedFieldName}/Text/default", handler.LastRequest!.RequestUri!.AbsolutePath,
            StringComparison.Ordinal);

        await client.DeleteCustomField(CustomFieldEntityType.Camera, fieldName);
        Assert.EndsWith($"/customField/Camera/{escapedFieldName}", handler.LastRequest!.RequestUri!.AbsolutePath,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CustomFieldAction.GetValue, ".")]
    [InlineData(CustomFieldAction.GetValue, "..")]
    [InlineData(CustomFieldAction.GetValue, "../Name")]
    [InlineData(CustomFieldAction.GetValue, @"..\Name")]
    [InlineData(CustomFieldAction.SetValue, ".")]
    [InlineData(CustomFieldAction.SetValue, "..")]
    [InlineData(CustomFieldAction.SetValue, "../Name")]
    [InlineData(CustomFieldAction.SetValue, @"..\Name")]
    [InlineData(CustomFieldAction.CreateField, ".")]
    [InlineData(CustomFieldAction.CreateField, "..")]
    [InlineData(CustomFieldAction.CreateField, "../Name")]
    [InlineData(CustomFieldAction.CreateField, @"..\Name")]
    [InlineData(CustomFieldAction.DeleteField, ".")]
    [InlineData(CustomFieldAction.DeleteField, "..")]
    [InlineData(CustomFieldAction.DeleteField, "../Name")]
    [InlineData(CustomFieldAction.DeleteField, @"..\Name")]
    public async Task CustomFieldMethods_RejectTraversalFieldNames(CustomFieldAction action, string fieldName)
    {
        var handler = new CallbackHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });
        var client = new SecurityCenterRestClient(CreateSession(), new HttpClient(handler));

        var error = await Assert.ThrowsAsync<ArgumentException>(() => action switch
        {
            CustomFieldAction.GetValue => client.GetCustomFieldValue("11111111-2222-3333-4444-555555555555", fieldName),
            CustomFieldAction.SetValue => client.SetCustomFieldValue("11111111-2222-3333-4444-555555555555", fieldName, "value"),
            CustomFieldAction.CreateField => client.CreateCustomField(CustomFieldEntityType.Camera, fieldName, CustomFieldDataType.Text),
            CustomFieldAction.DeleteField => client.DeleteCustomField(CustomFieldEntityType.Camera, fieldName),
            _ => throw new InvalidOperationException()
        });

        Assert.Equal("field_name", error.ParamName);
        Assert.Null(handler.LastRequest);
    }

    private static void AssertEncodedQuery(HttpRequestMessage request, string expected)
    {
        var rawQuery = request.RequestUri!.Query;
        Assert.Equal($"?q={expected}", Uri.UnescapeDataString(rawQuery));
        Assert.Contains("%26", request.RequestUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%2B", request.RequestUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%25", request.RequestUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("%2526", request.RequestUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("%2525", request.RequestUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertBatchResponse(string rawBatch, string expectedResponse)
    {
        using var document = JsonDocument.Parse(rawBatch);
        var response = document.RootElement
            .GetProperty("results")[0]
            .GetProperty("response")
            .GetString();
        Assert.Equal(expectedResponse, response);
    }

    private sealed class CallbackHandler(Func<HttpRequestMessage, HttpResponseMessage> callback) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(callback(request));
        }
    }
}
