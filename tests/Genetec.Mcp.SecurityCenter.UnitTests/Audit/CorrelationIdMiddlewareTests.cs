using Genetec.Mcp.SecurityCenter.Audit;
using Microsoft.AspNetCore.Http;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Audit;

public class CorrelationIdMiddlewareTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("request-123", true)]
    [InlineData("bad\r\nid", false)]
    [InlineData("bad\tid", false)]
    [InlineData("bad\0id", false)]
    [InlineData("bad\u007fid", false)]
    public async Task IncomingId_UsesSafeIdOrServerTrace(string? incomingId, bool accepted)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "server-trace" };
        if (incomingId is not null)
            context.Request.Headers["X-Correlation-Id"] = incomingId;
        var nextCalled = false;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(accepted ? incomingId : context.TraceIdentifier,
            context.Response.Headers["X-Correlation-Id"].ToString());
    }

    [Theory]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public async Task IncomingId_LengthBoundaryUsesSafeIdOrServerTrace(int length, bool accepted)
    {
        await IncomingId_UsesSafeIdOrServerTrace(new string('a', length), accepted);
    }
}
