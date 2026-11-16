using System.Net;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Configuration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Client;

public class WebSdkResponseTests
{
    [Theory]
    [InlineData("Ok", false)]
    [InlineData("Partial", true)]
    [InlineData("TooManyResults", true)]
    public void Unwrap_PagedBody_PreservesColumnsAndIncompleteMetadata(string status, bool enveloped)
    {
        var response = JsonSerializer.Serialize(new
        {
            RequestId = "request-1",
            Rsp = new { Status = status, Detail = "detail", Result = new { Columns = new[] { "Guid" }, Body = new[] { new[] { "first" }, new[] { "second" } }, Total = 2 } }
        });
        using var document = JsonDocument.Parse(WebSdkResponse.Unwrap(response, 1));
        var root = document.RootElement;
        var result = enveloped ? root.GetProperty("Rsp").GetProperty("Result") : root;
        Assert.Equal(1, result.GetProperty("Body").GetArrayLength());
        Assert.Equal("first", result.GetProperty("Body")[0][0].GetString());
        Assert.Equal("Guid", result.GetProperty("Columns")[0].GetString());
        Assert.Equal(2, result.GetProperty("Total").GetInt32());
        if (enveloped)
        {
            Assert.Equal(status, root.GetProperty("Rsp").GetProperty("Status").GetString());
            Assert.Equal("detail", root.GetProperty("Rsp").GetProperty("Detail").GetString());
            Assert.Equal("request-1", root.GetProperty("RequestId").GetString());
        }
        else Assert.False(root.TryGetProperty("Rsp", out _));
    }

    [Theory]
    [InlineData("Ok")]
    [InlineData("Partial")]
    [InlineData("TooManyResults")]
    public void Unwrap_PagedArray_CapsRowsWithoutChangingTheirValues(string status)
    {
        var response = JsonSerializer.Serialize(new { Rsp = new { Status = status, Result = new[] { "first", "second" } } });
        using var document = JsonDocument.Parse(WebSdkResponse.Unwrap(response, 1));
        var result = status == "Ok" ? document.RootElement : document.RootElement.GetProperty("Rsp").GetProperty("Result");
        Assert.Equal(1, result.GetArrayLength());
        Assert.Equal("first", result[0].GetString());
    }

    [Theory]
    [InlineData("[1,2]")]
    [InlineData("{\"Body\":[[1],[2]],\"Columns\":[\"Id\"]}")]
    public void Unwrap_BareResult_CapsOnlyRows(string response)
    {
        using var document = JsonDocument.Parse(WebSdkResponse.Unwrap(response, 1));
        var rows = document.RootElement.ValueKind == JsonValueKind.Array ? document.RootElement : document.RootElement.GetProperty("Body");
        Assert.Equal(1, rows.GetArrayLength());
    }

    [Theory]
    [InlineData("Ok")]
    [InlineData("TooManyResults")]
    [InlineData("Partial")]
    public void Unwrap_PagesWithUpstreamDuplicates_PreservesOrderAndTrimsOnlyLookAhead(string status)
    {
        string[] firstPage = ["A", "A", "B"];
        string[] secondPage = ["A", "B", "C"];
        foreach (var page in new[] { firstPage, secondPage })
        {
            var response = JsonSerializer.Serialize(new { Rsp = new { Status = status, Result = page } });
            using var document = JsonDocument.Parse(WebSdkResponse.Unwrap(response, 2));
            var result = status == "Ok" ? document.RootElement : document.RootElement.GetProperty("Rsp").GetProperty("Result");
            Assert.Equal(page.Take(2), result.EnumerateArray().Select(row => row.GetString()));
            if (status != "Ok")
                Assert.Equal(status, document.RootElement.GetProperty("Rsp").GetProperty("Status").GetString());
        }
    }

    [Theory]
    [InlineData("Partial")]
    [InlineData("TooManyResults")]
    public void Unwrap_NonPagedResponse_PreservesAllRows(string status)
    {
        var response = JsonSerializer.Serialize(new { Rsp = new { Status = status, Result = new[] { 1, 2, 3 } } });
        using var document = JsonDocument.Parse(WebSdkResponse.Unwrap(response));
        Assert.Equal(3, document.RootElement.GetProperty("Rsp").GetProperty("Result").GetArrayLength());
    }

    [Fact]
    public void Unwrap_Fail_ThrowsUpstreamFailure()
    {
        var error = Assert.Throws<InvalidOperationException>(() => WebSdkResponse.Unwrap("{\"Rsp\":{\"Status\":\"Fail\",\"Result\":\"denied\"}}"));
        Assert.Contains("denied", error.Message);
    }

    [Theory]
    [InlineData("{\"Rsp\":{\"Status\":\"Partial\",\"Detail\":\"timeout\"}}", "Partial")]
    [InlineData("{\"Rsp\":{\"Status\":\"TooManyResults\",\"Result\":null,\"Detail\":\"limit\"}}", "TooManyResults")]
    public void Unwrap_IncompleteWithoutRows_PreservesEnvelope(string response, string status)
    {
        using var document = JsonDocument.Parse(WebSdkResponse.Unwrap(response, 1));
        Assert.Equal(status, document.RootElement.GetProperty("Rsp").GetProperty("Status").GetString());
        Assert.True(document.RootElement.GetProperty("Rsp").TryGetProperty("Detail", out _));
    }

    [Theory]
    [InlineData("[]", 0)]
    [InlineData("[1]", 1)]
    public void Unwrap_ShortPage_DoesNotAddRows(string response, int count)
    {
        using var document = JsonDocument.Parse(WebSdkResponse.Unwrap(response, 2));
        Assert.Equal(count, document.RootElement.GetArrayLength());
    }

    [Theory]
    [InlineData("{ \"Body\": [1], \"Columns\": [\"Id\"] }", 2)]
    [InlineData("[ 1, 2 ]", null)]
    [InlineData("{ \"Rsp\": { \"Status\": \"Partial\", \"Result\": [ 1 ] } }", 2)]
    public void Unwrap_UnchangedResponse_PreservesRawJson(string response, int? pageSize)
        => Assert.Equal(response, WebSdkResponse.Unwrap(response, pageSize));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Unwrap_InvalidCap_RejectsBeforeTrimming(int pageSize)
        => Assert.Throws<ArgumentOutOfRangeException>(() => WebSdkResponse.Unwrap("[1]", pageSize));

    [Theory]
    [InlineData("Partial")]
    [InlineData("TooManyResults")]
    public async Task TypedSend_IncompleteResponse_ThrowsInsteadOfDefaultModel(string status)
    {
        using var http = new HttpClient(new ResponseHandler(JsonSerializer.Serialize(new { Rsp = new { Status = status, Result = new { Name = "first" } } })));
        var session = CreateSession();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SecurityCenterRestClient(session, http).SendAsync<ResultModel>("entity/1", "GET"));
        Assert.Contains(status, error.Message);
    }

    [Fact]
    public async Task TypedSend_OkResponse_DeserializesResult()
    {
        using var http = new HttpClient(new ResponseHandler("{\"Rsp\":{\"Status\":\"Ok\",\"Result\":{\"Name\":\"first\"}}}"));
        var session = CreateSession();
        var result = await new SecurityCenterRestClient(session, http).SendAsync<ResultModel>("entity/1", "GET");
        Assert.Equal("first", result.Name);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(null, 2)]
    [InlineData(1, null)]
    [InlineData(null, null)]
    public async Task RunPagedReport_UsesTypedOptionalPagingAndCapsOnlyWhenRequested(int? pageSize, int? page)
    {
        var handler = new ResponseHandler("{\"Rsp\":{\"Status\":\"TooManyResults\",\"Result\":{\"Body\":[[1],[2]]}}}");
        using var http = new HttpClient(handler);
        var result = await new SecurityCenterRestClient(CreateSession(), http).RunPagedReport("UserGroupReport", "Name=first", pageSize, page);
        var query = Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.Query);
        Assert.Equal($"?q=Name=first,PageSize={pageSize ?? 50},Page={page ?? 1}", query);
        using var document = JsonDocument.Parse(result);
        Assert.Equal(pageSize ?? 2, document.RootElement.GetProperty("Rsp").GetProperty("Result").GetProperty("Body").GetArrayLength());
    }

    [Theory]
    [InlineData(0, 1, "page_size")]
    [InlineData(100001, 1, "page_size")]
    [InlineData(101, 1, "page_size")]
    [InlineData(1, 0, "page")]
    public void RunPagedReport_InvalidPaging_RejectsBeforeHttp(int pageSize, int page, string parameter)
    {
        var handler = new ResponseHandler("{}");
        using var http = new HttpClient(handler);
        var error = Assert.Throws<ArgumentException>(() => { _ = new SecurityCenterRestClient(CreateSession(), http).RunPagedReport("EntityConfiguration", "", pageSize, page); });
        Assert.Equal(parameter, error.ParamName);
        Assert.Null(handler.LastRequest);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("search")]
    [InlineData("custom")]
    public async Task PagedClientMethods_IncludeFirstPageAndUseCentralRowCap(string operation)
    {
        var handler = new ResponseHandler("{\"Rsp\":{\"Status\":\"TooManyResults\",\"Result\":[1,2]}}");
        using var http = new HttpClient(handler);
        var client = new SecurityCenterRestClient(CreateSession(), http);
        var response = operation switch
        {
            "list" => await client.ListEntities("Camera", 1, 1, ["Name"]),
            "search" => await client.SearchEntities("camera", "Camera", pageSize: 1, page: 1),
            _ => await client.SearchCredentialsByCustomFormat("format", null, 1, 1)
        };
        var query = Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.Query);
        Assert.Contains("PageSize=1,Page=1", query);
        using var document = JsonDocument.Parse(response);
        Assert.Equal(1, document.RootElement.GetProperty("Rsp").GetProperty("Result").GetArrayLength());
    }

    [Fact]
    public async Task ListEntities_ReturnFieldsFallback_PreservesPagingAndCap()
    {
        var handler = new FallbackHandler();
        using var http = new HttpClient(handler);
        var response = await new SecurityCenterRestClient(CreateSession(), http).ListEntities("Camera", 1, 1, ["Name"]);
        Assert.Equal(2, handler.Queries.Count);
        Assert.Contains("ReturnFields@Name", handler.Queries[0]);
        Assert.DoesNotContain("ReturnFields", handler.Queries[1]);
        Assert.All(handler.Queries, query => Assert.Contains("PageSize=1,Page=1", query));
        using var document = JsonDocument.Parse(response);
        Assert.Equal(1, document.RootElement.GetProperty("Rsp").GetProperty("Result").GetArrayLength());
    }


    private static SecurityCenterSession CreateSession() => new(new ScConnectionContext
    {
        Host = "localhost", Port = 4590, User = "admin", Password = "password"
    });

    private sealed record ResultModel(string Name);

    private sealed class ResponseHandler(string response) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) });
        }
    }

    private sealed class FallbackHandler : HttpMessageHandler
    {
        public List<string> Queries { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Queries.Add(Uri.UnescapeDataString(request.RequestUri!.Query));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Queries.Count == 1
                    ? "{\"Rsp\":{\"Status\":\"Fail\",\"Result\":\"ReturnFields unsupported\"}}"
                    : "{\"Rsp\":{\"Status\":\"TooManyResults\",\"Result\":[1,2]}}")
            });
        }
    }
}
