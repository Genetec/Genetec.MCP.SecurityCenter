using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using System.Globalization;
using System.Threading.RateLimiting;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Genetec.Mcp.SecurityCenter.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddCache(
        this IServiceCollection services,
        Action<MemoryCacheOptions>? configure = null)
    {
        if (configure is not null)
            services.AddMemoryCache(configure);
        else
            services.AddMemoryCache();
        return services;
    }

    public static IServiceCollection AddSecurityCenterAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        string signingKey)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        var authOptions = configuration
            .GetSection(AuthenticationOptions.SectionName)
            .Get<AuthenticationOptions>() ?? new AuthenticationOptions();

        services.AddSingleton<IValidateOptions<AuthenticationOptions>, AuthenticationOptionsValidator>();
        services.AddOptions<AuthenticationOptions>()
            .Bind(configuration.GetSection(AuthenticationOptions.SectionName))
            .PostConfigure(options => options.SigningKey = signingKey)
            .ValidateOnStart();

        services.AddSingleton<AuthorizationCodeStore>();
        services.AddSingleton<ClientRegistrationStore>();
        services.AddSingleton<SessionContextProtector>();
        services.AddAntiforgery(options => options.FormFieldName = "csrf_token");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = authOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (context.Principal?.HasClaim("token_type", "refresh") == true)
                            context.Fail("Refresh tokens cannot be used as bearer access tokens.");
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddRateLimitingOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<RateLimitingOptions>, RateLimitingOptionsValidator>();
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateOnStart();
        return services;
    }

    public static IServiceCollection AddThrottling(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRateLimitingOptions(configuration);
        services.AddSingleton<ThrottleService>();
        services.AddSingleton<SdkConcurrencyLimiter>();
        return services;
    }

    public static IServiceCollection AddAuthorizationThrottling(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };
            options.AddPolicy(AuthorizeEndpoint.RateLimitPolicy, context =>
            {
                var settings = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                var address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(address, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.AuthorizeWindowAttemptLimit,
                    Window = TimeSpan.FromSeconds(settings.AuthorizeWindowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });
            options.AddPolicy(TokenEndpoint.RateLimitPolicy, context =>
            {
                var settings = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                var address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(address, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.TokenWindowAttemptLimit,
                    Window = TimeSpan.FromSeconds(settings.TokenWindowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });
        });
        return services;
    }

    public static IServiceCollection AddSecurityCenterHttpClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var skipCert = configuration
            .GetSection(SecurityCenterOptions.SectionName)
            .Get<SecurityCenterOptions>()?.WebSdkSettings?.SkipCertificateValidation ?? false;

        services.AddHttpClient(SecurityCenterSessionManager.HttpClientName)
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                SslOptions = new SslClientAuthenticationOptions
                {
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    RemoteCertificateValidationCallback = skipCert
                        ? (_, _, _, _) => true
                        : WebSdkCertificateValidation.Validate
                }
            });

        return services;
    }
}
