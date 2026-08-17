using System.Runtime.InteropServices;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Padallock;

public sealed class MediaSessionService : IDisposable
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly Dictionary<GlobalSystemMediaTransportControlsSession, DateTimeOffset> _activity = [];
    private GlobalSystemMediaTransportControlsSessionManager? _manager;

    public event Func<MediaArtwork?, Task>? ArtworkChanged;
    public event EventHandler<PadallockStatus>? StatusChanged;

    public async Task StartAsync()
    {
        if (_manager is null)
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.SessionsChanged += Manager_SessionsChanged;
            _manager.CurrentSessionChanged += Manager_CurrentSessionChanged;
        }

        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (_manager is null || !await _refreshLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var sessions = _manager.GetSessions().ToArray();
            SubscribeToSessions(sessions);
            var playback = sessions
                .Select(session => (Session: session, Info: session.GetPlaybackInfo()))
                .ToArray();
            var active = playback
                .Where(item => item.Info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                .OrderByDescending(item => _activity.GetValueOrDefault(item.Session))
                .Select(item => item.Session)
                .FirstOrDefault();

            if (active is null)
            {
                if (playback.All(item => item.Info?.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused))
                {
                    await PublishArtworkAsync(null);
                    Report("No usable media session is active.", "No active media session detected.");
                }

                return;
            }

            _activity[active] = DateTimeOffset.UtcNow;
            var properties = await active.TryGetMediaPropertiesAsync();
            if (properties?.Thumbnail is null)
            {
                await PublishArtworkAsync(null);
                Report("The active media session did not provide artwork.", Describe(properties));
                return;
            }

            var bytes = await ReadThumbnailAsync(properties.Thumbnail);
            await PublishArtworkAsync(new MediaArtwork(bytes, properties.Title, properties.Artist));
            Report("Updated from the active media session.", Describe(properties));
        }
        catch (COMException exception)
        {
            Report($"Windows media-session access failed: {exception.Message}", "Media session unavailable.", true);
        }
        catch (UnauthorizedAccessException exception)
        {
            Report($"Media artwork access was denied: {exception.Message}", "Media artwork unavailable.", true);
        }
        catch (IOException exception)
        {
            Report($"Media artwork could not be read: {exception.Message}", "Media artwork unavailable.", true);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Dispose()
    {
        if (_manager is not null)
        {
            _manager.SessionsChanged -= Manager_SessionsChanged;
            _manager.CurrentSessionChanged -= Manager_CurrentSessionChanged;
        }

        foreach (var session in _activity.Keys)
        {
            session.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
            session.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
        }

        _refreshLock.Dispose();
    }

    private void SubscribeToSessions(IEnumerable<GlobalSystemMediaTransportControlsSession> sessions)
    {
        foreach (var session in sessions.Where(session => !_activity.ContainsKey(session)))
        {
            _activity.Add(session, DateTimeOffset.MinValue);
            session.MediaPropertiesChanged += Session_MediaPropertiesChanged;
            session.PlaybackInfoChanged += Session_PlaybackInfoChanged;
        }
    }

    private void Manager_SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args) => _ = RefreshAsync();

    private void Manager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) => _ = RefreshAsync();

    private void Session_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _activity[sender] = DateTimeOffset.UtcNow;
        _ = RefreshAsync();
    }

    private void Session_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        _activity[sender] = DateTimeOffset.UtcNow;
        _ = RefreshAsync();
    }

    private async Task PublishArtworkAsync(MediaArtwork? artwork)
    {
        if (ArtworkChanged is null)
        {
            return;
        }

        foreach (var handler in ArtworkChanged.GetInvocationList().Cast<Func<MediaArtwork?, Task>>())
        {
            await handler(artwork);
        }
    }

    private static async Task<byte[]> ReadThumbnailAsync(IRandomAccessStreamReference thumbnail)
    {
        using var stream = await thumbnail.OpenReadAsync();
        using var reader = new DataReader(stream);
        await reader.LoadAsync(checked((uint)stream.Size));
        var bytes = new byte[reader.UnconsumedBufferLength];
        reader.ReadBytes(bytes);
        return bytes;
    }

    private void Report(string message, string playbackDescription, bool isError = false) =>
        StatusChanged?.Invoke(this, new PadallockStatus(message, playbackDescription, isError));

    private static string Describe(GlobalSystemMediaTransportControlsSessionMediaProperties? properties) =>
        string.IsNullOrWhiteSpace(properties?.Title)
            ? "Active media session detected."
            : string.IsNullOrWhiteSpace(properties.Artist)
                ? properties.Title
                : $"{properties.Title} — {properties.Artist}";
}
