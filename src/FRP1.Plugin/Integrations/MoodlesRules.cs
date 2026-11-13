using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FRP1.Core.Profiles;
using FRP1.Core.Safety;

namespace FRP1.Plugin.Integrations;

public static class MoodlesRules
{
    public const int SourceSchemaVersion = 2;
    public const int MaximumStatuses = 128;
    public const int MaximumSourceStringLength = 4096;
    public const int MaximumSourceBytes = 64 * 1024;
    public const int MaximumBindings = 5;
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    public static bool TryCreateStatus(int sourceVersion, Guid id, int iconId, string? label, string? description,
        out MoodlesStatus? status)
    {
        status = null;
        if (sourceVersion != SourceSchemaVersion || id == Guid.Empty || iconId is < 0 or > 1_000_000 ||
            !ValidText(label) || !ValidText(description)) return false;
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new { SourceSchemaVersion, Label = label, Description = description, IconId = iconId })));
        status = new(id, label!, description!, (uint)iconId, hash);
        return true;
    }

    private static bool ValidText(string? value)
    {
        if (value is null || value.Length > MaximumSourceStringLength || value.Any(char.IsControl)) return false;
        try { StrictUtf8.GetByteCount(value); return true; }
        catch (EncoderFallbackException) { return false; }
    }

    public static bool TryValidateStatuses(IReadOnlyList<MoodlesStatus>? source, out IReadOnlyList<MoodlesStatus> statuses)
    {
        statuses = Array.Empty<MoodlesStatus>();
        if (source is null || source.Count > MaximumStatuses) return false;
        var detached = new List<MoodlesStatus>(source.Count);
        var ids = new HashSet<Guid>();
        var bytes = 0;
        foreach (var item in source)
        {
            if (item is null || item.IconId > int.MaxValue || !ids.Add(item.StatusId) ||
                !TryCreateStatus(SourceSchemaVersion, item.StatusId, (int)item.IconId, item.Label, item.Description, out var valid) ||
                valid!.ContentHash != item.ContentHash) return false;
            bytes += StrictUtf8.GetByteCount(item.Label) + StrictUtf8.GetByteCount(item.Description);
            if (bytes > MaximumSourceBytes) return false;
            detached.Add(valid);
        }
        statuses = detached.AsReadOnly();
        return true;
    }

    public static MoodlesOptions ValidateOptions(MoodlesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var bindings = options.Bindings ?? Array.Empty<MoodlesBinding>();
        if (bindings.Count > MaximumBindings) throw new InvalidDataException("Too many local Moodle selections.");
        var ids = new HashSet<Guid>();
        foreach (var binding in bindings)
            if (binding is null || binding.StatusId == Guid.Empty || !ids.Add(binding.StatusId) ||
                binding.ContentHash is not { Length: 64 } || binding.ContentHash.Any(c => !char.IsAsciiHexDigitUpper(c)) ||
                (binding.Rating is { } rating && !Enum.IsDefined(rating)))
                throw new InvalidDataException("Invalid local Moodle selection.");
        return options with { Bindings = bindings.Count == 0 ? Array.Empty<MoodlesBinding>() : Array.AsReadOnly(bindings.ToArray()) };
    }

    public static ContentRating? ReviewedRating(MoodlesStatus status, MoodlesOptions options) =>
        options.Bindings?.FirstOrDefault(binding => binding.StatusId == status.StatusId && binding.ContentHash == status.ContentHash)?.Rating;

    public static bool AdultOptIn(AudiencePreferences audience) =>
        audience.Selection == AudienceSelection.Adult && audience.MatureContentOptIn;

    public static bool CanPreview(MoodlesStatus status, MoodlesOptions options, AudiencePreferences audience) =>
        audience.Selection != AudienceSelection.Unspecified &&
        (ReviewedRating(status, options) == ContentRating.General || AdultOptIn(audience));

    public static bool CanCopy(MoodlesStatus status, ContentRating? rating, ContentRating target, AudiencePreferences audience) =>
        ValidText(status.Label) && ValidText(status.Description) && !string.IsNullOrWhiteSpace(status.Label) &&
        status.Label.Length <= 80 && status.Description.Length <= 1024 && status.IconId <= 1_000_000 &&
        Enum.IsDefined(target) && audience.Selection != AudienceSelection.Unspecified &&
        (rating == ContentRating.General || rating == ContentRating.Mature18 && target == ContentRating.Mature18 && AdultOptIn(audience));
}
