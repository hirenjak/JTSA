using System.Windows;

namespace JTSA;

public partial class CategorySearchWindow : Window
{
    // DialogResult を使うため、呼び出し元では ShowDialog() で表示する。
    public CategorySearchWindow(bool addToPlaylistOnSelect = false)
    {
        InitializeComponent();
        SearchPanel.AddToPlaylistOnSelect = addToPlaylistOnSelect;
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        // 通信待機中にキャンセル・閉じる操作が行われる場合がある。
        if (await SearchPanel.AddSelectedCategoryAsync() && IsVisible) DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
