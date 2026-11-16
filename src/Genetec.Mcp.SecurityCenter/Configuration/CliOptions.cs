using System.CommandLine;
using System.Text.Json;

namespace Genetec.Mcp.SecurityCenter.Configuration;

public enum TransportMode { Stdio, Http }

public sealed class CliOptions
{
    private static readonly string ValidScopes = string.Join(", ",
        Enum.GetValues<ToolScope>().Select(s => JsonNamingPolicy.SnakeCaseLower.ConvertName(s.ToString())));

    private static readonly Option<TransportMode?> TransportOption = new("--transport")
    {
        Description = "Transport mode: 'stdio' (spawned by MCP client) or 'http' (long-running HTTP/SSE server)"
    };

    private static readonly Option<string[]> ScopesOption = new("--scopes")
    {
        Description = $"Comma- or space-separated tool scopes to preselect in the HTTP or stdio sign-in form. Valid values: {ValidScopes}. Omit to use configured defaults, or all scopes when none are configured.",
        AllowMultipleArgumentsPerToken = true
    };

    public TransportMode? Transport { get; private init; }
    public IReadOnlyList<ToolScope> Scopes { get; private init; } = [];

    public static CliOptions Parse(string[] args)
    {
        var root = new RootCommand("Genetec Security Center MCP Server");
        root.Options.Add(TransportOption);
        root.Options.Add(ScopesOption);
        root.TreatUnmatchedTokensAsErrors = false;

        var result = root.Parse(args);
        if (result.Errors.Count > 0)
        {
            foreach (var e in result.Errors)
                Console.Error.WriteLine(e.Message);
            Environment.Exit(1);
        }

        var rawScopes = result.GetValue(ScopesOption) ?? [];
        var scopes = rawScopes
            .SelectMany(s => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(ParseScope)
            .ToArray();

        return new CliOptions
        {
            Transport = result.GetValue(TransportOption),
            Scopes = scopes
        };
    }

    public IReadOnlyDictionary<string, string?> ToConfigOverrides()
    {
        var d = new Dictionary<string, string?>();

        switch (Transport)
        {
            case TransportMode.Stdio:
                d["Transport:Stdio:Enabled"] = "true";
                d["Transport:Http:Enabled"]  = "false";
                break;
            case TransportMode.Http:
                d["Transport:Stdio:Enabled"] = "false";
                d["Transport:Http:Enabled"]  = "true";
                break;
        }

        for (int i = 0; i < Scopes.Count; i++)
            d[$"Transport:Scopes:{i}"] = Scopes[i].ToString();

        return d;
    }

    private static ToolScope ParseScope(string value)
    {
        var normalized = value.Replace("_", "");
        foreach (var scope in Enum.GetValues<ToolScope>())
        {
            if (string.Equals(scope.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
                return scope;
        }
        throw new ArgumentException($"Unknown scope '{value}'. Valid values: {ValidScopes}.");
    }
}
