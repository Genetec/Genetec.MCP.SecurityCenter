namespace Genetec.Mcp.SecurityCenter.Configuration;

internal static class ConfigurationBuilderExtensions
{
    internal static IConfigurationBuilder AddSecurityCenterConfiguration(
        this IConfigurationBuilder builder, string configPath, CliOptions cli)
        => builder.SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(configPath, optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .AddInMemoryCollection(cli.ToConfigOverrides());

    internal static void SetDefaultFileLogPaths(this IConfiguration configuration)
    {
        foreach (var sink in configuration.GetSection("Serilog:WriteTo").GetChildren())
        {
            if (string.Equals(sink["Name"], "File", StringComparison.OrdinalIgnoreCase)
                && sink["Args:path"] is null)
                configuration[$"{sink.Path}:Args:path"] = Path.Combine(AppContext.BaseDirectory, "logs", "mcp-server-.log");
        }
    }
}
