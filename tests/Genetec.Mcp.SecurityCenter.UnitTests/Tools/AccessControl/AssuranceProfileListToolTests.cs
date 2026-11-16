using System.Reflection;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class AssuranceProfileListToolTests
{
    [Fact]
    public void Discovery_ExposesProfileLookupWithoutBusinessInputs()
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            SearchToolsTool.Execute(new SearchToolsRequest(Query: "assurance_profile_list", Detail: ToolDetailLevel.Full)),
            NumericStringConverter.ToolOptions));
        var tool = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal("assurance_profile_list", tool.GetProperty("name").GetString());
        Assert.Empty(tool.GetProperty("parameters").EnumerateArray());
        var description = tool.GetProperty("description").GetString();
        Assert.Contains("ProfileId", description);
        Assert.Contains("Description", description);
        Assert.Contains("Mode", description);
        Assert.Contains("reader_configuration.enable_assurance_profile", description);
    }

    [Fact]
    public void Tool_IsReadOnlyAndIdempotent()
    {
        var method = typeof(AssuranceProfileListTool).GetMethod(nameof(AssuranceProfileListTool.Execute))!;
        var attribute = method.GetCustomAttribute<McpServerToolAttribute>()!;
        Assert.True(attribute.ReadOnly);
        Assert.True(attribute.Idempotent);
        Assert.False(attribute.Destructive);
        Assert.True(attribute.OpenWorld);
    }
}
