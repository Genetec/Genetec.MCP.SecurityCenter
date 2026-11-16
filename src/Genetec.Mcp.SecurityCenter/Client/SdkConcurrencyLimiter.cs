using System.Threading.RateLimiting;
using Genetec.Mcp.SecurityCenter.Configuration;
using Microsoft.Extensions.Options;
using Genetec.Mcp.SecurityCenter.Helpers;

namespace Genetec.Mcp.SecurityCenter.Client;

/// <summary>Bounds SDK requests independently of the enclosing MCP call permits.</summary>
public sealed class SdkConcurrencyLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter;
    private readonly TimeSpan _wait;

    public SdkConcurrencyLimiter(IOptions<RateLimitingOptions> options)
    {
        var settings = options.Value;
        _wait = TimeSpan.FromSeconds(settings.McpConcurrencyWaitSeconds);
        _limiter = PartitionedRateLimiter.Create<string, string>(user =>
            RateLimitPartition.GetConcurrencyLimiter(user, _ => new ConcurrencyLimiterOptions
            {
                PermitLimit = settings.McpMaxConcurrentPerUser,
                QueueLimit = checked(settings.McpMaxConcurrentPerUser * ResourceLimits.MaxBatchItems),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
    }

    public async ValueTask<RateLimitLease> AcquireAsync(string user, CancellationToken cancellationToken)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        wait.CancelAfter(_wait);
        try
        {
            var lease = await _limiter.AcquireAsync(user, 1, wait.Token);
            if (lease.IsAcquired)
                return lease;

            lease.Dispose();
            throw new InvalidOperationException("SDK concurrency queue is full. Serialize requests and retry.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("SDK concurrency wait limit reached. Serialize requests and retry.");
        }
    }

    public void Dispose() => _limiter.Dispose();
}
