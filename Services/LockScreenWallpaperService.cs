using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.UserProfile;
using System.Security.Cryptography;

namespace Padallock;

public sealed class LockScreenWallpaperService
{
    private const string OriginalImageName = "original-lock-screen.jpg";

    public Task<string?> CaptureOriginalImageAsync()
    {
        return CaptureOriginalImageCoreAsync();
    }

    public async Task<bool> ApplyAsync(string imagePath)
    {
        var imageFile = await StorageFile.GetFileFromPathAsync(imagePath);
        return await UserProfilePersonalizationSettings.Current.TrySetLockScreenImageAsync(imageFile);
    }

    public async Task<bool> RestoreOriginalAsync(string? originalImagePath, string? managedImagePath)
    {
        if (string.IsNullOrWhiteSpace(originalImagePath) ||
            !File.Exists(originalImagePath) ||
            string.IsNullOrWhiteSpace(managedImagePath) ||
            !File.Exists(managedImagePath) ||
            !await IsCurrentImageManagedAsync(managedImagePath))
        {
            return false;
        }

        return await ApplyAsync(originalImagePath);
    }

    private static async Task<string?> CaptureOriginalImageCoreAsync()
    {
        var originalPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, OriginalImageName);
        if (File.Exists(originalPath))
        {
            return originalPath;
        }

        using var source = LockScreen.GetImageStream();
        if (source is null)
        {
            return null;
        }

        var target = await ApplicationData.Current.LocalFolder.CreateFileAsync(
            OriginalImageName,
            CreationCollisionOption.ReplaceExisting);
        using var destination = await target.OpenAsync(FileAccessMode.ReadWrite);
        await RandomAccessStream.CopyAsync(source.GetInputStreamAt(0), destination.GetOutputStreamAt(0));
        await destination.FlushAsync();
        return target.Path;
    }

    private static async Task<bool> IsCurrentImageManagedAsync(string managedImagePath)
    {
        using var currentImage = LockScreen.GetImageStream();
        if (currentImage is null)
        {
            return false;
        }

        var currentBytes = await ReadBytesAsync(currentImage);
        var managedBytes = await File.ReadAllBytesAsync(managedImagePath);
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(currentBytes),
            SHA256.HashData(managedBytes));
    }

    private static async Task<byte[]> ReadBytesAsync(IRandomAccessStream stream)
    {
        using var reader = new DataReader(stream);
        await reader.LoadAsync(checked((uint)stream.Size));
        var bytes = new byte[reader.UnconsumedBufferLength];
        reader.ReadBytes(bytes);
        return bytes;
    }
}
