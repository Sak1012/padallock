namespace Padallock;

public sealed record AppSettings(
    bool IsEnabled,
    bool ShowTrackDetails,
    string? OriginalLockScreenPath,
    string? CurrentGeneratedImagePath);
