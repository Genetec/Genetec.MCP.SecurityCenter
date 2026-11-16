using System.Net;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools;

internal sealed class ToolTestHandler : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];
    public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory { get; init; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        if (ResponseFactory is not null)
            return Task.FromResult(ResponseFactory(request));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"Guid\":\"00000000-0000-0000-0000-000000000001\",\"Status\":\"Ok\",\"Name\":\"Test\"}")
        });
    }
}
