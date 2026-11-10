using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FRP1.Core.Networking;

public sealed record PeerInvitation([property: JsonRequired] int ProtocolVersion, [property: JsonRequired] string Address,
    [property: JsonRequired] int Port, [property: JsonRequired] string Fingerprint)
{
    public const string PrivacyNotice = "Direkte Verbindungen machen die IP-Adressen für den jeweiligen Kontakt sichtbar. Einladungen nur über einen vertrauenswürdigen Weg austauschen.";

    public void Validate()
    {
        if (ProtocolVersion != 1 || !IPAddress.TryParse(Address, out var ip) || ip.Equals(IPAddress.Any) ||
            ip.Equals(IPAddress.IPv6Any) || ip.IsIPv6Multicast || (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && ip.GetAddressBytes()[0] >= 224) ||
            Port is < 1 or > 65535 || !IsFingerprint(Fingerprint))
            throw new InvalidDataException("Invalid or unsupported peer invitation.");
    }

    public string Encode()
    {
        Validate();
        return "frp1:" + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(this, Profiles.ProfileJson.Options));
    }

    public static PeerInvitation Parse(string code)
    {
        if (code is null || code.Length > 2048 || !code.StartsWith("frp1:", StringComparison.Ordinal))
            throw new InvalidDataException("Invalid invitation code.");
        try
        {
            var invitation = JsonSerializer.Deserialize<PeerInvitation>(Convert.FromBase64String(code[5..]), Profiles.ProfileJson.Options);
            if (invitation is null) throw new InvalidDataException("Invalid invitation code.");
            invitation.Validate();
            return invitation;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        { throw new InvalidDataException("Invalid invitation code.", ex); }
    }

    public static bool IsFingerprint(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
