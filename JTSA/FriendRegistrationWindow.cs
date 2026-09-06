using JTSA.Panels;
using JTSA.Utility;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JTSA;

public sealed class FriendRegistrationWindow : ToolPanelWindow
{
    public FriendRegistrationWindow(FriendPanel friendPanel, string? existingUserId = null)
        : this(new FriendRegistrationContent(friendPanel, existingUserId), existingUserId != null) { }

    private FriendRegistrationWindow(FriendRegistrationContent content, bool isEditing)
        : base(isEditing ? "登録ユーザーを編集" : "フレンドを追加", content)
    {
        Width = 560;
        Height = 540;
        MinWidth = 500;
        MinHeight = 500;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        content.CloseRequested += (_, _) => Close();
    }

    private sealed class FriendRegistrationContent : UserControl
    {
        private readonly FriendPanel friendPanel;
        private readonly TextBox twitchLoginTextBox = new();
        private readonly TextBox loginIdTextBox = new();
        private readonly TextBox displayNameTextBox = new();
        private readonly TextBox profileImageUrlTextBox = new();
        private readonly TextBox streamingPlatformTextBox = new();
        private readonly TextBox streamingUrlTextBox = new();
        private readonly TextBlock statusTextBlock = new();
        private readonly Button fetchButton = new();
        private readonly Button registerButton = new();
        private readonly string? existingUserId;
        private string? twitchUserId;
        private string fetchedTwitchLogin = string.Empty;

        public event EventHandler? CloseRequested;

        public FriendRegistrationContent(FriendPanel friendPanel, string? existingUserId)
        {
            this.friendPanel = friendPanel;
            this.existingUserId = existingUserId;
            Background = new SolidColorBrush(Color.FromRgb(48, 48, 48));
            var root = new StackPanel { Margin = new Thickness(20) };
            root.Children.Add(new TextBlock
            {
                Text = "Twitchから情報を取得するか、各項目を直接入力してください。",
                Foreground = Brushes.White,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 12)
            });

            var twitchRow = new Grid();
            twitchRow.ColumnDefinitions.Add(new ColumnDefinition());
            twitchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            AddLabeledTextBox(root, "TwitchユーザーID（任意）", twitchLoginTextBox, twitchRow);
            fetchButton.Content = "情報を取得";
            fetchButton.Width = 100;
            fetchButton.Height = 28;
            fetchButton.Margin = new Thickness(8, 0, 0, 0);
            fetchButton.Foreground = Brushes.White;
            fetchButton.Background = new SolidColorBrush(Color.FromRgb(85, 85, 85));
            fetchButton.BorderBrush = new SolidColorBrush(Color.FromRgb(119, 119, 119));
            fetchButton.Click += FetchButton_Click;
            Grid.SetColumn(fetchButton, 1);
            twitchRow.Children.Add(fetchButton);

            AddLabeledTextBox(root, "ユーザーID／識別名（必須）", loginIdTextBox);
            AddLabeledTextBox(root, "表示名（必須）", displayNameTextBox);
            AddLabeledTextBox(root, "プロフィール画像URL（任意）", profileImageUrlTextBox);
            AddLabeledTextBox(root, "配信プラットフォーム（任意）", streamingPlatformTextBox);
            AddLabeledTextBox(root, "配信URL（任意）", streamingUrlTextBox);
            statusTextBlock.Margin = new Thickness(0, 8, 0, 0);
            statusTextBlock.Foreground = Brushes.IndianRed;
            root.Children.Add(statusTextBlock);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            var cancelButton = new Button
            {
                Content = "キャンセル", Width = 90, Height = 30,
                Margin = new Thickness(0, 0, 8, 0)
            };
            cancelButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
            registerButton.Content = existingUserId == null ? "登録" : "更新";
            registerButton.Width = 90;
            registerButton.Height = 30;
            registerButton.IsDefault = true;
            registerButton.Foreground = Brushes.White;
            registerButton.Background = new SolidColorBrush(Color.FromRgb(40, 86, 83));
            registerButton.BorderBrush = Brushes.LightSeaGreen;
            registerButton.Click += RegisterButton_Click;
            buttons.Children.Add(cancelButton);
            buttons.Children.Add(registerButton);
            root.Children.Add(buttons);
            Content = root;
            LoadExistingUser();
            Loaded += (_, _) => twitchLoginTextBox.Focus();
        }

        private void LoadExistingUser()
        {
            if (string.IsNullOrWhiteSpace(existingUserId)) return;
            var user = Dao.DAO_User.SelectOneByUserId(existingUserId);
            if (user == null) return;

            var isManual = user.UserId.StartsWith("manual:", StringComparison.Ordinal);
            twitchUserId = isManual ? null : user.UserId;
            fetchedTwitchLogin = isManual ? string.Empty : user.LoginId;
            twitchLoginTextBox.Text = fetchedTwitchLogin;
            loginIdTextBox.Text = user.LoginId;
            displayNameTextBox.Text = user.DisplayName;
            profileImageUrlTextBox.Text = user.ProfielImageUrl ?? string.Empty;
            streamingPlatformTextBox.Text = user.StreamingPlatform;
            streamingUrlTextBox.Text = user.StreamingUrl;
        }

        private static void AddLabeledTextBox(Panel root, string label, TextBox textBox, Grid? inputRow = null)
        {
            root.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = Brushes.LightGray,
                Margin = new Thickness(0, 7, 0, 4)
            });
            textBox.Height = 28;
            if (inputRow == null) root.Children.Add(textBox);
            else
            {
                inputRow.Children.Add(textBox);
                root.Children.Add(inputRow);
            }
        }

        private async void FetchButton_Click(object sender, RoutedEventArgs e)
        {
            var twitchLogin = twitchLoginTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(twitchLogin))
            {
                ShowError("TwitchユーザーIDを入力してください。");
                return;
            }

            SetBusy(true);
            statusTextBlock.Foreground = Brushes.LightGray;
            statusTextBlock.Text = "Twitchからユーザー情報を取得しています…";
            try
            {
                var user = await TwitchHelper.GetBroadcasterIdAsync(twitchLogin);
                if (user == null || string.IsNullOrWhiteSpace(user.UserId))
                {
                    twitchUserId = null;
                    fetchedTwitchLogin = string.Empty;
                    ShowError("Twitchユーザーを確認できませんでした。");
                    return;
                }

                twitchUserId = user.UserId;
                fetchedTwitchLogin = user.Login;
                twitchLoginTextBox.Text = user.Login;
                loginIdTextBox.Text = user.Login;
                displayNameTextBox.Text = user.DisplayName;
                profileImageUrlTextBox.Text = user.ProfileImageUrl;
                streamingPlatformTextBox.Text = "Twitch";
                streamingUrlTextBox.Text = $"https://www.twitch.tv/{user.Login}";
                statusTextBlock.Foreground = Brushes.LightGreen;
                statusTextBlock.Text = "情報を取得しました。各項目は編集できます。";
            }
            finally { SetBusy(false); }
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            var loginId = loginIdTextBox.Text.Trim();
            var displayName = displayNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(loginId) || string.IsNullOrWhiteSpace(displayName))
            {
                ShowError("ユーザーID／識別名と表示名を入力してください。");
                return;
            }

            var selectedTwitchUserId = string.Equals(
                twitchLoginTextBox.Text.Trim(), fetchedTwitchLogin,
                StringComparison.OrdinalIgnoreCase) ? twitchUserId : null;
            friendPanel.AddFriend(
                selectedTwitchUserId,
                loginId,
                displayName,
                profileImageUrlTextBox.Text,
                streamingPlatformTextBox.Text,
                streamingUrlTextBox.Text,
                existingUserId);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void SetBusy(bool isBusy)
        {
            fetchButton.IsEnabled = !isBusy;
            registerButton.IsEnabled = !isBusy;
        }

        private void ShowError(string message)
        {
            statusTextBlock.Foreground = Brushes.IndianRed;
            statusTextBlock.Text = message;
        }
    }
}
