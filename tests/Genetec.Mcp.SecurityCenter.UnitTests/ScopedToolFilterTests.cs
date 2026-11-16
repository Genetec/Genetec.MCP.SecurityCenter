using Genetec.Mcp.SecurityCenter;

namespace Genetec.Mcp.SecurityCenter.UnitTests;

public class ScopedToolFilterTests
{
    [Theory]
    [InlineData("Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance.CameraGetTool", ToolScope.VideoSurveillance)]
    [InlineData("Genetec.Mcp.SecurityCenter.Tools.AccessControl.CardholderCreateTool", ToolScope.AccessControl)]
    [InlineData("Genetec.Mcp.SecurityCenter.Tools.Lpr.LprHotlistTool", ToolScope.Lpr)]
    [InlineData("Genetec.Mcp.SecurityCenter.Tools.Events.EventSubscribeTool", ToolScope.Events)]
    [InlineData("Genetec.Mcp.SecurityCenter.Tools.Administration.UserCreateTool", ToolScope.Administration)]
    [InlineData("Genetec.Mcp.SecurityCenter.Tools.Utilities.EntitySearchTool", ToolScope.Utilities)]
    public void DeriveScope_ReturnsCorrectScope(string typeName, ToolScope expectedScope)
    {
        var result = ScopedToolFilter.DeriveScope(typeName);
        Assert.Equal(expectedScope, result);
    }

    [Fact]
    public void DeriveScope_UnknownNamespace_ReturnsNull()
    {
        var result = ScopedToolFilter.DeriveScope("Genetec.Mcp.SecurityCenter.Health.SomeType");
        Assert.Null(result);
    }

    [Theory]
    [InlineData(new[] { ToolScope.VideoSurveillance, ToolScope.AccessControl }, ToolScope.VideoSurveillance, true)]
    [InlineData(new[] { ToolScope.VideoSurveillance, ToolScope.AccessControl }, ToolScope.Lpr, false)]
    [InlineData(new ToolScope[] { }, ToolScope.VideoSurveillance, false)]
    public void IsScopeAllowed_ReturnsExpected(ToolScope[] grantedScopes, ToolScope toolScope, bool expected)
    {
        var granted = grantedScopes.ToHashSet();
        var result = ScopedToolFilter.IsScopeAllowed(granted, toolScope);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildToolScopeMap_MapsKnownToolNames()
    {
        var assembly = typeof(Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance.CameraGetTool).Assembly;

        var map = ScopedToolFilter.BuildToolScopeMap(assembly);

        Assert.False(map.ContainsKey("camera_management"));
        Assert.Equal(ToolScope.VideoSurveillance, map["camera_get"]);
        Assert.True(map.ContainsKey("cardholder_create"));
        Assert.Equal(ToolScope.AccessControl, map["cardholder_create"]);
    }
}
