using Genetec.Mcp.SecurityCenter.Tools;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools;

public class EntityNameSanitizerTests
{
    [Fact]
    public void Sanitize_ReplacesAmpersandAndSlash()
    {
        var input = "Break Room & Cafeteria/West";

        var output = EntityNameSanitizer.Sanitize(input);

        Assert.Equal("Break Room - Cafeteria-West", output);
    }

    [Fact]
    public void Sanitize_RemovesNonAsciiCharacters()
    {
        var input = "Réception ×2";

        var output = EntityNameSanitizer.Sanitize(input);

        Assert.Equal("Rception 2", output);
    }
}
