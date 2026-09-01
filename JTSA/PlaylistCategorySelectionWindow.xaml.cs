using JTSA.Dao;
using JTSA.Forms;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace JTSA;

public partial class PlaylistCategorySelectionWindow : Window
{
    public ObservableCollection<CategoryForm> Categories { get; } = [];
    public string SelectedCategoryId { get; private set; } = string.Empty;

    public PlaylistCategorySelectionWindow()
    {
        InitializeComponent();
        DataContext = this;

        foreach (var item in DAO_Category.SelectAllOrderbyLastUser())
        {
            Categories.Add(new CategoryForm
            {
                CategoryId = item.CategoryId,
                DisplayName = item.DisplayName,
                JapaneseDisplayName = item.JapaneseDisplayName,
                BoxArtUrl = item.BoxArtUrl,
                SteamUrl = item.SteamUrl ?? string.Empty,
                ChannelPointPresetId = item.ChannelPointPresetId ?? 0,
                LastUsedDate = item.LastUsedDateTime.ToString("yyyy/MM/dd HH:mm")
            });
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e) => ConfirmSelection();

    private void CategoryListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ConfirmSelection();

    private void ConfirmSelection()
    {
        if (CategoryListBox.SelectedItem is not CategoryForm category) return;
        SelectedCategoryId = category.CategoryId;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
