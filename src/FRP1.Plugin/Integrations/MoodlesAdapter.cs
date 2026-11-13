using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using MoodlesStatusTuple = (int Version, System.Guid GUID, int IconID, string Title, string Description,
    string CustomVFXPath, long ExpireTicks, int Type, int Stacks, int StackSteps, uint Modifiers,
    System.Guid ChainedStatus, int ChainTrigger, string Applier, string Dispeller, bool Permanent);

namespace FRP1.Plugin.Integrations;

// API 4 / status schema 2. Dalamud's return-type JSON bridge converts the provider's enum slots to ints.
// Only active statuses of the current local actor are read; no mutation channels are acquired.
public sealed class MoodlesAdapter : IMoodlesReader
{
    private readonly ICallGateSubscriber<int> api;
    private readonly ICallGateSubscriber<List<MoodlesStatusTuple>> local;
    private readonly ICallGateSubscriber<object> ready;
    private readonly ICallGateSubscriber<object> unloading;
    private readonly ICallGateSubscriber<nint, object> modified;
    private readonly ICallGateSubscriber<Guid, bool, object> updated;
    private long epoch;
    private int disposed;
    private int providerUnloading;
    private bool initializationFailed;
    private long ownAddress;
    private Guid[] knownIds = Array.Empty<Guid>();

    public MoodlesAdapter(IDalamudPluginInterface plugin)
    {
        api = plugin.GetIpcSubscriber<int>("Moodles.Version");
        local = plugin.GetIpcSubscriber<List<MoodlesStatusTuple>>("Moodles.GetClientStatusManagerInfoV2");
        ready = plugin.GetIpcSubscriber<object>("Moodles.Ready");
        unloading = plugin.GetIpcSubscriber<object>("Moodles.Unloading");
        modified = plugin.GetIpcSubscriber<nint, object>("Moodles.StatusManagerModified");
        updated = plugin.GetIpcSubscriber<Guid, bool, object>("Moodles.StatusUpdated");
        try
        {
            ready.Subscribe(OnReady); unloading.Subscribe(OnUnloading);
            modified.Subscribe(OnModified); updated.Subscribe(OnUpdated);
        }
        catch (Exception) { initializationFailed = true; Unsubscribe(); }
    }

    public long Version => Interlocked.Read(ref epoch);
    private void OnChanged() => Interlocked.Increment(ref epoch);
    private void OnReady() { Volatile.Write(ref providerUnloading, 0); OnChanged(); }
    private void OnUnloading() { Volatile.Write(ref providerUnloading, 1); OnChanged(); }
    public void ObserveActor(IntegrationActor actor) => Interlocked.Exchange(ref ownAddress, actor.IsValid ? actor.Address : 0);
    private void OnModified(nint address) { if ((long)address != 0 && (long)address == Interlocked.Read(ref ownAddress)) OnChanged(); }
    private void OnUpdated(Guid id, bool _) { if (Volatile.Read(ref knownIds).Contains(id)) OnChanged(); }

    public MoodlesReadResult ReadLocal()
    {
        if (Volatile.Read(ref disposed) != 0 || initializationFailed) return new(IntegrationStatus.Error);
        if (Volatile.Read(ref providerUnloading) != 0 || !api.HasFunction || !local.HasFunction) return new(IntegrationStatus.Missing);
        try
        {
            if (api.InvokeFunc() != 4) return new(IntegrationStatus.Incompatible);
            // IPC may allocate its return value before FRP1 can enforce bounds. Never retain the raw tuple list.
            var source = local.InvokeFunc();
            if (source is null || source.Count > MoodlesRules.MaximumStatuses) return new(IntegrationStatus.Error);
            var detached = new List<MoodlesStatus>(source.Count);
            foreach (var tuple in source)
            {
                if (tuple.Version != MoodlesRules.SourceSchemaVersion) return new(IntegrationStatus.Incompatible);
                if (!MoodlesRules.TryCreateStatus(tuple.Version, tuple.GUID, tuple.IconID, tuple.Title, tuple.Description, out var status))
                    return new(IntegrationStatus.Error);
                detached.Add(status!);
            }
            if (!MoodlesRules.TryValidateStatuses(detached, out var valid)) return new(IntegrationStatus.Error);
            Volatile.Write(ref knownIds, valid.Select(status => status.StatusId).ToArray());
            return new(IntegrationStatus.Ready, valid);
        }
        catch (Exception) { return new(IntegrationStatus.Error); }
    }

    private void Unsubscribe()
    {
        try { ready.Unsubscribe(OnReady); } catch (Exception) { }
        try { unloading.Unsubscribe(OnUnloading); } catch (Exception) { }
        try { modified.Unsubscribe(OnModified); } catch (Exception) { }
        try { updated.Unsubscribe(OnUpdated); } catch (Exception) { }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        OnChanged(); Unsubscribe();
    }
}
