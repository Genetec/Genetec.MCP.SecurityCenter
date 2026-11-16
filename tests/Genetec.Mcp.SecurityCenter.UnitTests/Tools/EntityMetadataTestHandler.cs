using System.Net;
using System.Text.Json;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools;

internal sealed class EntityMetadataTestHandler(HttpMessageHandler? fallbackHandler = null) : HttpMessageHandler
{
    private readonly HttpMessageInvoker? _fallback = fallbackHandler is null ? null : new(fallbackHandler, disposeHandler: false);
    private readonly Dictionary<Guid, EntityRecord> _entities = [];

    public List<(HttpMethod Method, Uri Uri)> Requests { get; } = [];

    public void AddEntity(Guid id, string entityType, Guid? doorId = null, IReadOnlyList<Guid>? doorSides = null)
        => _entities[id] = new EntityRecord(id, entityType, doorId, doorSides ?? []);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        Requests.Add((request.Method, uri));
        if (_fallback is not null &&
            !(request.Method == HttpMethod.Get && FindEntity(uri) is not null &&
              Uri.UnescapeDataString(uri.Query).EndsWith(",Guid,EntityType", StringComparison.Ordinal)))
            return _fallback.SendAsync(request, cancellationToken);
        var response = request.Method == HttpMethod.Get && FindEntity(uri) is { } entity
            ? JsonSerializer.Serialize(new
            {
                Guid = entity.Id,
                EntityType = entity.EntityType,
                Door = entity.DoorId,
                DoorSides = entity.DoorSides.Select(id => id.ToString()).ToArray(),
                AccessPointSide = "Alpha",
                AccessPointType = "CardReader",
                Name = "Test entity"
            })
            : "{\"Guid\":\"00000000-0000-0000-0000-000000000001\",\"Status\":\"Ok\",\"Name\":\"Test\"}";

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(response)
        });
    }

    private EntityRecord? FindEntity(Uri uri)
    {
        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        if (path.StartsWith("/WebSdk/entity/", StringComparison.Ordinal) &&
            Guid.TryParse(path["/WebSdk/entity/".Length..], out var fullId))
            return _entities.GetValueOrDefault(fullId);

        var query = Uri.UnescapeDataString(uri.Query);
        const string selector = "?q=entity=";
        if (!query.StartsWith(selector, StringComparison.Ordinal))
            return null;
        var idText = query[selector.Length..].Split(',', 2)[0];
        return Guid.TryParse(idText, out var id) ? _entities.GetValueOrDefault(id) : null;
    }

    private sealed record EntityRecord(Guid Id, string EntityType, Guid? DoorId, IReadOnlyList<Guid> DoorSides);
}
