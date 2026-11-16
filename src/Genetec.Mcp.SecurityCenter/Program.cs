using Genetec.Mcp.SecurityCenter;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.DependencyInjection;
using Genetec.Mcp.SecurityCenter.Audit;
using Genetec.Mcp.SecurityCenter.Configuration;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Security.Authentication;
using Serilog;

var cli = CliOptions.Parse(args);
var serverVersion = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

var configPath = Environment.GetEnvironmentVariable("APPSETTINGS_PATH")
    ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");

var configuration = new ConfigurationBuilder()
    .AddSecurityCenterConfiguration(configPath, cli)
    .Build();

var scOptions = configuration
    .GetSection(SecurityCenterOptions.SectionName)
    .Get<SecurityCenterOptions>();

var transportOptions = configuration
    .GetSection(TransportOptions.SectionName)
    .Get<TransportOptions>() ?? new TransportOptions();

var rateLimitingEnabled = configuration
    .GetSection(RateLimitingOptions.SectionName)
    .Get<RateLimitingOptions>()?.Enabled ?? true;

const string McpServerInstructions =
    "Tool schemas are not included in tools/list to reduce token cost. " +
    "Before calling a tool other than session_management for the first time in a session, call search_tools(query:\"<tool_name>\", detail:\"full\") " +
    "to retrieve its parameter schema. Use detail=\"summary\" to discover tools by keyword or domain scope. " +
    "Never guess parameter names — always fetch the schema first. " +
    "Prefer batch operations over loops: use a single tool call with multiple items rather than calling the same tool repeatedly.";

var serverInstructions = McpServerInstructions + (rateLimitingEnabled
    ? " Single-item loops are rate-limited and will be throttled."
    : "");

// Sanity checks on config
if (transportOptions.Stdio.Enabled && transportOptions.Http.Enabled)
    throw new InvalidOperationException("Cannot enable both Stdio and Http transports. Set one to true and the other to false.");

if (!transportOptions.Stdio.Enabled && !transportOptions.Http.Enabled)
    throw new InvalidOperationException("Must enable either Stdio or Http transport.");

if (!transportOptions.Stdio.Enabled)
{
    // HTTP/SSE mode using WebApplication
    var builder = WebApplication.CreateBuilder(args);
    builder.Configuration.AddSecurityCenterConfiguration(configPath, cli);
    builder.Configuration.SetDefaultFileLogPaths();
    // Validate the final host configuration and load the key before listening.
    var signingKey = await HttpSigningKeyStartup.LoadAsync(builder.Configuration, configPath);
    builder.Host.UseSerilog((context, config) =>
        config.ReadFrom.Configuration(context.Configuration));
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.ConfigureHttpsDefaults(httpsOptions =>
        {
            httpsOptions.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        });
    });
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddCache();
    builder.Services.AddSecurityCenterHttpClient(builder.Configuration);
    builder.Services.AddOptions<SecurityCenterOptions>().BindConfiguration(SecurityCenterOptions.SectionName);
    builder.Services.AddOptions<TransportOptions>().BindConfiguration(TransportOptions.SectionName);
    builder.Services.AddSingleton<SecurityCenterSessionManager>();
    builder.Services.AddSingleton<IAuditService, AuditService>();
    builder.Services.AddSecurityCenterAuthentication(builder.Configuration, signingKey);
    if (rateLimitingEnabled)
    {
        builder.Services.AddThrottling(builder.Configuration);
        builder.Services.AddAuthorizationThrottling();
    }
    else
        builder.Services.AddRateLimitingOptions(builder.Configuration);
    builder.Services
        .AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "Genetec Security Center",
                Version = serverVersion,
                Title = "MCP for Genetec Security Center",
                Description = "An MCP server allowing interaction with Genetec Security Center unified security platform.",
                WebsiteUrl = "https://www.genetec.com/",
                Icons =
                [
                    new ModelContextProtocol.Protocol.Icon
                    {
                        Source = "https://www.genetec.com/favicon.ico",
                        MimeType = "image/x-icon",
                        Sizes = ["256x256"]
                    }
                ]
            };
            options.ServerInstructions = serverInstructions;
        })
        .WithHttpTransport(options =>
        {
            options.SessionMode = ModelContextProtocol.AspNetCore.HttpServerSessionMode.StatefulForInitializeClients;
        })
        .WithToolsFromAssembly(serializerOptions: NumericStringConverter.ToolOptions)
        .WithPromptsFromAssembly()
        .WithResourcesFromAssembly()
        .WithRequestFilters(filters =>
        {
            filters.AddAuditCallFilter();
            filters.AddScopedToolFilter();
            filters.AddSchemaStripFilter();
            filters.AddAuditResourceFilter();
            if (rateLimitingEnabled)
            {
                filters.AddThrottleResourceFilter();
                filters.AddThrottleCallFilter();
            }
            filters.AddErrorHandlerCallFilter();
        });
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    });


    var app = builder.Build();

    app.UseForwardedHeaders();

    SecurityNotices.LogStartup(app.Logger,
        app.Services.GetRequiredService<IOptions<SecurityCenterOptions>>().Value.WebSdkSettings);
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        if (app.Urls.Any(url => Uri.TryCreate(url, UriKind.Absolute, out var address) && address.Scheme == Uri.UriSchemeHttp))
            app.Logger.LogWarning("{Title}: {Message}", SecurityNotices.BrowserHttpTitle, SecurityNotices.BrowserHttpMessage);
    });

    app.UseHsts();
    app.UseRouting();
    if (rateLimitingEnabled)
        app.UseRateLimiter();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapOAuthMetadataEndpoints();
    app.MapAuthorizeEndpoint();
    app.MapTokenEndpoint();
    app.MapClientRegistrationEndpoint();
    app.MapMcp(McpServerEndpoints.Mcp).RequireAuthorization();

    await app.RunAsync();
}
else // transportOptions.Stdio
{
    SecurityCenterSessionManager sessionManager = null!;
    // Stdio mode using generic host
    var builder = Host.CreateApplicationBuilder(args);
    builder.Configuration.AddSecurityCenterConfiguration(configPath, cli);
    builder.Configuration.SetDefaultFileLogPaths();
    builder.Services.AddSerilog(config =>
        config.ReadFrom.Configuration(builder.Configuration));
    builder.Services.AddSecurityCenterHttpClient(builder.Configuration);
    if (rateLimitingEnabled)
        builder.Services.AddThrottling(builder.Configuration);
    else
        builder.Services.AddRateLimitingOptions(builder.Configuration);
    builder.Services.AddOptions<SecurityCenterOptions>().BindConfiguration(SecurityCenterOptions.SectionName);
    builder.Services.AddSingleton<SecurityCenterSessionManager>();
    builder.Services.AddSingleton<StdioSignInService>();
    builder.Services.AddSingleton<IAuditService, AuditService>();

    builder.Services
        .AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "Genetec Security Center",
                Version = serverVersion,
                Title = "MCP for Genetec Security Center",
                Description = "An MCP server allowing interaction with Genetec Security Center unified security platform.",
                WebsiteUrl = "https://www.genetec.com/",
                Icons =
                [
                    new ModelContextProtocol.Protocol.Icon
                    {
                        Source = "https://www.genetec.com/favicon.ico",
                        MimeType = "image/x-icon",
                        Sizes = ["256x256"]
                    }
                ]
            };
            options.ServerInstructions =
                "Before using Security Center tools, call session_management with action 'connect' to sign in. " +
                "If connect returns Pending, show sign_in_url to the user. " +
                "Wait for the user to finish signing in, then call session_management with action 'list' " +
                "and confirm an active session before continuing; connect reuses an existing session. " +
                "After logout, call connect to sign in again. " + serverInstructions;
        })
        .WithStdioServerTransport()
        .WithToolsFromAssembly(serializerOptions: NumericStringConverter.ToolOptions)
        .WithPromptsFromAssembly()
        .WithResourcesFromAssembly()
        .WithRequestFilters(filters =>
        {
            filters.AddAuditCallFilter();
            filters.AddScopedToolFilter(() => sessionManager.StdioScopes, () => sessionManager.HasSession());
            filters.AddSchemaStripFilter();
            filters.AddAuditResourceFilter();
            if (rateLimitingEnabled)
            {
                filters.AddThrottleResourceFilter();
                filters.AddThrottleCallFilter();
            }
            filters.AddErrorHandlerCallFilter();
        });

    using var host = builder.Build();
    sessionManager = host.Services.GetRequiredService<SecurityCenterSessionManager>();
    sessionManager.StdioScopes = transportOptions.Scopes.ToHashSet();
    var startupLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    SecurityNotices.LogStartup(startupLogger,
        host.Services.GetRequiredService<IOptions<SecurityCenterOptions>>().Value.WebSdkSettings);

    if (scOptions?.WebSdkSettings?.Hosts?.Any(host => !string.IsNullOrWhiteSpace(host)) != true)
        throw new InvalidOperationException(
            "No Security Center hosts configured. Set SecurityCenter__WebSdkSettings__Hosts__0 in the environment or appsettings.json.");

    await host.RunAsync();
}

