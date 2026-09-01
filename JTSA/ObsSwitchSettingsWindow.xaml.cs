using System.Windows;

namespace JTSA;

public partial class ObsSwitchSettingsWindow : Window
{
    public ObsSwitchSettingsWindow(bool showSourceSwitch)
    {
        InitializeComponent();
        if (showSourceSwitch)
        {
            Title = "OBSソース切替設定";
            SwitchSettingsPanel.ShowSourceSwitchSettings();
        }
        else
        {
            Title = "OBSシーン切替設定";
            SwitchSettingsPanel.ShowSceneSwitchSettings();
        }
    }
}
