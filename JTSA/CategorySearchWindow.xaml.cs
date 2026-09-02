using System.Windows;

namespace JTSA;

public partial class CategorySearchWindow : Window
{
    public CategorySearchWindow(bool addToPlaylistOnSelect = false)
    {
        InitializeComponent();
        SearchPanel.AddToPlaylistOnSelect = addToPlaylistOnSelect;
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (await SearchPanel.AddSelectedCategoryAsync()) DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
