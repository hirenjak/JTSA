using System.Windows;

namespace JTSA;

public partial class ObsCaptureDestinationWindow : Window
{
    public ObsCaptureDestinationWindow(string categoryId, string categoryName, string boxArtUrl)
    {
        InitializeComponent();
        Title = $"OBSキャプチャ先変更 - {categoryName}";
        CaptureSettingsPanel.ShowCaptureDestinationSettings(categoryId, categoryName, boxArtUrl);
    }
}
