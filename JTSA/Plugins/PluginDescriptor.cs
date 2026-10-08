using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JTSA.Plugins;

public sealed class PluginDescriptor : INotifyPropertyChanged
{
    private string status = "未読み込み";
    private bool isAutoStart;
    private readonly Action<PluginDescriptor, bool>? autoStartChanged;

    internal PluginDescriptor(
        string id,
        string name,
        string description,
        Version version,
        bool isAutoStart = false,
        Action<PluginDescriptor, bool>? autoStartChanged = null)
    {
        Id = id;
        Name = name;
        Description = description;
        Version = version;
        this.isAutoStart = isAutoStart;
        this.autoStartChanged = autoStartChanged;
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public Version Version { get; }
    public string VersionText => $"v{Version}";
    public bool IsAutoStart
    {
        get => isAutoStart;
        set
        {
            if (isAutoStart == value) return;
            isAutoStart = value;
            OnPropertyChanged();
            autoStartChanged?.Invoke(this, value);
        }
    }
    public string Status
    {
        get => status;
        internal set
        {
            if (status == value) return;
            status = value;
            OnPropertyChanged();
        }
    }

    internal LoadedPlugin? LoadedPlugin { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
