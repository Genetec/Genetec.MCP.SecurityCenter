using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Client;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Authentication;

public sealed class SecurityNoticesStartupTests
{
    [Theory]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, true)]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, false)]
    public async Task ProcessStartupLogsSharedNoticesAndHttpSignInRendersThem(
        bool stdio, bool useHttps, bool skipCertificateValidation, bool browserHttp)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"mcp-security-notices-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        using var process = new Process();
        var processStarted = false;
        try
        {
            var configPath = Path.Combine(directory, "appsettings.json");
            var certificatePath = Path.Combine(directory, "server.pfx");
            if (!stdio && !browserHttp)
            {
                using var rsa = RSA.Create(2048);
                var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
                await File.WriteAllBytesAsync(certificatePath, certificate.Export(X509ContentType.Pfx));
            }
            var endpoint = new Dictionary<string, object>
            {
                ["Url"] = $"{(browserHttp ? "http" : "https")}://127.0.0.1:0"
            };
            if (!stdio && !browserHttp)
                endpoint["Certificate"] = new { Path = certificatePath };
            await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(new
            {
                SecurityCenter = new { WebSdkSettings = new { Hosts = new[] { "localhost" }, UseHttps = useHttps, SkipCertificateValidation = skipCertificateValidation } },
                Transport = new { Stdio = new { Enabled = stdio }, Http = new { Enabled = !stdio } },
                Authentication = new { SigningKeyProtection = new { Provider = "FileSystem", KeyRingPath = Path.Combine(directory, "keys") } },
                Kestrel = new { Endpoints = new { Test = endpoint } },
                Serilog = new
                {
                    Using = new[] { "Serilog.Sinks.Console" },
                    MinimumLevel = "Information",
                    WriteTo = new[] { new { Name = "Console", Args = new { standardErrorFromLevel = "Verbose", outputTemplate = "{Message:lj}{NewLine}{Exception}" } } }
                }
            }));
            process.StartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = directory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.StartInfo.ArgumentList.Add(typeof(SecurityCenterSessionManager).Assembly.Location);
            foreach (var key in process.StartInfo.Environment.Keys.ToArray())
                if (new[] { "SecurityCenter__", "Transport__", "Authentication__", "Kestrel__", "Serilog__" }
                    .Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                    process.StartInfo.Environment.Remove(key);
            process.StartInfo.Environment["APPSETTINGS_PATH"] = configPath;
            process.StartInfo.Environment["DOTNET_ENVIRONMENT"] = "Production";
            process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";

            var messages = new ConcurrentQueue<string>();
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not { } message) return;
                messages.Enqueue(message);
                if (message.StartsWith("Application started.")) started.TrySetResult();
            };
            Assert.True(process.Start());
            processStarted = true;
            process.BeginErrorReadLine();
            var stdout = process.StandardOutput.ReadToEndAsync();
            var exited = process.WaitForExitAsync();
            await Task.WhenAny(started.Task, exited).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(started.Task.IsCompleted, string.Join(Environment.NewLine, messages));

            // ApplicationStarted callbacks may run just after the host's readiness log.
            if (browserHttp)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while (!messages.Any(message => message.Contains(SecurityNotices.BrowserHttpMessage)))
                    await Task.Delay(20, timeout.Token);
            }
            Assert.Contains($"{SecurityNotices.AgentAccessTitle}: {SecurityNotices.AgentAccessMessage}", messages);
            Assert.Equal(!useHttps, messages.Contains($"{SecurityNotices.WebSdkHttpTitle}: {SecurityNotices.WebSdkHttpMessage}"));
            Assert.Equal(useHttps && skipCertificateValidation,
                messages.Contains($"{SecurityNotices.CertificateValidationTitle}: {SecurityNotices.CertificateValidationMessage}"));
            Assert.Equal(browserHttp, messages.Contains($"{SecurityNotices.BrowserHttpTitle}: {SecurityNotices.BrowserHttpMessage}"));

            if (!stdio)
            {
                var address = Assert.Single(messages, message => message.StartsWith("Now listening on: "))["Now listening on: ".Length..];
                using var handler = new HttpClientHandler();
                if (!browserHttp)
                    handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };
                var challenge = new string('a', 43);
                var page = WebUtility.HtmlDecode(await client.GetStringAsync(
                    $"/authorize?response_type=code&redirect_uri=http://localhost/callback&code_challenge={challenge}&code_challenge_method=S256"));
                Assert.Contains(SecurityNotices.AgentAccessMessage, page);
                Assert.Equal(!useHttps, page.Contains(SecurityNotices.WebSdkHttpMessage));
                Assert.Equal(useHttps && skipCertificateValidation, page.Contains(SecurityNotices.CertificateValidationMessage));
                Assert.Contains(SecurityNotices.BrowserHttpMessage, page);
                Assert.Contains("window.location.protocol === 'http:'", page);
                Assert.DoesNotContain("{{", page);
            }
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Equal("", await stdout);
        }
        finally
        {
            if (processStarted && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            Directory.Delete(directory, recursive: true);
        }
    }
}
