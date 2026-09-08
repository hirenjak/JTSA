using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JTSA.Plugins;

public sealed class PluginDescriptor : INotifyPropertyChanged
{
    private string status = "未読み込み";

    internal PluginDescriptor(string id, string name, string description, Version version)
    {
        Id = id;
        Name = name;
        Description = description;
        Version = version;
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public Version Version { get; }
    public string VersionText => $"v{Version}";
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
