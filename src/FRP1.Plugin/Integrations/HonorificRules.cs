using System.Text;
using System.Text.Json;
using FRP1.Core.Profiles;

namespace FRP1.Plugin.Integrations;

public static class HonorificRules
{
    public const int MaximumPayloadBytes = 8192;
    public const int MaximumTitleLength = 256;
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
    private static readonly JsonDocumentOptions Options = new() { MaxDepth = 6 };

    public static bool TryReadTitle(string? json, out string title)
    {
        title = "";
        // The provider returns an empty string when no local title is selected.
        if (json == "") return true;
        if (json is null || json.Length > MaximumPayloadBytes)
            return false;
        try
        {
            if (StrictUtf8.GetByteCount(json) > MaximumPayloadBytes) return false;
            using var document = JsonDocument.Parse(json, Options);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !UniqueProperties(root) || !root.TryGetProperty("Title", out var value) ||
                value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)) return false;
            var candidate = value.GetString() ?? "";
            if (candidate.Length > MaximumTitleLength || candidate.Any(char.IsControl)) return false;
            // Reuse the canonical profile validator; external formatting/commands remain plain text.
            ProfileJson.Validate(new ProfileDocument { DisplayName = "Honorific", Title = candidate });
            title = candidate;
            return true;
        }
        // JsonDocument decodes escaped UTF-16 lazily in GetString and Property.Name.
        catch (Exception ex) when (ex is JsonException or InvalidDataException or ArgumentException or InvalidOperationException) { return false; }
    }

    // The parser bounds depth and bytes; inspect ignored decoration fields too.
    private static bool UniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
                if (!names.Add(property.Name) || !UniqueProperties(property.Value)) return false;
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray())
                if (!UniqueProperties(item)) return false;
        return true;
    }

    public static bool YieldOwnTitle(bool enabled, IntegrationStatus status, IntegrationActor confirmed,
        IntegrationActor current, ulong plateObjectId, long plateAddress) =>
        enabled && status == IntegrationStatus.Ready && confirmed.IsValid && confirmed == current &&
        current.ObjectId == plateObjectId && current.Address == plateAddress;
}
