namespace Genetec.Mcp.SecurityCenter.Authentication;

internal static class OAuthRedirectUri
{
    internal static bool IsAllowed(string value, bool requireLoopback = false)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || string.IsNullOrEmpty(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || value.Contains('#'))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            return false;

        return requireLoopback
            ? uri.IsLoopback
            : uri.Scheme == Uri.UriSchemeHttps || uri.IsLoopback;
    }
}
