using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Client;

public sealed class CreateToolHelperTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyItems_RejectsWithAndWithoutValidation(bool validate)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CreateToolHelper.Execute(
            TestHelpers.FakeManager().GetClient(), "Zone", [], validate ? _ => Task.CompletedTask : null));
        Assert.Equal("items", error.ParamName);
    }

    [Fact]
    public async Task BatchValidationFailure_PreservesEachIndexAndContinues()
    {
        var handler = new ToolTestHandler { ResponseFactory = _ => throw new HttpRequestException("Second item reached mutation.") };
        var validated = new List<int>();
        var result = await CreateToolHelper.Execute(TestHelpers.FakeManager(handler).GetClient(), "Zone",
            [new("A"), new("B")], index =>
            {
                validated.Add(index);
                if (index == 0) throw new ArgumentException("First item rejected.", "items");
                return Task.CompletedTask;
            });

        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(new[] { 0, 1 }, validated);
        Assert.Equal(2, batch.RequestedCount);
        Assert.Equal(0, batch.SuccessCount);
        Assert.Equal(2, batch.FailureCount);
        Assert.Equal(0, batch.Results[0].Index);
        Assert.Contains("First item rejected.", batch.Results[0].Error);
        Assert.Equal(1, batch.Results[1].Index);
        Assert.Equal("Second item reached mutation.", batch.Results[1].Error);
    }

    [Fact]
    public async Task ValidationCancellation_Propagates()
    {
        var cancellation = new OperationCanceledException();
        var error = await Assert.ThrowsAsync<OperationCanceledException>(() => CreateToolHelper.Execute(
            TestHelpers.FakeManager().GetClient(), "Zone", [new("A"), new("B")], _ => throw cancellation));
        Assert.Same(cancellation, error);
    }

    [Fact]
    public async Task SingleItem_PreservesEntityTypeBodyAndInitializationBeforeMutation()
    {
        Uri? mutation = null;
        var upstream = new HttpRequestException("Mutation reached.");
        var handler = new ToolTestHandler
        {
            ResponseFactory = request =>
            {
                mutation = request.RequestUri;
                throw upstream;
            }
        };
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => CreateToolHelper.Execute(
            TestHelpers.FakeManager(handler).GetClient(), "Zone",
            [new("Role A", new { Type = "AccessManager" }, "Role", "AddServer(00000000-0000-0000-0000-000000000001)")]));
        Assert.Same(upstream, error);
        Assert.NotNull(mutation);
        var query = Uri.UnescapeDataString(mutation.Query);
        Assert.Contains("NewEntity(Role)", query);
        Assert.Contains("Type=AccessManager", query);
        Assert.Contains("AddServer(00000000-0000-0000-0000-000000000001)", query);
    }
}
