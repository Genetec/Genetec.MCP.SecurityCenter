using System.Text;
using Microsoft.Extensions.Options;

namespace Genetec.Mcp.SecurityCenter.Configuration;

internal sealed class AuthenticationOptionsValidator : IValidateOptions<AuthenticationOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthenticationOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(options.SigningKey))
            errors.Add("Authentication:SigningKey is required.");
        else if (Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
            errors.Add($"Authentication:SigningKey must be at least 32 bytes (got {Encoding.UTF8.GetByteCount(options.SigningKey)}). HMAC-SHA256 requires a 256-bit key.");

        if (string.IsNullOrEmpty(options.Issuer))
            errors.Add("Authentication:Issuer is required.");

        if (string.IsNullOrEmpty(options.Audience))
            errors.Add("Authentication:Audience is required.");

        if (options.TokenLifetimeMinutes <= 0)
            errors.Add("Authentication:TokenLifetimeMinutes must be greater than 0.");

        if (options.RefreshTokenLifetimeDays <= 0)
            errors.Add("Authentication:RefreshTokenLifetimeDays must be greater than 0.");

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}

internal sealed class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>
{
    public ValidateOptionsResult Validate(string? name, RateLimitingOptions options)
    {
        var errors = new List<string>();

        if (options.McpMaxConcurrentPerUser <= 0)
            errors.Add("RateLimiting:McpMaxConcurrentPerUser must be greater than 0.");

        if (options.McpWindowCallLimit <= 0)
            errors.Add("RateLimiting:McpWindowCallLimit must be greater than 0.");

        if (options.McpWindowMinutes <= 0)
            errors.Add("RateLimiting:McpWindowMinutes must be greater than 0.");

        if (options.McpConcurrencyWaitSeconds < 0)
            errors.Add("RateLimiting:McpConcurrencyWaitSeconds must be 0 or greater.");

        if (options.AuthorizeWindowAttemptLimit <= 0)
            errors.Add("RateLimiting:AuthorizeWindowAttemptLimit must be greater than 0.");

        if (options.AuthorizeWindowSeconds <= 0)
            errors.Add("RateLimiting:AuthorizeWindowSeconds must be greater than 0.");

        if (options.TokenWindowAttemptLimit <= 0)
            errors.Add("RateLimiting:TokenWindowAttemptLimit must be greater than 0.");

        if (options.TokenWindowSeconds <= 0)
            errors.Add("RateLimiting:TokenWindowSeconds must be greater than 0.");

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
