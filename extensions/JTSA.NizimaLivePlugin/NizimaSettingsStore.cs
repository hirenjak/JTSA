using System.IO;
using System.Text.Json;

namespace JTSA.NizimaLivePlugin;

public static class NizimaSettingsStore
{
    public static NizimaLiveSettings Load(string directory)
    {
        var path = Path.Combine(directory, "settings.json");
        if (!File.Exists(path))
            return new NizimaLiveSettings();
        try
        {
            return JsonSerializer.Deserialize<NizimaLiveSettings>(File.ReadAllText(path), NizimaJson.Options)
                ?? new NizimaLiveSettings();
        }
        catch
        {
            return new NizimaLiveSettings();
        }
    }

    public static void Save(string directory, NizimaLiveSettings settings)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "settings.json"),
            JsonSerializer.Serialize(settings, NizimaJson.Options));
    }
}
