namespace FRP1.Plugin.Integrations;

public static class LifestreamRules
{
    public const string SupportedVersion = "2.6.0.13";
    public const int SupportedDalamudApi = 15;

    public static bool IsSupportedProvider(Version? version, int apiLevel) =>
        version == new Version(SupportedVersion) && apiLevel == SupportedDalamudApi;

    public static bool IsPublicWorld(HousingWorld? world) => world is { WorldId: > 0, DataCenterId: > 0, IsPublic: true } &&
        !string.IsNullOrWhiteSpace(world.Name);

    public static bool IsValidAddress(HousingAddress? address) => address is not null && address.WorldId > 0 &&
        Enum.IsDefined(address.District) && Enum.IsDefined(address.PropertyType) && address.Ward is >= 1 and <= 30 &&
        address.Plot is >= 1 and <= 60 && address.Apartment is >= 1 and <= 90;

    public static HousingAddress Canonicalize(HousingAddress address) => address.PropertyType == HousingPropertyType.House
        ? address with { Apartment = 1, ApartmentSubdivision = false } : address with { Plot = 1 };

    public static LifestreamOptions ValidateOptions(LifestreamOptions value)
    {
        if (value is null || value.Address is not null && !IsValidAddress(value.Address))
            throw new InvalidDataException("Invalid local housing destination.");
        return value.Address is null ? value : value with { Address = Canonicalize(value.Address) };
    }

    public static bool WorldExists(HousingAddress address, IReadOnlyList<HousingWorld>? worlds) =>
        worlds?.Any(world => world.WorldId == address.WorldId && IsPublicWorld(world)) == true;

    public static (string Name, int World, int City, int Ward, int PropertyType, int Plot, int Apartment,
        bool ApartmentSubdivision, bool AliasEnabled, string Alias) ToTuple(HousingAddress address)
    {
        if (!IsValidAddress(address)) throw new ArgumentException("Invalid housing destination.", nameof(address));
        var canonical = Canonicalize(address);
        return ("", canonical.WorldId, (int)canonical.District, canonical.Ward, (int)canonical.PropertyType,
            canonical.Plot, canonical.Apartment, canonical.ApartmentSubdivision, false, "");
    }
}
