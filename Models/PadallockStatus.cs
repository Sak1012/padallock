namespace Padallock;

public sealed record PadallockStatus(string Message, string PlaybackDescription, bool IsError = false);
