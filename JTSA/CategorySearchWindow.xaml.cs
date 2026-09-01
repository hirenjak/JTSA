using System.Windows;

namespace JTSA;

public partial class CategorySearchWindow : Window
{
    public CategorySearchWindow(bool addToPlaylistOnSelect = false)
    {
        InitializeComponent();
        SearchPanel.AddToPlaylistOnSelect = addToPlaylistOnSelect;
    }
}
