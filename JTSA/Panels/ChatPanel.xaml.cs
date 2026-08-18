using JTSA.Dao;
using JTSA.Forms;
using JTSA.Models;
using JTSA.Utility;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NAudio.Wave;
using System.Collections.Concurrent;

namespace JTSA.Panels
{
    public class TwitchChatPart
    {
        public string? Text { get; set; }

        public string? ImageUrl { get; set; }

        public bool IsEmote => !string.IsNullOrEmpty(ImageUrl);
    }

    /// <summary>
    /// ChatPanel.xaml の相互作用ロジック
    /// </summary>
    public partial class ChatPanel : UserControl
    {
        public static readonly RoutedUICommand AddFriendCommand = new(
            "フレンドに追加", nameof(AddFriendCommand), typeof(ChatPanel));

        private TwitchChatService? twitchChatService;

        private TwitchEventSubService? twitchEventSubService;

        private readonly StreamExpansionService streamExpansionService = new();

        private readonly BouyomiChanClient bouyomiChanClient = new();
        private readonly VoiceVoxClient voiceVoxClient = new();

        private bool bouyomiEnabled;
        private string bouyomiEndpoint = BouyomiChanClient.DefaultEndpoint;
        private string speechEngine = "None";
        private string voiceVoxEndpoint = VoiceVoxClient.DefaultEndpoint;
        private int voiceVoxSpeakerId = VoiceVoxClient.DefaultSpeakerId;

        private readonly ConcurrentDictionary<string, byte> chattedUserIds = new();

        // XAML construction can raise ValueChanged/Checked before persisted values are loaded.
        private bool overlayAppearanceInitialized;

        public ObservableCollection<TwitchChatForm> TwitchChatFormList { get; } = new();

        public ObservableCollection<TwitchChatForm> PinedTwitchChatFormList { get; } = new();

        public ObservableCollection<ChatUserForm> ChatUserFormList { get; } = new();

        /// <summary>OBSブラウザソース用のチャットデータを返す。</summary>
        public string CreateObsChatJson()
        {
            object[] items = Array.Empty<object>();

            Dispatcher.Invoke(() =>
            {
                items = TwitchChatFormList
                    .Reverse()
                    .Select(chat => (object)new
                    {
                        displayName = chat.DisplayName,
                        userName = chat.UserName,
                        hexColor = chat.HexColor,
                        messageColor = chat.MessageColor,
                        profileImageUrl = chat.ProfielImageUrl,
                        messageParts = chat.MessageParts.Select(part => new
                        {
                            text = part.Text,
                            imageUrl = part.ImageUrl,
                            isEmote = part.IsEmote
                        }).ToList()
                    })
                    .ToArray();
            });

            return JsonSerializer.Serialize(new { items });
        }

        /// <summary>WPF版チャットオーバーレイと同じ見た目のOBS用HTMLを返す。</summary>
        public string CreateObsChatHtml() => """
            <!DOCTYPE html>
            <html lang="ja">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>JTSA Chat Overlay</title>
                <style>
                    * { box-sizing: border-box; }
                    html, body {
                        width: 100%;
                        height: 100%;
                        margin: 0;
                        overflow: hidden;
                        background: transparent;
                        font-family: "Yu Gothic UI", "Meiryo UI", sans-serif;
                    }
                    #overlay {
                        position: absolute;
                        inset: 10px;
                        display: flex;
                        flex-direction: column;
                        justify-content: flex-end;
                        overflow: hidden;
                        padding: 12px;
                        border-radius: 12px;
                        background: rgba(0, 0, 0, 0.267);
                    }
                    #chatList {
                        display: flex;
                        min-height: 0;
                        flex-direction: column;
                        justify-content: flex-end;
                    }
                    .chatItem { flex: 0 0 auto; }
                    .chatContent {
                        display: grid;
                        grid-template-columns: 38px minmax(0, 1fr);
                        column-gap: 10px;
                        margin: 6px 8px 0;
                    }
                    .avatar {
                        width: 36px;
                        height: 38px;
                        border-radius: 50%;
                        object-fit: cover;
                    }
                    .messageArea { min-width: 0; }
                    .userLine {
                        display: flex;
                        align-items: baseline;
                        min-width: 0;
                        font-size: 13px;
                        line-height: 17px;
                        white-space: nowrap;
                    }
                    .displayName, .userName {
                        overflow: hidden;
                        text-overflow: ellipsis;
                    }
                    .userName {
                        margin-left: 4px;
                        color: white;
                    }
                    .message {
                        color: white;
                        font-size: 20px;
                        line-height: 25px;
                        overflow-wrap: anywhere;
                        white-space: pre-wrap;
                    }
                    .emote {
                        width: auto;
                        height: 34px;
                        vertical-align: middle;
                    }
                    .divider {
                        height: 2px;
                        margin-top: 6px;
                        background: white;
                    }
                </style>
            </head>
            <body>
                <div id="overlay"><div id="chatList"></div></div>
                <script>
                    const list = document.getElementById("chatList");

                    function createMessagePart(part) {
                        if (part.isEmote && part.imageUrl) {
                            const image = document.createElement("img");
                            image.className = "emote";
                            image.src = part.imageUrl;
                            image.alt = part.text ?? "";
                            return image;
                        }

                        return document.createTextNode(part.text ?? "");
                    }

                    function render(items) {
                        const fragment = document.createDocumentFragment();

                        for (const item of items ?? []) {
                            const row = document.createElement("div");
                            row.className = "chatItem";

                            const content = document.createElement("div");
                            content.className = "chatContent";

                            const avatar = document.createElement("img");
                            avatar.className = "avatar";
                            avatar.src = item.profileImageUrl ?? "";
                            avatar.alt = "";

                            const messageArea = document.createElement("div");
                            messageArea.className = "messageArea";

                            const userLine = document.createElement("div");
                            userLine.className = "userLine";

                            const displayName = document.createElement("span");
                            displayName.className = "displayName";
                            displayName.style.color = item.hexColor || "white";
                            displayName.textContent = item.displayName ?? "";

                            const userName = document.createElement("span");
                            userName.className = "userName";
                            userName.textContent = `(${item.userName ?? ""})`;

                            const message = document.createElement("div");
                            message.className = "message";
                            message.style.color = item.messageColor || "white";
                            for (const part of item.messageParts ?? []) {
                                message.appendChild(createMessagePart(part));
                            }

                            userLine.append(displayName, userName);
                            messageArea.append(userLine, message);
                            content.append(avatar, messageArea);

                            const divider = document.createElement("div");
                            divider.className = "divider";
                            row.append(content, divider);
                            fragment.appendChild(row);
                        }

                        list.replaceChildren(fragment);
                    }

                    async function load() {
                        try {
                            const response = await fetch("/chat-data?t=" + Date.now(), { cache: "no-store" });
                            if (response.ok) render((await response.json()).items);
                        } catch { }
                    }

                    load();
                    setInterval(load, 500);
                </script>
            </body>
            </html>
            """;

        private bool isChatUserListVisible = true;

        private WaveFileReader? ChatNotificationReader = new(Properties.Resources.CommentNotification);
        private WaveOutEvent? ChatNotificationPlayer = new();

        private WaveFileReader? JoinChatReader = new(Properties.Resources.JoinChat);
        private WaveOutEvent? JoinChatPlayer = new();



        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ChatPanel()
        {
            InitializeComponent();
            DataContext = this;

            ChatNotificationPlayer.Init(ChatNotificationReader);
            JoinChatPlayer.Init(JoinChatReader);

            sendChatButton.Click += SendChatButton_Click;
            pinedChatButton.Click += PinedChatButton_Click;

            IsStartShowChatOverlayDisp.Checked += IsStartShowChatOverlayDisp_Checked;
            IsStartShowChatOverlayDisp.Unchecked += IsStartShowChatOverlayDisp_Unchecked;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IsStartShowChatOverlayDisp_Unchecked(object sender, RoutedEventArgs e)
        {
            DAO_Setting.InsertUpdate(DAO_Setting.SettingName.IsChatOverlay, "0");
            IsStartShowChatOverlayDisp.IsChecked = false;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IsStartShowChatOverlayDisp_Checked(object sender, RoutedEventArgs e)
        {
            DAO_Setting.InsertUpdate(DAO_Setting.SettingName.IsChatOverlay, "1");
            IsStartShowChatOverlayDisp.IsChecked = true;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void PinedChatButton_Click(object sender, RoutedEventArgs e)
        {
            var chatId = await TwitchHelper.SendChat(sendChatTextBox.Text);

            if (string.IsNullOrWhiteSpace(chatId)) return;

            var result = await TwitchHelper.PinedChat(chatId);

            if (result == true)
            {
                await PinedChatLoad();
                sendChatTextBox.Text = string.Empty;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SendChatButton_Click(object sender, RoutedEventArgs e)
        {
            await TwitchHelper.SendChat(sendChatTextBox.Text);
            sendChatTextBox.Text = string.Empty;
        }


        /// <summary>
        /// コントロール読み込み時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public async void Initialize()
        {
            ReloadSpeechSettings();

            ChatNotificationVolumeSlider.Value =
               double.Parse(DAO_Setting.SelectOneById(DAO_Setting.SettingName.ChatNotificationVolume)?.Value ?? "50");

            JoinChatVolumeSlider.Value =
               double.Parse(DAO_Setting.SelectOneById(DAO_Setting.SettingName.JoinChatVolume)?.Value ?? "50");

            ChatOverlayFontSizeSlider.Value = ReadOverlayFontSize();
            ChatOverlayShowIconCheckBox.IsChecked =
                DAO_Setting.SelectOneById(DAO_Setting.SettingName.ChatOverlayShowUserIcon)?.Value != "0";
            overlayAppearanceInitialized = true;

            // 前回配信時のチャットユーザーをクリア
            DAO_ChatUser.AllDelete();
            ChatUserFormList.Clear();
            chattedUserIds.Clear();
            StreamSupportTracker.Reset();

            if(twitchChatService == null)
            {
                twitchChatService = new TwitchChatService(JTSAHelper.LoginName);
                twitchEventSubService = new TwitchEventSubService(TwitchHelper.api, TwitchHelper.BroadcasterId);

                twitchChatService.MessageReceived += message =>
                {
                    SpeakChatMessage(message.Message);

                    Dispatcher.InvokeAsync(() =>
                    {
                        // チャット欄に亜チャット追加
                        ChatAddAsync(new TwitchChatForm
                        {
                            Channel = message.Channel,
                            UserId = message.UserId,
                            UserName = message.Username,
                            DisplayName = message.DisplayName,
                            Message = message.Message,
                            HexColor = message.HexColor,
                            MessageId = message.Id,
                            IsModerator = message.UserDetail.IsModerator,
                            IsSubscriber = message.UserDetail.IsSubscriber,
                            IsVip = message.UserDetail.IsVip,
                            MessageParts = TwitchHelper.CreateParts(message)
                        }, false);
                    });

                    // チャットの発火条件確認
                    var chatPlaceholders = new ChatPlaceholderValues(message.DisplayName, message.Username);
                    _ = streamExpansionService.HandleAsync(
                        StreamExpansionTriggerType.Chat,
                        message.Message,
                        chatPlaceholders);

                    if (!string.IsNullOrWhiteSpace(message.UserId) && chattedUserIds.TryAdd(message.UserId, 0))
                    {
                        _ = streamExpansionService.HandleAsync(
                            StreamExpansionTriggerType.FirstChat,
                            message.Username,
                            chatPlaceholders);
                    }

                    // ビッツの発火条件確認
                    if (message.Bits > 0)
                    {
                        _ = streamExpansionService.HandleAsync(StreamExpansionTriggerType.Bits, message.Bits.ToString());
                    }
                };

                twitchChatService.SubscriptionReceived += () =>
                    _ = streamExpansionService.HandleAsync(StreamExpansionTriggerType.Subscribe, string.Empty);

                twitchEventSubService.ChannelPointRedeemed += channelPoint =>
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        ChatAddAsync(new TwitchChatForm
                        {
                            UserId = channelPoint.UserId,
                            UserName = channelPoint.UserLogin,
                            DisplayName = "ChannelPonint",
                            HexColor = "White",
                            Message = channelPoint.RewardTitle + " by." + channelPoint.UserName,
                            MessageParts = TwitchHelper.CreateParts(channelPoint.RewardTitle + " by." + channelPoint.UserName)
                        }, true);
                    });

                    _ = streamExpansionService.HandleAsync(StreamExpansionTriggerType.ChannelPoint, channelPoint.RewardId);
                };

                twitchEventSubService.FollowReceived += userName =>
                    _ = streamExpansionService.HandleAsync(StreamExpansionTriggerType.Follow, userName);

                twitchEventSubService.RaidReceived += userName =>
                    _ = streamExpansionService.HandleAsync(StreamExpansionTriggerType.Raid, userName);

                await twitchChatService.ConnectAsync();
                await twitchEventSubService.ConnectAsync();
            }


            #region ==========チャットオーバーレイ==========
            
            var settingIsChatOverlay = DAO_Setting.SelectOneById(DAO_Setting.SettingName.IsChatOverlay);
            if (settingIsChatOverlay != null && settingIsChatOverlay.Value == "1")
            {
                transparentWindow = new ChatOverlayWindow(Application.Current.MainWindow, TwitchChatFormList);
                transparentWindow.Show();
                IsStartShowChatOverlayDisp.IsChecked = true;
            }
            else
            {
                DAO_Setting.InsertUpdate(DAO_Setting.SettingName.IsChatOverlay, "0");
                IsStartShowChatOverlayDisp.IsChecked = false;
            }

            #endregion

            await PinedChatLoad();
        }

        /// <summary>DBからチャット読み上げ連携の設定を再読み込みする。</summary>
        public void ReloadSpeechSettings()
        {
            bouyomiEnabled = DAO_Setting.SelectOneById(
                DAO_Setting.SettingName.BouyomiEnabled)?.Value == "1";
            bouyomiEndpoint = DAO_Setting.SelectOneById(
                DAO_Setting.SettingName.BouyomiEndpoint)?.Value
                ?? BouyomiChanClient.DefaultEndpoint;
            speechEngine = DAO_Setting.SelectOneById(DAO_Setting.SettingName.SpeechEngine)?.Value
                ?? (bouyomiEnabled ? "Bouyomi" : "None");
            voiceVoxEndpoint = DAO_Setting.SelectOneById(DAO_Setting.SettingName.VoiceVoxEndpoint)?.Value
                ?? VoiceVoxClient.DefaultEndpoint;
            if (!int.TryParse(DAO_Setting.SelectOneById(DAO_Setting.SettingName.VoiceVoxSpeakerId)?.Value,
                out voiceVoxSpeakerId) || voiceVoxSpeakerId < 0)
                voiceVoxSpeakerId = VoiceVoxClient.DefaultSpeakerId;
        }

        private async void SpeakChatMessage(string message)
        {
            if (speechEngine == "None" || string.IsNullOrWhiteSpace(message)) return;

            try
            {
                if (speechEngine == "VoiceVox")
                    await voiceVoxClient.SpeakAsync(voiceVoxEndpoint, voiceVoxSpeakerId, message);
                else if (speechEngine == "Bouyomi")
                    await bouyomiChanClient.SpeakAsync(bouyomiEndpoint, message);
            }
            catch (Exception ex)
            {
                // 読み上げ失敗でTwitchチャットの受信処理を止めない。
                Console.WriteLine($"チャット読み上げエラー: {ex.Message}");
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="form"></param>
        /// <param name="isChannelPoint"></param>
        private async void ChatAddAsync(TwitchChatForm form, bool isChannelPoint)
        {
            DAO_DailyChatUserCount.Increment(
                DateTime.Now, form.UserId, form.UserName, form.DisplayName);

            var userData = DAO_User.SelectOneByUserId(form.UserId);

            if (userData == null)
            {
                // 配信者情報取得
                var streamerInfo = await TwitchHelper.GetBroadcasterIdAsync(form.UserName);

                // データチェック
                if (streamerInfo == null) return;
                if (string.IsNullOrWhiteSpace(streamerInfo.UserId)) return;

                // データ作成
                var insertData = new M_User
                {
                    UserId = streamerInfo.UserId,
                    LoginId = streamerInfo.Login,
                    DisplayName = streamerInfo.DisplayName,
                    ProfielImageUrl = streamerInfo.ProfileImageUrl,
                    IsFriend = false,
                    LastUsedDateTime = DateTime.Now,
                    CreatedDateTime = DateTime.Now,
                    UpdatedDateTime = DateTime.Now
                };

                DAO_User.Insert(insertData);

                userData = insertData;
            }

            if (userData == null) return;

            form.ProfielImageUrl = userData.ProfielImageUrl?.Replace("-300x300.png", "-70x70.png");
            form.CreatedDateTime = DateTime.Now;

            if (isChannelPoint)
            {
                form.MessageColor = "#AAAAAA";
                form.HexColor = "#FFFFFF";
            }

            var chatedUser = DAO_ChatUser.SelectOneByUserId(form.UserId);

            UnmanagedMemoryStream soundData;

            if (chatedUser == null)
            {
                JoinChatReader.Position = 0;
                JoinChatPlayer.Volume = (float)JoinChatVolumeSlider.Value / 100f;
                JoinChatPlayer.Play();

                var inserData = new T_ChatUser
                {
                    UserId = form.UserId,
                    CreatedDateTime = DateTime.Now,
                    UpdatedDateTime = DateTime.Now,
                    LastUsedDateTime = DateTime.Now
                };

                DAO_ChatUser.InsertUpdate(inserData);
            }
            else
            {
                ChatNotificationReader.Position = 0;
                ChatNotificationPlayer.Volume = (float)ChatNotificationVolumeSlider.Value / 100f;
                ChatNotificationPlayer.Play();
            }

            UpdateChatUserList(form, userData);

            TwitchChatFormList.Insert(0, form);

            await PinedChatLoad();
        }

        /// <summary>発言ユーザーを重複なしで一覧へ追加し、最新発言者を先頭へ移動する。</summary>
        private void UpdateChatUserList(TwitchChatForm chat, M_User user)
        {
            var existingUser = ChatUserFormList.FirstOrDefault(x => x.UserId == chat.UserId);
            var messageCount = (existingUser?.MessageCount ?? 0) + 1;

            if (existingUser != null)
            {
                ChatUserFormList.Remove(existingUser);
            }

            ChatUserFormList.Insert(0, new ChatUserForm
            {
                UserId = chat.UserId,
                UserName = user.LoginId,
                DisplayName = user.DisplayName,
                ProfileImageUrl = user.ProfielImageUrl?.Replace("-300x300.png", "-70x70.png") ?? "",
                LastChatDateTime = DateTime.Now,
                MessageCount = messageCount
            });
        }

        /// <summary>チャットユーザー一覧の表示・非表示を切り替える。</summary>
        private void ChatUserListToggleButton_Click(object sender, RoutedEventArgs e)
        {
            isChatUserListVisible = !isChatUserListVisible;

            ChatUserListPanel.Visibility = isChatUserListVisible
                ? Visibility.Visible
                : Visibility.Collapsed;
            ChatUserListColumn.Width = isChatUserListVisible
                ? new GridLength(260)
                : new GridLength(0);
            ChatUserListToggleButton.Content = isChatUserListVisible
                ? "ユーザー一覧を隠す"
                : "ユーザー一覧を表示";
        }

        /// <summary>チャットユーザー一覧の右クリックメニューからフレンドへ追加する。</summary>
        private void ChatUserContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu contextMenu) return;

            var user = (contextMenu.PlacementTarget as FrameworkElement)?.DataContext as ChatUserForm;
            foreach (var menuItem in contextMenu.Items.OfType<MenuItem>())
            {
                menuItem.CommandTarget = contextMenu.PlacementTarget;
                menuItem.CommandParameter = user;
            }
        }

        private void AddChatUserToFriendCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var user = e.Parameter as ChatUserForm
                ?? (e.OriginalSource as FrameworkElement)?.DataContext as ChatUserForm
                ?? (e.Source as FrameworkElement)?.DataContext as ChatUserForm;

            if (user == null) return;

            if (DAO_User.MarkAsFriend(user.UserId))
            {
                ((MainWindow)Application.Current.MainWindow).FriendPanel.ReloadFriend();
            }

            e.Handled = true;
        }

        private async Task PinedChatLoad()
        {
            PinedTwitchChatFormList.Clear();

            var message = await TwitchHelper.GetPinedChat();

            if (message != null)
            {
                var user = DAO_User.SelectOneByUserId(message.UserId);
                message.ProfielImageUrl = user?.ProfielImageUrl.Replace("-300x300.png", "-70x70.png") ?? "";
                PinedTwitchChatFormList.Add(message);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChatNotificationVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DAO_Setting.InsertUpdate(
                DAO_Setting.SettingName.ChatNotificationVolume,
                e.NewValue.ToString()
            );
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void JoinChatVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DAO_Setting.InsertUpdate(
                DAO_Setting.SettingName.JoinChatVolume,
                e.NewValue.ToString()
            );
        }

        /// <summary>  </summary>
        ChatOverlayWindow? transparentWindow;

        private double ReadOverlayFontSize()
        {
            var value = DAO_Setting.SelectOneById(DAO_Setting.SettingName.ChatOverlayFontSize)?.Value;
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? Math.Clamp(parsed, 10, 36)
                : 16;
        }

        private void ApplyOverlayAppearance()
        {
            transparentWindow?.ApplyAppearance(
                ChatOverlayShowIconCheckBox.IsChecked == true,
                ChatOverlayFontSizeSlider.Value);
        }

        private void ChatOverlayFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!overlayAppearanceInitialized) return;

            DAO_Setting.InsertUpdate(
                DAO_Setting.SettingName.ChatOverlayFontSize,
                e.NewValue.ToString(CultureInfo.InvariantCulture));
            ApplyOverlayAppearance();
        }

        private void ChatOverlayShowIconCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!overlayAppearanceInitialized) return;

            DAO_Setting.InsertUpdate(
                DAO_Setting.SettingName.ChatOverlayShowUserIcon,
                ChatOverlayShowIconCheckBox.IsChecked == true ? "1" : "0");
            ApplyOverlayAppearance();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TransparentWindowOpen_Click(object sender, RoutedEventArgs e)
        {
            if(transparentWindow != null)
            {
                transparentWindow.Close();
                transparentWindow = null;
            }
            else
            {
                transparentWindow = new ChatOverlayWindow(Application.Current.MainWindow, TwitchChatFormList);
                ApplyOverlayAppearance();
                transparentWindow.Show();
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TransparentWindowOpenSetting_Click(object sender, RoutedEventArgs e)
        {
            if (transparentWindow == null) return;
            transparentWindow.SwitchSettingClick();
        }

        private async void PinedChatPurgeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string messageId ||
                string.IsNullOrWhiteSpace(messageId)) return;

            var result = await TwitchHelper.PinedDeleteChat(messageId);

            if (result == true)
            {
                PinedTwitchChatFormList.Clear();
            }
        }
    }
}
