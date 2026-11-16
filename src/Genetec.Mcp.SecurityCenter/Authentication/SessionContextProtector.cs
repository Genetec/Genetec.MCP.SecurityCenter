using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Configuration;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Genetec.Mcp.SecurityCenter.Authentication;

public sealed class SessionContextProtector : IDisposable
{
    private const byte FormatVersion = 1;
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const string Domain = "Genetec.Mcp.SecurityCenter|sc-context|v1|aes-256-gcm";

    private readonly byte[] _key;
    private bool _disposed;

    public SessionContextProtector(IOptions<AuthenticationOptions> options)
    {
        var signingKey = Encoding.UTF8.GetBytes(options.Value.SigningKey);
        var domain = Encoding.UTF8.GetBytes(Domain);
        try
        {
            _key = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                signingKey,
                KeySize,
                Array.Empty<byte>(),
                domain);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(signingKey);
            CryptographicOperations.ZeroMemory(domain);
        }
    }

    public string Protect(ScConnectionContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(context);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        var associatedData = new[] { FormatVersion };
        var payload = new byte[1 + NonceSize + TagSize + ciphertext.Length];
        try
        {
            using var aes = new AesGcm(_key, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

            payload[0] = FormatVersion;
            nonce.CopyTo(payload, 1);
            tag.CopyTo(payload, 1 + NonceSize);
            ciphertext.CopyTo(payload, 1 + NonceSize + TagSize);
            return Base64UrlEncode(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(associatedData);
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    public ScConnectionContext? Unprotect(string? value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrEmpty(value))
            return null;

        byte[]? payload = null;
        byte[]? plaintext = null;
        var associatedData = new[] { FormatVersion };
        try
        {
            payload = WebEncoders.Base64UrlDecode(value);
            if (payload.Length < 1 + NonceSize + TagSize || payload[0] != FormatVersion)
                return null;

            var nonce = payload.AsSpan(1, NonceSize);
            var tag = payload.AsSpan(1 + NonceSize, TagSize);
            var ciphertext = payload.AsSpan(1 + NonceSize + TagSize);
            plaintext = new byte[ciphertext.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(
                nonce, ciphertext, tag, plaintext.AsSpan(),
                new byte[] { FormatVersion }.AsSpan());
            return JsonSerializer.Deserialize<ScConnectionContext>(plaintext);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
        finally
        {
            if (payload is not null)
                CryptographicOperations.ZeroMemory(payload);
            if (plaintext is not null)
                CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(associatedData);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        CryptographicOperations.ZeroMemory(_key);
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
