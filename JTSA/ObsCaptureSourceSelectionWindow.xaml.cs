using JTSA.Utility;
using System.Windows;
using System.Windows.Input;

namespace JTSA;

public partial class ObsCaptureSourceSelectionWindow : Window
{
    private readonly Func<Task<IReadOnlyList<ObsCaptureSourceSelectionItem>>> loadSourcesAsync;
    public ObsCaptureSourceSelectionItem? SelectedSource { get; private set; }

    public ObsCaptureSourceSelectionWindow(
        Func<Task<IReadOnlyList<ObsCaptureSourceSelectionItem>>> loadSourcesAsync)
    {
        InitializeComponent();
        this.loadSourcesAsync = loadSourcesAsync;
        Loaded += async (_, _) => await RefreshSourcesAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshSourcesAsync();

    private async Task RefreshSourcesAsync()
    {
        SourceListBox.IsEnabled = false;
        try
        {
            SourceListBox.ItemsSource = await loadSourcesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"キャプチャソースを取得できませんでした。\n{ex.GetBaseException().Message}",
                "OBS連携", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SourceListBox.IsEnabled = true;
        }
    }

    private void RegisterButton_Click(object sender, RoutedEventArgs e) => ConfirmSelection();
    private void SourceListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ConfirmSelection();

    private void ConfirmSelection()
    {
        if (SourceListBox.SelectedItem is not ObsCaptureSourceSelectionItem source) return;
        SelectedSource = source;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

public sealed record ObsCaptureSourceSelectionItem(bool IsSub, ObsCaptureSource Source)
{
    public string DisplayName => $"{(IsSub ? "サブOBS" : "メインOBS")}｜{Source.InputName} / {Source.TypeName}";
}
