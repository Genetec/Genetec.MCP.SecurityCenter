using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// Tests share a live SC instance and mutate its configuration and hardware.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Genetec.Mcp.SecurityCenter.IntegrationTests;

public sealed class ScIntegrationFixture : IAsyncLifetime
{
    private readonly List<Guid> _trackedGuids = [];
    private readonly ConnectionOptions _settings;
    private bool _initialized;

    public SecurityCenterSessionManager Manager { get; }
    public bool IsAvailable => _settings.Hosts is { Length: > 0 } && !string.IsNullOrEmpty(_settings.Hosts[0]);

    public ScIntegrationFixture()
    {
        _settings = LoadSettings();
        Manager = BuildManager(_settings.SkipCertificateValidation, _settings);
    }

    public Task InitializeAsync()
    {
        if (!IsAvailable) return Task.CompletedTask;

        Manager.Connect("default", Manager.CreateConnectionContext(
            _settings.User, _settings.Password, _settings.Hosts[0].Trim()));

        _initialized = true;
        return Task.CompletedTask;
    }

    public void TrackForCleanup(string guid) => _trackedGuids.Add(Guid.Parse(guid));
    public void TrackForCleanup(Guid? guid) { if (guid.HasValue) _trackedGuids.Add(guid.Value); }

    public async Task<Guid?> GetServerIdAsync()
    {
        if (!IsAvailable) return null;
        try
        {
            var json = await Manager.GetClient().ListEntities("Server", 1, 1, ["Name"]);
            return IntegrationTestHelpers.ExtractFirstGuid(json);
        }
        catch { return null; }
    }

    public static Guid? ExtractFirstGuid(string json)
        => IntegrationTestHelpers.ExtractFirstGuid(json);

    public Task<(bool Success, string? Error)> ValidateConfiguredConnection() =>
        Manager.ValidateConnection(_settings.User, _settings.Password, _settings.Hosts[0].Trim());

    public async Task DisposeAsync()
    {
        if (!_initialized || _trackedGuids.Count == 0) return;

        var client = Manager.GetClient();
        foreach (var guid in _trackedGuids)
        {
            try { await client.DeleteEntity(guid); }
            catch { /* best-effort */ }
        }
    }

    public static SecurityCenterSessionManager FakeManager()
    {
        var manager = BuildManager(skipCertValidation: true);
        manager.Connect("test", new ScConnectionContext
        {
            Host = "localhost",
            Port = 4590,
            User = "test",
            Password = "test",
        });
        return manager;
    }

    public static SecurityCenterSessionManager BuildManager(bool skipCertValidation = false, ConnectionOptions? settings = null)
    {
        var services = new ServiceCollection();
        services.AddHttpClient(SecurityCenterSessionManager.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                SslOptions = new SslClientAuthenticationOptions
                {
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    RemoteCertificateValidationCallback = skipCertValidation
                        ? (_, _, _, _) => true
                        : null
                }
            });
        var sp = services.BuildServiceProvider();
        return new SecurityCenterSessionManager(
            Options.Create(new SecurityCenterOptions { WebSdkSettings = settings }),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp);
    }

    private static ConnectionOptions LoadSettings()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.test.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var settings = configuration.GetSection("SecurityCenter:WebSdkSettings");
        var connectionOptions = settings.Get<ConnectionOptions>();
        // Integration tests sign in directly to the first configured allowed host.
        var hosts = connectionOptions?.Hosts;
        if (connectionOptions is null || hosts is not { Length: > 0 } || string.IsNullOrWhiteSpace(hosts[0]))
            throw new InvalidOperationException(
                "Integration tests require SecurityCenter:WebSdkSettings:Hosts:0 in test settings or environment variables.");
        if (connectionOptions.Port is < 1 or > 65535)
            throw new InvalidOperationException(
                "Integration tests require a valid SecurityCenter:WebSdkSettings:Port.");
        if (string.IsNullOrWhiteSpace(connectionOptions.User))
            throw new InvalidOperationException(
                "Integration tests require SecurityCenter:WebSdkSettings:User in appsettings.test.json or environment variables.");

        return connectionOptions;
    }
}
