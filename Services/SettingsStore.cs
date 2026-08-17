using System.Text.Json;
using Windows.Storage;

namespace Padallock;

public sealed class SettingsStore
{
    private const string FileName = "settings.json";
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public async Task<AppSettings> LoadAsync()
    {
        var file = await ApplicationData.Current.LocalFolder.TryGetItemAsync(FileName) as StorageFile;
        if (file is null)
        {
            return new AppSettings(false, false, null, null);
        }

        var json = await FileIO.ReadTextAsync(file);
        return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions)
            ?? new AppSettings(false, false, null, null);
    }

    public async Task SaveAsync(AppSettings settings)
    {
        var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
            FileName,
            CreationCollisionOption.ReplaceExisting);
        await FileIO.WriteTextAsync(file, JsonSerializer.Serialize(settings, SerializerOptions));
    }
}
