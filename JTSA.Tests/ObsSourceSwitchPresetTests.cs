using System.Text.Json;
using JTSA.Panels;
using Xunit;

namespace JTSA.Tests;

public sealed class ObsSourceSwitchPresetTests
{
    [Fact]
    public void LegacyPreset_StaysSceneSpecific_AndGlobalScopePersists()
    {
        const string legacy = """
            {"AccountId":12,"IsSub":true,"SceneName":"ゲーム","SourceName":"カメラ", "ContainerName":"グループ"}
            """;
        var preset = JsonSerializer.Deserialize<ObsSettingPanel.SourceSwitchPreset>(legacy)!;
        Assert.False(preset.ApplyToAllScenes);
        Assert.Equal("ゲーム / グループ / カメラ", preset.DetailText);
        preset.ApplyToAllScenes = true;
        var restored = JsonSerializer.Deserialize<ObsSettingPanel.SourceSwitchPreset>(JsonSerializer.Serialize(preset))!;
        Assert.True(restored.ApplyToAllScenes);
        Assert.Equal(12, restored.AccountId);
        Assert.True(restored.IsSub);
        Assert.Equal("ゲーム", restored.SceneName);
        Assert.Equal("グループ", restored.ContainerName);
        Assert.Equal("全シーン / カメラ", restored.DetailText);
        restored.ApplyToAllScenes = false;
        Assert.Equal("ゲーム / グループ / カメラ", restored.DetailText);
    }
}
