using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Client;

public sealed class DeleteToolHelperTests
{
    [Fact]
    public async Task SingleGuid_PreservesStatusResultAndCanonicalDeletePath()
    {
        var id = Guid.Parse("ABCDEF01-2345-6789-ABCD-EF0123456789");
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "Camera");

        var result = await DeleteToolHelper.Execute(CreateClient(handler), [id], "Camera");

        Assert.Equal("Ok", Assert.IsType<StatusResult>(result).Status);
        Assert.Equal(2, handler.Requests.Count);
        var deletion = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Delete);
        Assert.Equal($"/WebSdk/entity/{id:D}", deletion.Uri.AbsolutePath);
        Assert.Empty(deletion.Uri.Query);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
    }

    [Fact]
    public void Execute_ExpectedTypeIsRequiredAndNonNullable()
    {
        var parameter = typeof(DeleteToolHelper).GetMethod(nameof(DeleteToolHelper.Execute))!.GetParameters()[2];
        Assert.False(parameter.IsOptional);
        Assert.Equal(System.Reflection.NullabilityState.NotNull,
            new System.Reflection.NullabilityInfoContext().Create(parameter).ReadState);
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData(null, 2)]
    [InlineData("", 1)]
    [InlineData("", 2)]
    [InlineData(" ", 1)]
    [InlineData(" ", 2)]
    public async Task Execute_MissingExpectedTypeRejectsBeforeHttp(string? expectedType, int count)
    {
        var handler = new EntityMetadataTestHandler();
        var items = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

        var error = await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            DeleteToolHelper.Execute(CreateClient(handler), items, expectedType!));

        Assert.Equal("expected_type", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BatchGuid_WithExpectedTypePreservesEachResultAndRejectsWrongEntity(bool invalidFirst)
    {
        var cameraId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var ids = invalidFirst ? new List<Guid> { otherId, cameraId } : [cameraId, otherId];
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(cameraId, "Camera");
        handler.AddEntity(otherId, "UserGroup");

        var result = await DeleteToolHelper.Execute(CreateClient(handler), ids, "Camera");

        using var response = JsonDocument.Parse(Assert.IsType<string>(result));
        var batch = response.RootElement;
        Assert.Equal(2, batch.GetProperty("requested_count").GetInt32());
        Assert.Equal(1, batch.GetProperty("success_count").GetInt32());
        Assert.Equal(1, batch.GetProperty("failure_count").GetInt32());
        var results = batch.GetProperty("results");
        for (var index = 0; index < ids.Count; index++)
        {
            Assert.Equal(index, results[index].GetProperty("index").GetInt32());
            Assert.Equal(ids[index], results[index].GetProperty("id").GetGuid());
            Assert.Equal(ids[index] == cameraId, results[index].GetProperty("success").GetBoolean());
        }
        var deletion = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Delete);
        Assert.Equal($"/WebSdk/entity/{cameraId:D}", deletion.Uri.AbsolutePath);
    }

    private static SecurityCenterRestClient CreateClient(HttpMessageHandler handler)
        => new(new SecurityCenterSession(new ScConnectionContext
        { Host = "localhost", Port = 4590, User = "test", Password = "test" }), new HttpClient(handler));
}
