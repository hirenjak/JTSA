using System.Windows;

namespace JTSA;

public partial class JapaneseCategoryNameEditWindow : Window
{
    public string JapaneseName => JapaneseNameTextBox.Text.Trim();

    public JapaneseCategoryNameEditWindow(string categoryName, string japaneseName)
    {
        InitializeComponent();
        CategoryNameTextBlock.Text = categoryName;
        JapaneseNameTextBox.Text = japaneseName;
        JapaneseNameTextBox.SelectAll();
        Loaded += (_, _) => JapaneseNameTextBox.Focus();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
