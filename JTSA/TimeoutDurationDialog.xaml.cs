using System.Windows;
using System.Windows.Input;

namespace JTSA;

public partial class TimeoutDurationDialog : Window
{
    private const int MaximumDurationSeconds = 1_209_600;

    public int DurationSeconds { get; private set; }

    public TimeoutDurationDialog(string displayName, string userName)
    {
        InitializeComponent();
        TargetUserTextBlock.Text = $"{displayName}（{userName}）をタイムアウトします。";
        Loaded += (_, _) =>
        {
            DurationTextBox.Focus();
            DurationTextBox.SelectAll();
        };
    }

    private void DurationTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        => e.Handled = e.Text.Any(character => !char.IsDigit(character));

    private void DurationTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Confirm();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e) => Confirm();

    private void Confirm()
    {
        if (!int.TryParse(DurationTextBox.Text, out var seconds) ||
            seconds is < 1 or > MaximumDurationSeconds)
        {
            MessageBox.Show(
                $"秒数は1〜{MaximumDurationSeconds:N0}の範囲で入力してください。",
                "Twitchタイムアウト",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            DurationTextBox.Focus();
            DurationTextBox.SelectAll();
            return;
        }

        DurationSeconds = seconds;
        DialogResult = true;
    }
}
