using System.Text.Json;
using System.Text.Json.Serialization;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Client;

public class EntityExistsResponseTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Exists\":false}")]
    [InlineData("{\"Value\":null}")]
    [InlineData("{\"Value\":\"false\"}")]
    public void InvalidResponse_CannotBeInterpretedAsEntityMissing(string json)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EntityExistsResponse>(json));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExplicitBoolean_PreservesExistence(bool exists)
    {
        var json = JsonSerializer.Serialize(new { Value = exists });
        var response = JsonSerializer.Deserialize<EntityExistsResponse>(json);
        Assert.NotNull(response);
        Assert.Equal(exists, response.Value);
    }

    private sealed record EntityExistsResponse([property: JsonRequired] bool Value);
}
