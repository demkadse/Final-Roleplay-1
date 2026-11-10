using System.Security.Cryptography;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using FRP1.Core.Profiles;

namespace FRP1.Plugin.UiProfiles;

internal sealed class PortraitTexture(ITextureProvider textures) : IDisposable
{
    private Task<IDalamudTextureWrap>? pending;
    private CancellationTokenSource? cancellation;
    private PortraitThumbnail? desired;
    private string? desiredHash;
    private string? activeHash;
    private string? pendingHash;
    private IDalamudTextureWrap? current;
    private bool disposed;

    public IDalamudTextureWrap? Current => current;

    public void Set(PortraitThumbnail? portrait)
    {
        if (disposed) return;
        var hash = portrait is null ? null : Convert.ToHexString(SHA256.HashData(portrait.Data));
        if (hash == desiredHash) return;
        desiredHash = hash;
        desired = portrait is null ? null : portrait with { Data = (byte[])portrait.Data.Clone() };
        cancellation?.Cancel();
        current?.Dispose();
        current = null;
        activeHash = null;
    }

    public void Poll()
    {
        if (disposed) return;
        if (pending is { IsCompleted: true } completed)
        {
            pending = null;
            try
            {
                var wrap = completed.GetAwaiter().GetResult();
                if (pendingHash == desiredHash) { current = wrap; activeHash = pendingHash; }
                else wrap.Dispose();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            { if (pendingHash == desiredHash) activeHash = desiredHash; }
            finally { cancellation?.Dispose(); cancellation = null; }
        }
        if (pending is not null || desired is null || desiredHash == activeHash) return;
        cancellation = new CancellationTokenSource();
        pendingHash = desiredHash;
        pending = textures.CreateFromImageAsync(desired.Data, "FRP1 portrait", cancellation.Token);
    }

    public void Clear() => Set(null);

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        cancellation?.Cancel();
        current?.Dispose();
        current = null;
        desired = null;
        var source = cancellation;
        if (pending is not null)
            _ = pending.ContinueWith(task =>
            {
                if (task.Status == TaskStatus.RanToCompletion) task.Result.Dispose();
                _ = task.Exception;
                source?.Dispose();
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        else source?.Dispose();
    }
}
