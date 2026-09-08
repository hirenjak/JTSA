using JTSA.Plugins;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace JTSA.Panels;

public partial class ExtensionsPanel : UserControl
{
    private PluginManager? pluginManager;

    public ExtensionsPanel() => InitializeComponent();

    public ObservableCollection<PluginDescriptor>? Plugins => pluginManager?.Plugins;
    public ObservableCollection<string>? LoadErrors => pluginManager?.LoadErrors;

    public void Initialize(PluginManager manager)
    {
        pluginManager = manager;
        manager.Discover();
        DataContext = this;
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        pluginManager?.Discover();
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is PluginDescriptor descriptor)
            pluginManager?.Open(descriptor);
    }
}
