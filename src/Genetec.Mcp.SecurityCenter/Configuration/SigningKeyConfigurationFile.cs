using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Genetec.Mcp.SecurityCenter.Configuration;

internal static class SigningKeyConfigurationFile
{
    internal const int MaximumBytes = 4 * 1024 * 1024;
    private static readonly byte[] NewLineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);
    private static readonly ConditionalWeakTable<object, HashSet<string>> ConfiguredProperties = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowDuplicateProperties = false,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { PreserveConfiguredProperties } },
        Converters = { new JsonStringEnumConverter<SigningKeyProtectionProvider>() }
    };

    private static void PreserveConfiguredProperties(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(PersistedAuthenticationOptions)
            && typeInfo.Type != typeof(SigningKeyProtectionSettings))
            return;

        // Saving the managed key must not materialize defaults for omitted settings:
        // those settings may be supplied by a retained host configuration provider.
        foreach (var property in typeInfo.Properties)
        {
            if (property.IsExtensionData || property.Set is not { } setter
                || property.Name is nameof(AuthenticationOptions.SigningKey) or nameof(PersistedAuthenticationOptions.ProtectedSigningKey))
                continue;

            property.Set = (instance, value) =>
            {
                setter(instance, value);
                ConfiguredProperties.GetOrCreateValue(instance).Add(property.Name);
            };
            property.ShouldSerialize = (instance, _) =>
                ConfiguredProperties.TryGetValue(instance, out var properties) && properties.Contains(property.Name);
        }
    }

    internal static async Task<SigningKeyConfiguration> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = await ReadBoundedBytesAsync(path, MaximumBytes, cancellationToken).ConfigureAwait(false);
        try
        {
            return Parse(bytes);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static SigningKeyConfiguration Parse(ReadOnlySpan<byte> content)
    {
        try
        {
            return JsonSerializer.Deserialize<SigningKeyConfiguration>(content, JsonOptions)
                ?? throw new InvalidDataException("The signing-key configuration must be a JSON object.");
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new InvalidDataException("The signing-key configuration is not valid JSON.", exception);
        }
    }

    internal static async Task WriteAsync(string path, SigningKeyConfiguration root, CancellationToken cancellationToken)
    {
        EnsureSafeFile(path);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(root, JsonOptions);
        var temporaryPath = Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        var ownsTemporaryFile = false;
        try
        {
            if (bytes.Length + NewLineBytes.Length > MaximumBytes)
                throw new InvalidDataException("The signing-key configuration file is too large.");

            cancellationToken.ThrowIfCancellationRequested();
            // Stage beside the destination so replacement stays on one filesystem.
            await using (var stream = new FileStream(temporaryPath, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 4096,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough
            }))
            {
                ownsTemporaryFile = true;
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(temporaryPath, File.GetUnixFileMode(path));
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(NewLineBytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            EnsureSafeFile(path);
            cancellationToken.ThrowIfCancellationRequested();
            // Windows retains destination metadata; Unix retains the staged file's
            // owner and the destination mode copied above.
            if (OperatingSystem.IsWindows())
                File.Replace(temporaryPath, path, destinationBackupFileName: null);
            else
                File.Move(temporaryPath, path, overwrite: true);
            ownsTemporaryFile = false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            if (ownsTemporaryFile) File.Delete(temporaryPath);
        }
    }

    internal static void EnsureSafeFile(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Signing-key paths must be absolute.", nameof(path));
        if (!File.Exists(path) && !Directory.Exists(path))
            throw new FileNotFoundException($"Required signing-key state file '{path}' does not exist.", path);
        EnsureNoSymbolicLinks(path);
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0 || Directory.Exists(path))
            throw new IOException("Signing-key state paths cannot be symbolic links or directories.");
    }

    internal static void EnsureNoSymbolicLinks(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            if ((File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Signing-key paths cannot contain symbolic links or reparse points.");
            current = Path.GetDirectoryName(current);
        }
    }

    private static async Task<byte[]> ReadBoundedBytesAsync(string path, int maximumBytes, CancellationToken cancellationToken)
    {
        EnsureSafeFile(path);
        var info = new FileInfo(path);
        if (info.Length > maximumBytes) throw new InvalidDataException("The signing-key file is too large.");
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var result = new MemoryStream();
        var buffer = new byte[8192];
        try
        {
            while (true)
            {
                var count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (count == 0) break;
                if (result.Length + count > maximumBytes) throw new InvalidDataException("The signing-key file is too large.");
                result.Write(buffer, 0, count);
            }
            return result.ToArray();
        }
        finally
        {
            if (result.TryGetBuffer(out var segment) && segment.Array is not null)
                CryptographicOperations.ZeroMemory(segment.Array.AsSpan(segment.Offset, segment.Count));
            CryptographicOperations.ZeroMemory(buffer);
        }
    }
}

internal sealed class SigningKeyConfiguration
{
    private PersistedAuthenticationOptions authentication = new();

    public PersistedAuthenticationOptions Authentication
    {
        get => authentication;
        set => authentication = value ?? throw new InvalidDataException("Authentication must be a JSON object.");
    }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalSettings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class PersistedAuthenticationOptions : AuthenticationOptions
{
    private string? protectedSigningKey;

    // Absence requires initialization. Explicit null or empty values must fail
    // before touching an established key ring.
    public string? ProtectedSigningKey
    {
        get => protectedSigningKey;
        set => protectedSigningKey = !string.IsNullOrWhiteSpace(value) ? value
            : throw new InvalidDataException($"{SectionName}:{nameof(ProtectedSigningKey)} must be a Data Protection string.");
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public override string SigningKey
    {
        get => null!;
        set => throw new InvalidDataException($"{SectionName}:{nameof(SigningKey)} is plaintext. Remove it and use {SectionName}:{nameof(ProtectedSigningKey)}.");
    }
}
