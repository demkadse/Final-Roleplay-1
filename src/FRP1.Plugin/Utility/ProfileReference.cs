namespace FRP1.Plugin.Utility;

public sealed record ProfileReference(string Fingerprint, Guid ProfileId)
{
    private const string Prefix = "frp1-profile:";
    public const int EncodedLength = 13 + 64 + 1 + 32;

    public string Format()
    {
        if (!FRP1.Core.Networking.PeerInvitation.IsFingerprint(Fingerprint) || ProfileId == Guid.Empty)
            throw new InvalidDataException("Invalid local profile reference.");
        return Prefix + Fingerprint.ToUpperInvariant() + ":" + ProfileId.ToString("N");
    }

    public static ProfileReference Parse(string value) => TryParse(value, out var reference)
        ? reference! : throw new InvalidDataException("Invalid local profile reference.");

    public static bool TryParse(string? value, out ProfileReference? reference)
    {
        reference = null;
        if (value is null || value.Length != EncodedLength || !value.StartsWith(Prefix, StringComparison.Ordinal) ||
            value[Prefix.Length + 64] != ':') return false;
        var fingerprint = value.Substring(Prefix.Length, 64);
        if (!FRP1.Core.Networking.PeerInvitation.IsFingerprint(fingerprint) ||
            !Guid.TryParseExact(value.AsSpan(Prefix.Length + 65), "N", out var profileId) || profileId == Guid.Empty) return false;
        reference = new ProfileReference(fingerprint.ToUpperInvariant(), profileId);
        return true;
    }
}
