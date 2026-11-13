using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using HousingTuple = (string Name, int World, int City, int Ward, int PropertyType, int Plot, int Apartment,
    bool ApartmentSubdivision, bool AliasEnabled, string Alias);

namespace FRP1.Plugin.Integrations;

// Lifestream 2.6.0.13 / API 15, source b363201. The void EzIPC endpoint is a Dalamud action.
// No address book, command, abort or automatic visit endpoints are acquired.
public sealed class LifestreamAdapter : ILifestreamProvider
{
    private readonly IDalamudPluginInterface plugin;
    private readonly ICallGateSubscriber<bool> busy;
    private readonly ICallGateSubscriber<HousingTuple, object> visit;
    private long epoch;
    private int disposed;

    public LifestreamAdapter(IDalamudPluginInterface plugin)
    {
        this.plugin = plugin;
        busy = plugin.GetIpcSubscriber<bool>("Lifestream.IsBusy");
        visit = plugin.GetIpcSubscriber<HousingTuple, object>("Lifestream.GoToHousingAddress");
        plugin.ActivePluginsChanged += OnPluginsChanged;
    }

    public long Version => Interlocked.Read(ref epoch);
    private void OnPluginsChanged(IActivePluginsChangedEventArgs _) => Interlocked.Increment(ref epoch);

    private IntegrationStatus CheckContract()
    {
        if (Volatile.Read(ref disposed) != 0) return IntegrationStatus.Disabled;
        var installed = plugin.InstalledPlugins.Where(item => item.InternalName == "Lifestream").ToArray();
        if (installed.Length == 0 || !installed.Any(item => item.IsLoaded)) return IntegrationStatus.Missing;
        if (installed.Length != 1 || !LifestreamRules.IsSupportedProvider(installed[0].Version, installed[0].Manifest.DalamudApiLevel))
            return IntegrationStatus.Incompatible;
        return busy.HasFunction && visit.HasAction ? IntegrationStatus.Ready : IntegrationStatus.Missing;
    }

    public IntegrationStatus CheckAvailability()
    {
        try
        {
            var status = CheckContract();
            return status == IntegrationStatus.Ready && busy.InvokeFunc() ? IntegrationStatus.Busy : status;
        }
        catch (Exception) { return IntegrationStatus.Error; }
    }

    public void GoToHousingAddress(HousingAddress address)
    {
        if (CheckContract() != IntegrationStatus.Ready) throw new InvalidOperationException("Lifestream is unavailable.");
        visit.InvokeAction(LifestreamRules.ToTuple(address));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        Interlocked.Increment(ref epoch);
        plugin.ActivePluginsChanged -= OnPluginsChanged;
    }
}
