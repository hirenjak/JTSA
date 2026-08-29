using JTSA.Utility;
using System.Windows;
using System.Windows.Controls;

namespace JTSA.Panels;

public partial class TwitchNotificationPanel : UserControl
{
    public static readonly RoutedEvent CloseRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(CloseRequested),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(TwitchNotificationPanel));

    private readonly TwitchNotificationBrowserService browserService = new();
    private string targetUserName = string.Empty;

    public event RoutedEventHandler CloseRequested
    {
        add => AddHandler(CloseRequestedEvent, value);
        remove => RemoveHandler(CloseRequestedEvent, value);
    }

    public TwitchNotificationPanel()
    {
        InitializeComponent();
    }

    public void SetTargetAccount(string userName)
    {
        targetUserName = userName;
        TargetAccountTextBlock.Text = $"対象アカウント：{userName}";
    }

    private async void FillButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(targetUserName))
        {
            StatusTextBlock.Text = "対象アカウントを選択してください。";
            return;
        }

        if (string.IsNullOrWhiteSpace(NotificationTextBox.Text))
        {
            StatusTextBlock.Text = "通知文を入力してください。";
            return;
        }

        FillButton.IsEnabled = false;
        StatusTextBlock.Text = "Twitch画面を操作しています...";

        try
        {
            await browserService.FillAsync(targetUserName, NotificationTextBox.Text);
            StatusTextBlock.Text = "入力しました。Twitch画面で内容を確認して保存してください。";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = ex.Message;
        }
        finally
        {
            FillButton.IsEnabled = true;
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(targetUserName))
        {
            StatusTextBlock.Text = "対象アカウントを選択してください。";
            return;
        }

        LoginButton.IsEnabled = false;
        try
        {
            await browserService.OpenLoginChromeAsync(targetUserName);
            StatusTextBlock.Text = "Twitchへログインし、完了したらChromeを閉じてください。";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = ex.Message;
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private void NotificationTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CharacterCountTextBlock.Text = $"{NotificationTextBox.Text.Length} / 140";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(CloseRequestedEvent));
    }
}
