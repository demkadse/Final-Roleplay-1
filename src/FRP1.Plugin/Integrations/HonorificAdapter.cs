using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace FRP1.Plugin.Integrations;

// Honorific API 3.2. No writable IPC channels are acquired by this adapter.
public sealed class HonorificAdapter : IHonorificReader
{
    private readonly ICallGateSubscriber<(uint, uint)> version;
    private readonly ICallGateSubscriber<string> title;
    private readonly ICallGateSubscriber<object> ready;
    private readonly ICallGateSubscriber<object> disposing;
    private readonly ICallGateSubscriber<string, object> changed;
    private long epoch;
    private int disposed;
    private int providerDisposing;

    public static IHonorificReader Create(IDalamudPluginInterface plugin)
    {
        try { return new HonorificAdapter(plugin); }
        catch (Exception) { return new UnavailableReader(); }
    }

    private sealed class UnavailableReader : IHonorificReader
    {
        public long Version => 0;
        public HonorificReadResult Read() => new(IntegrationStatus.Error);
        public void Dispose() { }
    }

    public HonorificAdapter(IDalamudPluginInterface plugin)
    {
        version = plugin.GetIpcSubscriber<(uint, uint)>("Honorific.ApiVersion");
        title = plugin.GetIpcSubscriber<string>("Honorific.GetLocalCharacterTitle");
        ready = plugin.GetIpcSubscriber<object>("Honorific.Ready");
        disposing = plugin.GetIpcSubscriber<object>("Honorific.Disposing");
        changed = plugin.GetIpcSubscriber<string, object>("Honorific.LocalCharacterTitleChanged");
        try
        {
            ready.Subscribe(OnReady);
            disposing.Subscribe(OnDisposing);
            changed.Subscribe(OnTitleChanged);
        }
        catch (Exception) { Dispose(); throw; }
    }

    public long Version => Interlocked.Read(ref epoch);
    private void OnChanged() => Interlocked.Increment(ref epoch);
    private void OnTitleChanged(string _) => OnChanged();
    private void OnReady() { Volatile.Write(ref providerDisposing, 0); OnChanged(); }
    private void OnDisposing() { Volatile.Write(ref providerDisposing, 1); OnChanged(); }

    public HonorificReadResult Read()
    {
        if (Volatile.Read(ref disposed) != 0 || Volatile.Read(ref providerDisposing) != 0 || !version.HasFunction || !title.HasFunction)
            return new(IntegrationStatus.Missing);
        var api = version.InvokeFunc();
        if (api.Item1 != 3 || api.Item2 < 2) return new(IntegrationStatus.Incompatible);
        return new(IntegrationStatus.Ready, title.InvokeFunc());
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        OnChanged();
        try { ready.Unsubscribe(OnReady); } catch (Exception) { }
        try { disposing.Unsubscribe(OnDisposing); } catch (Exception) { }
        try { changed.Unsubscribe(OnTitleChanged); } catch (Exception) { }
    }
}
