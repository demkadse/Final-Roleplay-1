using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace FRP1.Plugin.Ipc;

public sealed class FrpIpcProvider : IDisposable
{
    private readonly List<Action> unregister = [];
    private bool disposed;

    private FrpIpcProvider(IDalamudPluginInterface plugin, FrpIpcSession session)
    {
        try
        {
            Register(plugin.GetIpcProvider<(uint, uint)>("FRP1.ApiVersion"), () => (1u, 0u));
            Register(plugin.GetIpcProvider<int>("FRP1.v1.GetCapabilities"), session.GetCapabilities);
            Register(plugin.GetIpcProvider<int>("FRP1.v1.GetReadiness"), session.GetReadiness);
            Register(plugin.GetIpcProvider<(int, int, int)>("FRP1.v1.GetOwnState"), session.GetOwnState);
            Register(plugin.GetIpcProvider<int, long>("FRP1.v1.SetStatus"), value => session.Request(new(FrpIpcActionKind.SetStatus, value)));
            Register(plugin.GetIpcProvider<int, long>("FRP1.v1.SetAvailability"), value => session.Request(new(FrpIpcActionKind.SetAvailability, value)));
            Register(plugin.GetIpcProvider<long>("FRP1.v1.OpenOwnProfile"), () => session.Request(new(FrpIpcActionKind.OpenOwnProfile)));
            Register(plugin.GetIpcProvider<long, int>("FRP1.v1.GetActionResult"), session.GetActionResult);
        }
        catch (Exception) { Dispose(); throw; }
    }

    public static FrpIpcProvider? Create(IDalamudPluginInterface plugin, FrpIpcSession session)
    {
        try { return new(plugin, session); }
        catch (Exception) { return null; }
    }

    private void Register<T>(ICallGateProvider<T> channel, Func<T> function)
    {
        channel.RegisterFunc(function);
        unregister.Add(channel.UnregisterFunc);
    }

    private void Register<T, TResult>(ICallGateProvider<T, TResult> channel, Func<T, TResult> function)
    {
        channel.RegisterFunc(function);
        unregister.Add(channel.UnregisterFunc);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var remove in unregister.AsEnumerable().Reverse())
            try { remove(); } catch (Exception) { }
        unregister.Clear();
    }
}
