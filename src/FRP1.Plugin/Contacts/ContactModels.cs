using System.Text.Json.Serialization;
using FRP1.Core.Networking;

namespace FRP1.Plugin.Contacts;

public readonly record struct ProfileKey(string Fingerprint, Guid ProfileId)
{
    public ProfileKey Canonical() => new(Fingerprint.ToUpperInvariant(), ProfileId);
}

public readonly record struct CharacterKey(string Name, uint HomeWorldId)
{
    public CharacterKey Canonical() => new(Name.Trim(), HomeWorldId);
}

public enum RelationshipKind { Unspecified, Acquaintance, Friend, Family, Partner, Rival, Custom }
public enum EncounterKind { Seen, ProfileOpened }

public sealed record CharacterBinding(CharacterKey Character, bool Confirmed);

// These records are private local data and never form part of a shared profile.
public sealed record LocalContact
{
    public ProfileKey Profile { get; init; }
    public string Alias { get; init; } = "";
    public string ProfileNote { get; init; } = "";
    public bool Favorite { get; init; }
    public RelationshipKind Relationship { get; init; }
    public string CustomRelationship { get; init; } = "";
    public bool Muted { get; init; }
    public CharacterBinding? Binding { get; init; }
}

public sealed record CharacterNote(CharacterKey Character, string Note);
public sealed record EncounterEntry(ProfileKey Profile, CharacterKey? Character, EncounterKind Kind, DateTimeOffset At);

public sealed record ContactRegister
{
    [JsonRequired] public int SchemaVersion { get; init; } = 1;
    [JsonRequired] public IReadOnlyList<LocalContact> Contacts { get; init; } = Array.Empty<LocalContact>();
    [JsonRequired] public IReadOnlyList<CharacterNote> CharacterNotes { get; init; } = Array.Empty<CharacterNote>();
    [JsonRequired] public IReadOnlyList<string> BlockedPeers { get; init; } = Array.Empty<string>();
    [JsonRequired] public IReadOnlyList<EncounterEntry> Encounters { get; init; } = Array.Empty<EncounterEntry>();
}

public static class ContactData
{
    public const int MaximumContacts = 512;
    public const int MaximumCharacterNotes = 512;
    public const int MaximumBlockedPeers = 128;
    public const int MaximumEncounters = 1024;

    public static void ValidateKey(ProfileKey key)
    {
        if (!PeerInvitation.IsFingerprint(key.Fingerprint) || key.ProfileId == Guid.Empty)
            throw new InvalidDataException("Invalid local profile reference.");
    }

    public static void ValidateCharacter(CharacterKey key)
    {
        Text(key.Name, 80, true, false);
        if (key.HomeWorldId is 0 or > 65535) throw new InvalidDataException("Invalid home world.");
    }

    public static void Validate(ContactRegister register)
    {
        ArgumentNullException.ThrowIfNull(register);
        if (register.SchemaVersion != 1 || register.Contacts is null || register.CharacterNotes is null ||
            register.BlockedPeers is null || register.Encounters is null || register.Contacts.Count > MaximumContacts ||
            register.CharacterNotes.Count > MaximumCharacterNotes || register.BlockedPeers.Count > MaximumBlockedPeers ||
            register.Encounters.Count > MaximumEncounters)
            throw new InvalidDataException("Invalid local register.");
        var profiles = new HashSet<ProfileKey>();
        foreach (var contact in register.Contacts)
        {
            if (contact is null) throw new InvalidDataException("Invalid local contact.");
            ValidateKey(contact.Profile);
            if (!profiles.Add(contact.Profile.Canonical())) throw new InvalidDataException("Duplicate contact.");
            Text(contact.Alias, 160, false, false);
            Text(contact.ProfileNote, 8192);
            Text(contact.CustomRelationship, 80, false, false);
            if (!Enum.IsDefined(contact.Relationship) || (contact.Relationship == RelationshipKind.Custom &&
                string.IsNullOrWhiteSpace(contact.CustomRelationship)))
                throw new InvalidDataException("Invalid relationship.");
            if (contact.Binding is { } binding)
            {
                ValidateCharacter(binding.Character);
                if (!binding.Confirmed) throw new InvalidDataException("Unconfirmed character binding.");
            }
        }
        var characters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var note in register.CharacterNotes)
        {
            if (note is null) throw new InvalidDataException("Invalid character note.");
            ValidateCharacter(note.Character);
            Text(note.Note, 8192);
            if (!characters.Add(CharacterIndex(note.Character))) throw new InvalidDataException("Duplicate character note.");
        }
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var peer in register.BlockedPeers)
            if (!PeerInvitation.IsFingerprint(peer) || !blocked.Add(peer))
                throw new InvalidDataException("Invalid blocked contact.");
        foreach (var entry in register.Encounters)
        {
            if (entry is null) throw new InvalidDataException("Invalid encounter.");
            ValidateKey(entry.Profile);
            if (entry.Character is { } character) ValidateCharacter(character);
            if (!Enum.IsDefined(entry.Kind) || entry.At < DateTimeOffset.UnixEpoch || entry.At > DateTimeOffset.UtcNow.AddMinutes(5))
                throw new InvalidDataException("Invalid encounter time.");
        }
    }

    public static ContactRegister Copy(ContactRegister register)
    {
        Validate(register);
        return register with
        {
            Contacts = Array.AsReadOnly(register.Contacts.Select(c => c with
            {
                Profile = c.Profile.Canonical(),
                Binding = c.Binding is { } binding ? binding with { Character = binding.Character.Canonical() } : null,
            }).ToArray()),
            CharacterNotes = Array.AsReadOnly(register.CharacterNotes.Select(n => n with { Character = n.Character.Canonical() }).ToArray()),
            BlockedPeers = Array.AsReadOnly(register.BlockedPeers.Select(p => p.ToUpperInvariant()).ToArray()),
            Encounters = Array.AsReadOnly(register.Encounters.Select(e => e with
            {
                Profile = e.Profile.Canonical(), Character = e.Character?.Canonical(),
            }).ToArray()),
        };
    }

    public static bool SameCharacter(CharacterKey first, CharacterKey second) => first.HomeWorldId == second.HomeWorldId &&
        string.Equals(first.Name.Trim(), second.Name.Trim(), StringComparison.OrdinalIgnoreCase);

    public static string CharacterIndex(CharacterKey key) => key.HomeWorldId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + key.Name.Trim();

    private static void Text(string? value, int maximum, bool required = false, bool multiline = true)
    {
        if (value is null || value.Length > maximum || (required && string.IsNullOrWhiteSpace(value)) ||
            value.Any(c => char.IsControl(c) && !(multiline && (c is '\n' or '\r' or '\t'))))
            throw new InvalidDataException("Invalid local text.");
    }
}
