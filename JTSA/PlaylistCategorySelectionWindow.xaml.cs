using JTSA.Dao;
using JTSA.Forms;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace JTSA;

public partial class PlaylistCategorySelectionWindow : Window
{
    private const int DwmWindowCornerPreference = 33;
    private const int DwmWindowCornerDoNotRound = 1;

    public ObservableCollection<CategoryForm> Categories { get; } = [];
    public string SelectedCategoryId { get; private set; } = string.Empty;

    public PlaylistCategorySelectionWindow(bool selectionOnly = false)
    {
        InitializeComponent();
        CategoriesContainer.Collection = Categories;
        DataContext = this;
        SourceInitialized += PlaylistCategorySelectionWindow_SourceInitialized;

        if (selectionOnly)
        {
            Title = "カテゴリ選択";
            InstructionTextBlock.Text = "カテゴリを選択";
            ConfirmButton.Content = "選択";
            ConfirmButton.Width = 90;
        }

        ReloadCategories();
    }

    private void ReloadCategories()
    {
        Categories.Clear();
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

    private void AddCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new CategorySearchWindow(addToPlaylistOnSelect: false)
        {
            Owner = this
        };
        window.ShowDialog();
        ReloadCategories();
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

    private void PlaylistCategorySelectionWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var preference = DwmWindowCornerDoNotRound;
        DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, DwmWindowCornerPreference,
            ref preference, Marshal.SizeOf<int>());
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle, int attribute, ref int attributeValue, int attributeSize);
}
