using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FRP1.Core.Identity;

public sealed class PeerIdentity : IDisposable
{
    public X509Certificate2 Certificate { get; }
    public string Fingerprint => Certificate.GetCertHashString(HashAlgorithmName.SHA256);

    private PeerIdentity(X509Certificate2 certificate) => Certificate = certificate;

    // Windows TLS uses temporary OS-managed handles, without installing a certificate.
    public static PeerIdentity CreateEphemeral() => Create(DateTimeOffset.UtcNow.AddDays(7));

    internal static PeerIdentity CreatePersistent() => Create(DateTimeOffset.UtcNow.AddYears(5));

    private static PeerIdentity Create(DateTimeOffset expires)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=FRP1", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), expires);
        var bytes = certificate.Export(X509ContentType.Pkcs12);
        try { return Import(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal static PeerIdentity Import(ReadOnlySpan<byte> bytes)
    {
        var certificate = X509CertificateLoader.LoadPkcs12(bytes, null, OperatingSystem.IsWindows()
            ? X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable : X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
        if (!certificate.HasPrivateKey || certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow)
        {
            certificate.Dispose();
            throw new CryptographicException("The local peer identity is invalid or expired; explicit identity recovery and re-pairing are required.");
        }
        return new PeerIdentity(certificate);
    }

    public void Dispose() => Certificate.Dispose();
}
