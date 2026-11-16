using Genetec.Mcp.SecurityCenter.Authentication;
using System.Net;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Configuration;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Client;

public class SecurityCenterRestClientDoorSideTests
{
    private static readonly Guid DoorId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid AccessPointId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ArgumentId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public static IEnumerable<object[]> DoorSideMethods()
    {
        yield return ["Alpha", DoorSidePath.DoorSideIn, DoorSideMethod.AddCardholder, "DoorSideIn.AddCardholder"];
        yield return ["Alpha", DoorSidePath.DoorSideIn, DoorSideMethod.RemoveCardholder, "DoorSideIn.RemoveCardholder"];
        yield return ["Alpha", DoorSidePath.DoorSideIn, DoorSideMethod.AddCardholderGroup, "DoorSideIn.AddCardholderGroup"];
        yield return ["Alpha", DoorSidePath.DoorSideIn, DoorSideMethod.RemoveCardholderGroup, "DoorSideIn.RemoveCardholderGroup"];
        yield return ["Alpha", DoorSidePath.DoorSideIn, DoorSideMethod.SetDevice, "DoorSideIn.Reader.Device"];
        yield return ["Omega", DoorSidePath.DoorSideOut, DoorSideMethod.AddCardholder, "DoorSideOut.AddCardholder"];
        yield return ["Omega", DoorSidePath.DoorSideOut, DoorSideMethod.RemoveCardholder, "DoorSideOut.RemoveCardholder"];
        yield return ["Omega", DoorSidePath.DoorSideOut, DoorSideMethod.AddCardholderGroup, "DoorSideOut.AddCardholderGroup"];
        yield return ["Omega", DoorSidePath.DoorSideOut, DoorSideMethod.RemoveCardholderGroup, "DoorSideOut.RemoveCardholderGroup"];
        yield return ["Omega", DoorSidePath.DoorSideOut, DoorSideMethod.SetDevice, "DoorSideOut.Reader.Device"];
    }

    [Theory]
    [MemberData(nameof(DoorSideMethods))]
    public async Task ExecuteDoorSideMethod_UsesExpectedWebSdkPath(
        string wireSide,
        DoorSidePath expectedSide,
        DoorSideMethod method,
        string expectedPath)
    {
        var requests = new List<HttpRequestMessage>();
        var handler = new CallbackHandler(request =>
        {
            requests.Add(request);
            if (request.Method == HttpMethod.Get)
            {
                return JsonResponse(MetadataOrAccessPoint(request, wireSide, AccessPointType.CardReader, method));
            }

            return JsonResponse("{}");
        });

        var client = new SecurityCenterRestClient(CreateSession(), new HttpClient(handler));

        await client.ExecuteDoorSideMethod(DoorId, AccessPointId, method, ArgumentId);

        var postRequest = Assert.Single(requests.Where(request => request.Method == HttpMethod.Post));
        var query = Uri.UnescapeDataString(postRequest.RequestUri!.Query);
        Assert.Contains($"entity={DoorId}", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedPath, query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ArgumentId.ToString(), query, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expectedSide, Enum.Parse<DoorSidePath>(expectedPath.Split('.')[0]));
    }

    [Theory]
    [InlineData(AccessPointType.CardReader, DoorSideComponent.Reader)]
    [InlineData(AccessPointType.Rex, DoorSideComponent.Rex)]
    [InlineData(AccessPointType.EntrySensor, DoorSideComponent.EntrySensor)]
    public async Task SetDevice_SupportedAccessPointTypes_UseMatchingComponent(
        AccessPointType accessPointType, DoorSideComponent component)
    {
        var requests = new List<HttpRequestMessage>();
        var handler = new CallbackHandler(request =>
        {
            requests.Add(request);
            return request.Method == HttpMethod.Get
                ? JsonResponse(MetadataOrAccessPoint(request, "Alpha", accessPointType, DoorSideMethod.SetDevice))
                : JsonResponse("{}");
        });

        var client = new SecurityCenterRestClient(CreateSession(), new HttpClient(handler));

        await client.ExecuteDoorSideMethod(DoorId, AccessPointId,
            DoorSideMethod.SetDevice, ArgumentId);

        var mutation = Assert.Single(requests.Where(request => request.Method == HttpMethod.Post));
        Assert.Contains($"DoorSideIn.{component}.Device", Uri.UnescapeDataString(mutation.RequestUri!.Query),
            StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<object[]> UnsupportedAccessPointTypes()
        => Enum.GetValues<AccessPointType>()
            .Where(type => type is not AccessPointType.CardReader
                and not AccessPointType.Rex
                and not AccessPointType.EntrySensor)
            .Select(type => new object[] { type });

    [Theory]
    [MemberData(nameof(UnsupportedAccessPointTypes))]
    public async Task SetDevice_UnsupportedAccessPointTypes_ThrowArgumentException(AccessPointType accessPointType)
    {
        var handler = new CallbackHandler(request => request.Method == HttpMethod.Get
            ? JsonResponse(MetadataOrAccessPoint(request, "Alpha", accessPointType, DoorSideMethod.SetDevice))
            : JsonResponse("{}"));
        var client = new SecurityCenterRestClient(CreateSession(), new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ExecuteDoorSideMethod(DoorId, AccessPointId,
                DoorSideMethod.SetDevice, ArgumentId));

        Assert.Contains(accessPointType.ToString(), ex.Message, StringComparison.Ordinal);
    }

    private static string MetadataOrAccessPoint(HttpRequestMessage request, string side, AccessPointType pointType, DoorSideMethod method)
    {
        var query = Uri.UnescapeDataString(request.RequestUri!.Query);
        if (query.Contains($"entity={DoorId}", StringComparison.Ordinal))
            return JsonSerializer.Serialize(new { Guid = DoorId, EntityType = "Door", DoorSides = new[] { AccessPointId } });
        if (query.Contains($"entity={ArgumentId}", StringComparison.Ordinal))
        {
            var type = method switch
            {
                DoorSideMethod.AddCardholder or DoorSideMethod.RemoveCardholder => "Cardholder",
                DoorSideMethod.AddCardholderGroup or DoorSideMethod.RemoveCardholderGroup => "CardholderGroup",
                _ => "Device"
            };
            return JsonSerializer.Serialize(new { Guid = ArgumentId, EntityType = type });
        }
        return JsonSerializer.Serialize(new
        {
            Guid = AccessPointId, EntityType = "AccessPoint", Door = DoorId,
            AccessPointSide = side, AccessPointType = pointType.ToString()
        });
    }

    private static SecurityCenterSession CreateSession() =>
        new(new ScConnectionContext
        {
            Host = "localhost",
            Port = 4590,
            User = "admin",
            Password = "password",
        });

    private static HttpResponseMessage JsonResponse(string content) =>
        new(HttpStatusCode.OK) { Content = new StringContent(content) };

    private sealed class CallbackHandler(Func<HttpRequestMessage, HttpResponseMessage> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(callback(request));
    }
}
