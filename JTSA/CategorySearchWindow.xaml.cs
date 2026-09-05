using System.Windows;

namespace JTSA;

public partial class CategorySearchWindow : Window
{
    private bool isAdding;
    // DialogResult を使うため、呼び出し元では ShowDialog() で表示する。
    public CategorySearchWindow(bool addToPlaylistOnSelect = false)
    {
        InitializeComponent();
        SearchPanel.AddToPlaylistOnSelect = addToPlaylistOnSelect;
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
        => await AddSelectedCategoryAndCloseAsync();

    internal async Task AddSelectedCategoryAndCloseAsync()
    {
        // ボタン・Enter・一覧のダブルクリックを同じ入口で抑止する。
        if (isAdding || !IsVisible) return;
        isAdding = true;
        AddButton.IsEnabled = false;
        SearchPanel.IsEnabled = false;
        try
        {
            // 通信待機中に閉じられた場合はDialogResultに触らない。
            if (await SearchPanel.AddSelectedCategoryAsync() && IsVisible)
                DialogResult = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"カテゴリ追加失敗: {ex}");
            if (IsVisible)
                MessageBox.Show(this, "カテゴリの追加に失敗しました。時間をおいてもう一度お試しください。",
                    "カテゴリ追加", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            isAdding = false;
            AddButton.IsEnabled = true;
            SearchPanel.IsEnabled = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
