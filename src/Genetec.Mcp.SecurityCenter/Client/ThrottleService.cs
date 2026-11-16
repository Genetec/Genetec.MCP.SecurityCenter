using System.Threading.RateLimiting;
using Genetec.Mcp.SecurityCenter.Configuration;
using Microsoft.Extensions.Options;

namespace Genetec.Mcp.SecurityCenter.Client;

public sealed class ThrottleService : IDisposable
{
    private readonly IOptions<RateLimitingOptions> _options;
    private readonly PartitionedRateLimiter<string> _limiter;

    public ThrottleService(IOptions<RateLimitingOptions> options)
    {
        _options = options;
        var opts = options.Value;

        _limiter = PartitionedRateLimiter.CreateChained(
            PartitionedRateLimiter.Create<string, string>(userId =>
                RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = opts.McpWindowCallLimit,
                    Window = TimeSpan.FromMinutes(opts.McpWindowMinutes),
                    QueueLimit = 0,
                    AutoReplenishment = true
                })),
            PartitionedRateLimiter.Create<string, string>(userId =>
                RateLimitPartition.GetConcurrencyLimiter(userId, _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = opts.McpMaxConcurrentPerUser,
                    QueueLimit = opts.McpMaxConcurrentPerUser
                })));
    }

    public async Task<ThrottleAcquisition> TryAcquireAsync(string userId, CancellationToken ct)
    {
        var opts = _options.Value;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(opts.McpConcurrencyWaitSeconds));

        RateLimitLease lease;
        try
        {
            lease = await _limiter.AcquireAsync(userId, 1, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return ThrottleAcquisition.Rejected(
                $"Too many concurrent tool calls or resource reads. " +
                $"Max {opts.McpMaxConcurrentPerUser} running simultaneously. " +
                $"Waited {opts.McpConcurrencyWaitSeconds}s — serialize your requests and retry.");
        }

        if (!lease.IsAcquired)
        {
            lease.Dispose();

            // Distinguish window exhaustion from concurrency exhaustion via metadata
            if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                return ThrottleAcquisition.Rejected(
                    $"Call budget exceeded ({opts.McpWindowCallLimit} calls / {opts.McpWindowMinutes} min). " +
                    $"Retry after {retryAfter.TotalSeconds:0}s.");
            }

            return ThrottleAcquisition.Rejected(
                $"Too many concurrent tool calls or resource reads. Max {opts.McpMaxConcurrentPerUser} running simultaneously.");
        }

        return ThrottleAcquisition.Acquired(lease);
    }

    public void Dispose() => _limiter.Dispose();
}

public sealed class ThrottleAcquisition : IDisposable
{
    private readonly RateLimitLease? _lease;

    public bool IsAcquired { get; }
    public string? RejectionMessage { get; }

    private ThrottleAcquisition(bool acquired, string? message, RateLimitLease? lease)
    {
        IsAcquired = acquired;
        RejectionMessage = message;
        _lease = lease;
    }

    public static ThrottleAcquisition Acquired(RateLimitLease lease) => new(true, null, lease);
    public static ThrottleAcquisition Rejected(string message) => new(false, message, null);

    public void Dispose() => _lease?.Dispose();
}
