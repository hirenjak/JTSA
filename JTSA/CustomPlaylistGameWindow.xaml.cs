using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JTSA.Utility;

namespace JTSA;

public partial class CustomPlaylistGameWindow : Window
{
    public string GameName => GameNameTextBox.Text.Trim();
    public string SteamUrl => SteamUrlTextBox.Text.Trim();

    public CustomPlaylistGameWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => GameNameTextBox.Focus();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(GameName))
        {
            MessageBox.Show(this, "ゲーム名を入力してください。", "入力確認",
                MessageBoxButton.OK, MessageBoxImage.Information);
            GameNameTextBox.Focus();
            return;
        }

        if (!string.IsNullOrWhiteSpace(SteamUrl) && SteamHelper.GetSteamAppId(SteamUrl) is null)
        {
            MessageBox.Show(this, "Steamストアの商品ページURLを入力してください。", "入力確認",
                MessageBoxButton.OK, MessageBoxImage.Information);
            SteamUrlTextBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void InputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || e.Key != Key.V ||
            (Keyboard.Modifiers & ModifierKeys.Control) == 0) return;

        PasteClipboardText(textBox);
        e.Handled = true;
    }

    private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Parent: ContextMenu { PlacementTarget: TextBox textBox } })
            PasteClipboardText(textBox);
    }

    private static void PasteClipboardText(TextBox textBox)
    {
        try
        {
            if (!Clipboard.ContainsText()) return;

            var clipboardText = Clipboard.GetText();
            var availableLength = textBox.MaxLength <= 0
                ? clipboardText.Length
                : Math.Max(0, textBox.MaxLength - (textBox.Text.Length - textBox.SelectionLength));
            var insertedText = clipboardText[..Math.Min(clipboardText.Length, availableLength)];
            var selectionStart = textBox.SelectionStart;
            textBox.SelectedText = insertedText;
            textBox.CaretIndex = selectionStart + insertedText.Length;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"クリップボード貼り付け失敗: {ex.Message}");
        }
    }
}
