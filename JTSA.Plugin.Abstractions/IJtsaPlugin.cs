namespace JTSA.Plugin.Abstractions;

/// <summary>JTSA が読み込む拡張機能の共通契約。</summary>
public interface IJtsaPlugin
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    Version Version { get; }

    void Initialize(IJtsaPluginContext context);
    void Open();
    void Shutdown();
}

/// <summary>JTSA から拡張機能へ渡す、UI 実装に依存しない機能。</summary>
public interface IJtsaPluginContext
{
    string PluginDirectory { get; }
    string DataDirectory { get; }
    nint MainWindowHandle { get; }

    void Log(string message);
    void LogError(string message, Exception? exception = null);
    void SetExpansionOverlay(ExpansionOverlayContent content);
    void RemoveExpansionOverlay(string id);
}

/// <summary>既存の配信拡張ブラウザソース上に表示するプラグイン描画。</summary>
public sealed record ExpansionOverlayContent(
    string Id,
    string Html,
    int X,
    int Y,
    int Width,
    int Height);
