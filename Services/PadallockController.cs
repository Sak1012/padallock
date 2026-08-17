using System.Runtime.InteropServices;
using Windows.ApplicationModel;

namespace Padallock;

public sealed class PadallockController : IDisposable
{
    private readonly SettingsStore _settingsStore;
    private readonly MediaSessionService _mediaSessionService;
    private readonly LockScreenWallpaperService _wallpaperService;
    private readonly ArtworkRenderer _artworkRenderer;
    private bool _initialized;

    public PadallockController(
        SettingsStore settingsStore,
        MediaSessionService mediaSessionService,
        LockScreenWallpaperService wallpaperService,
        ArtworkRenderer artworkRenderer)
    {
        _settingsStore = settingsStore;
        _mediaSessionService = mediaSessionService;
        _wallpaperService = wallpaperService;
        _artworkRenderer = artworkRenderer;
        _mediaSessionService.ArtworkChanged += ApplyArtworkAsync;
        _mediaSessionService.StatusChanged += (_, status) => StatusChanged?.Invoke(this, status);
    }

    public AppSettings Settings { get; private set; } = new(false, false, null, null);
    public event EventHandler<PadallockStatus>? StatusChanged;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        Settings = await _settingsStore.LoadAsync();
        _initialized = true;
        if (Settings.IsEnabled)
        {
            await StartAsync();
        }
        else
        {
            Report("Enable Padallock to begin using album artwork on your lock screen.", "Padallock is off.");
        }
    }

    public async Task SetEnabledAsync(bool enabled)
    {
        if (enabled)
        {
            var originalImagePath = await _wallpaperService.CaptureOriginalImageAsync();
            Settings = Settings with { IsEnabled = true, OriginalLockScreenPath = originalImagePath ?? Settings.OriginalLockScreenPath };
            await _settingsStore.SaveAsync(Settings);
            await EnableStartupAsync();
            await StartAsync();
            return;
        }

        await _wallpaperService.RestoreOriginalAsync(Settings.OriginalLockScreenPath, Settings.CurrentGeneratedImagePath);
        DeleteManagedImage(Settings.CurrentGeneratedImagePath);
        Settings = Settings with { IsEnabled = false, CurrentGeneratedImagePath = null };
        await _settingsStore.SaveAsync(Settings);
        await DisableStartupAsync();
        Report("Padallock is disabled. Your prior static lock-screen image was restored when available.", "Padallock is off.");
    }

    public async Task SetShowTrackDetailsAsync(bool showTrackDetails)
    {
        Settings = Settings with { ShowTrackDetails = showTrackDetails };
        await _settingsStore.SaveAsync(Settings);
        await RefreshAsync();
    }

    public Task RefreshAsync() => Settings.IsEnabled ? _mediaSessionService.RefreshAsync() : Task.CompletedTask;

    public void Dispose() => _mediaSessionService.Dispose();

    private async Task StartAsync()
    {
        try
        {
            await _mediaSessionService.StartAsync();
        }
        catch (COMException exception)
        {
            Report($"Windows media-session access failed: {exception.Message}", "Media session unavailable.", true);
        }
        catch (UnauthorizedAccessException exception)
        {
            Report($"Windows denied media-session access: {exception.Message}", "Media session unavailable.", true);
        }
    }

    private async Task ApplyArtworkAsync(MediaArtwork? artwork)
    {
        if (!Settings.IsEnabled)
        {
            return;
        }

        if (artwork is null)
        {
            await _wallpaperService.RestoreOriginalAsync(Settings.OriginalLockScreenPath, Settings.CurrentGeneratedImagePath);
            DeleteManagedImage(Settings.CurrentGeneratedImagePath);
            Settings = Settings with { CurrentGeneratedImagePath = null };
            await _settingsStore.SaveAsync(Settings);
            return;
        }

        var imagePath = await _artworkRenderer.RenderAsync(
            artwork,
            Settings.ShowTrackDetails,
            Windows.Storage.ApplicationData.Current.LocalFolder.Path);
        if (!await _wallpaperService.ApplyAsync(imagePath))
        {
            File.Delete(imagePath);
            Report("Windows could not apply the generated lock-screen image.", "Artwork is ready but was not applied.", true);
            return;
        }

        DeleteManagedImage(Settings.CurrentGeneratedImagePath);
        Settings = Settings with { CurrentGeneratedImagePath = imagePath };
        await _settingsStore.SaveAsync(Settings);
    }

    private static async Task EnableStartupAsync()
    {
        var startupTask = await StartupTask.GetAsync("PadallockStartup");
        if (startupTask.State == StartupTaskState.Disabled)
        {
            await startupTask.RequestEnableAsync();
        }
    }

    private static async Task DisableStartupAsync()
    {
        var startupTask = await StartupTask.GetAsync("PadallockStartup");
        if (startupTask.State == StartupTaskState.Enabled)
        {
            startupTask.Disable();
        }
    }

    private static void DeleteManagedImage(string? imagePath)
    {
        if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
        {
            File.Delete(imagePath);
        }
    }

    private void Report(string message, string playbackDescription, bool isError = false) =>
        StatusChanged?.Invoke(this, new PadallockStatus(message, playbackDescription, isError));
}
