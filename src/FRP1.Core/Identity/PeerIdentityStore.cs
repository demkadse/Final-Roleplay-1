using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FRP1.Core.Identity;

public sealed class PeerIdentityStore
{
    private const int MaximumBytes = 64 * 1024;
    private static readonly byte[] Entropy = "FRP1.peer-identity.v1"u8.ToArray();
    private readonly string path;
    private readonly SemaphoreSlim gate = new(1, 1);

    public PeerIdentityStore(string configurationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationDirectory);
        path = Path.Combine(Path.GetFullPath(configurationDirectory), "peer-identity.dpapi");
    }

    public async Task<PeerIdentity> LoadOrCreateAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Protected peer identities require Windows DPAPI CurrentUser.");
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try { return await LoadAsync(cancellationToken).ConfigureAwait(false); }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }

            using var generated = PeerIdentity.CreatePersistent();
            var cleartext = generated.Certificate.Export(X509ContentType.Pkcs12);
            byte[] protectedBytes;
            try { protectedBytes = ProtectedData.Protect(cleartext, Entropy, DataProtectionScope.CurrentUser); }
            finally { CryptographicOperations.ZeroMemory(cleartext); }
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllBytesAsync(temporary, protectedBytes, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                try { File.Move(temporary, path, overwrite: false); }
                catch (IOException) when (File.Exists(path)) { /* Another instance created the identity; adopt that exact pin. */ }
            }
            finally { File.Delete(temporary); }
            return await LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public async Task<PeerIdentity> LoadExistingAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Protected peer identities require Windows DPAPI CurrentUser.");
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await LoadAsync(cancellationToken).ConfigureAwait(false); }
        finally { gate.Release(); }
    }

    private async Task<PeerIdentity> LoadAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Protected peer identities require Windows.");
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous);
        if (stream.Length is <= 0 or > MaximumBytes) throw new InvalidDataException("Invalid protected identity size.");
        var protectedBytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(protectedBytes, cancellationToken).ConfigureAwait(false);
        var cleartext = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        try { return PeerIdentity.Import(cleartext); }
        finally { CryptographicOperations.ZeroMemory(cleartext); }
    }
}
