using System.Security.Cryptography;
using System.Xml.Linq;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Authentication;

public sealed class SigningKeyProtectionTests : IDisposable
{
    private readonly string keyRingPath = Path.Combine(Path.GetTempPath(), $"mcp-key-ring-tests-{Guid.NewGuid():N}");

    [Fact]
    public void RotationPreservesExistingCiphertextAndPurposeIsolation()
    {
        var created = SigningKeyDataProtection.CreateKey(keyRingPath, SigningKeyProtectionProvider.FileSystem);
        Assert.Equal(32, Convert.FromBase64String(created.SigningKey).Length);
        Assert.Equal(created.SigningKey,
            SigningKeyDataProtection.Unprotect(created.ProtectedSigningKey, keyRingPath, SigningKeyProtectionProvider.FileSystem));
        using (var services = SigningKeyDataProtection.CreateProvider(keyRingPath, SigningKeyProtectionProvider.FileSystem, initialize: true))
        {
            services.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90));
            var differentPurpose = services.GetRequiredService<IDataProtectionProvider>().CreateProtector("OtherSecret");
            Assert.Throws<CryptographicException>(() => differentPurpose.Unprotect(created.ProtectedSigningKey));
        }
        SigningKeyDataProtection.RestrictNewKeyFiles(keyRingPath);
        Assert.Equal(2, Directory.GetFiles(keyRingPath).Length);
        Assert.Equal(created.SigningKey,
            SigningKeyDataProtection.Unprotect(created.ProtectedSigningKey, keyRingPath, SigningKeyProtectionProvider.FileSystem));
    }

    [Fact]
    public void MissingExistingKeyRingDoesNotCreateReplacementState()
    {
        Assert.Throws<DirectoryNotFoundException>(() =>
            SigningKeyDataProtection.Unprotect("established-ciphertext", keyRingPath, SigningKeyProtectionProvider.FileSystem));
        Assert.False(Directory.Exists(keyRingPath));
    }

    [Fact]
    public void AutoProtectsPersistedKeyWithDpapiOnWindowsAndFilePermissionsOnLinux()
    {
        var created = SigningKeyDataProtection.CreateKey(keyRingPath, SigningKeyProtectionProvider.Auto);
        var keyFile = Assert.Single(Directory.GetFiles(keyRingPath, "key-*.xml"));
        var xml = XElement.Load(keyFile);
        var encryptedSecrets = xml.Descendants(XName.Get("encryptedSecret", "http://schemas.asp.net/2015/03/dataProtection"));
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(SigningKeyProtectionProvider.WindowsDpapi,
                SigningKeyDataProtection.ResolveProvider(SigningKeyProtectionProvider.Auto));
            var encryptedSecret = Assert.Single(encryptedSecrets);
            Assert.StartsWith("Microsoft.AspNetCore.DataProtection.XmlEncryption.DpapiXmlDecryptor,",
                encryptedSecret.Attribute("decryptorType")!.Value);
            Assert.Empty(xml.Descendants("masterKey"));
        }
        else
        {
            Assert.Equal(SigningKeyProtectionProvider.FileSystem,
                SigningKeyDataProtection.ResolveProvider(SigningKeyProtectionProvider.Auto));
            Assert.Empty(encryptedSecrets);
            var masterKey = Assert.Single(xml.Descendants("masterKey"));
            Assert.NotEmpty(Convert.FromBase64String(masterKey.Element("value")!.Value));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(keyFile));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(keyRingPath));
        }
        Assert.Equal(created.SigningKey,
            SigningKeyDataProtection.Unprotect(created.ProtectedSigningKey, keyRingPath, SigningKeyProtectionProvider.Auto));
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(keyFile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);
            Assert.Throws<UnauthorizedAccessException>(() =>
                SigningKeyDataProtection.Unprotect(created.ProtectedSigningKey, keyRingPath, SigningKeyProtectionProvider.Auto));
            File.SetUnixFileMode(keyFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.SetUnixFileMode(keyRingPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.OtherExecute);
            Assert.Throws<UnauthorizedAccessException>(() =>
                SigningKeyDataProtection.Unprotect(created.ProtectedSigningKey, keyRingPath, SigningKeyProtectionProvider.Auto));
            File.SetUnixFileMode(keyRingPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(keyRingPath)) Directory.Delete(keyRingPath, recursive: true);
    }
}
