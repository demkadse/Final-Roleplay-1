using System.Text.Json.Serialization;

namespace FRP1.Plugin.Integrations;

public enum HousingDistrict { Goblet = 9, LavenderBeds = 2, Mist = 8, Empyreum = 70, Shirogane = 111 }
public enum HousingPropertyType { House = 0, Apartment = 1 }

// One local destination, never part of a public profile or a command string.
public sealed record HousingAddress(
    [property: JsonRequired] int WorldId,
    [property: JsonRequired] HousingDistrict District,
    [property: JsonRequired] int Ward,
    [property: JsonRequired] HousingPropertyType PropertyType,
    int Plot = 1, int Apartment = 1, bool ApartmentSubdivision = false);

public sealed record HousingWorld(int WorldId, string Name, int DataCenterId, bool IsPublic = true);
public sealed record LifestreamOptions(bool Enabled = false, HousingAddress? Address = null);
public sealed record LifestreamContext(IntegrationContext Integration, uint CurrentWorldId, long SceneEpoch,
    long SettingsGeneration = 0);
public enum LifestreamActionResult { None, Queued, HandedOff, Disabled, NotReady, Blocked, Stale, RateLimited, AlreadyPending }

public interface ILifestreamProvider : IDisposable
{
    long Version { get; }
    // Includes the exact runtime contract gate and a fresh IsBusy read.
    IntegrationStatus CheckAvailability();
    void GoToHousingAddress(HousingAddress address);
}
