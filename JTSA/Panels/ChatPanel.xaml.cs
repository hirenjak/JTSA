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
using TwitchLib.Api;
using System.Threading;
using Microsoft.Win32;

namespace JTSA.Panels
{
    public class TwitchChatPart
    {
        public string? Text { get; set; }

        public string? ImageUrl { get; set; }

        public string? Foreground { get; set; }

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
        private string connectedBroadcasterId = string.Empty;
        private string connectedAccessToken = string.Empty;
        private readonly SemaphoreSlim initializationLock = new(1, 1);

        private readonly StreamExpansionService streamExpansionService = new();

        private readonly BouyomiChanClient bouyomiChanClient = new();
        private readonly VoiceVoxClient voiceVoxClient = new();

        private bool bouyomiEnabled;
        private string bouyomiEndpoint = BouyomiChanClient.DefaultEndpoint;
        private string speechEngine = "None";
        private string voiceVoxEndpoint = VoiceVoxClient.DefaultEndpoint;
        private int voiceVoxSpeakerId = VoiceVoxClient.DefaultSpeakerId;

        private readonly StreamChatEntranceTracker chatEntranceTracker = new();

        // XAML construction can raise ValueChanged/Checked before persisted values are loaded.
        private bool overlayAppearanceInitialized;

        public ObservableCollection<TwitchChatForm> TwitchChatFormList { get; } = new();

        public ObservableCollection<TwitchChatForm> PinedTwitchChatFormList { get; } = new();

        public ObservableCollection<ChatUserForm> ChatUserFormList { get; } = new();

        internal (string BroadcasterId, string AccessToken) GetConnectedAccountContext()
            => (connectedBroadcasterId, connectedAccessToken);

        /// <summary>
        /// 接続中アカウントのアクセストークンを更新する。
        /// 別アカウント向けの更新を誤って適用しないよう、配信者IDが一致する場合だけ差し替える。
        /// </summary>
        internal void UpdateConnectedAccessToken(string broadcasterId, string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken) ||
                !string.Equals(connectedBroadcasterId, broadcasterId, StringComparison.Ordinal))
            {
                return;
            }

            connectedAccessToken = accessToken;
        }

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
                            isEmote = part.IsEmote,
                            foreground = part.Foreground
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

                        const text = document.createElement("span");
                        text.textContent = part.text ?? "";
                        if (part.foreground) text.style.color = part.foreground;
                        return text;
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

        private WaveStream? ChatNotificationReader;
        private WaveOutEvent? ChatNotificationPlayer;

        private WaveStream? JoinChatReader;
        private WaveOutEvent? JoinChatPlayer;



        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ChatPanel()
        {
            InitializeComponent();
            DataContext = this;

            LoadChatNotificationAudio();
            LoadJoinChatAudio();

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
            var chatId = await TwitchHelper.SendChat(
                sendChatTextBox.Text,
                connectedBroadcasterId,
                connectedAccessToken);

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
            var chatId = await TwitchHelper.SendChat(
                sendChatTextBox.Text,
                connectedBroadcasterId,
                connectedAccessToken);
            if (!string.IsNullOrWhiteSpace(chatId))
                sendChatTextBox.Text = string.Empty;
        }


        /// <summary>
        /// コントロール読み込み時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public async Task InitializeAsync(string channelName, string broadcasterId, string accessToken)
        {
            await initializationLock.WaitAsync();
            try
            {
                await InitializeCoreAsync(channelName, broadcasterId, accessToken);
            }
            finally
            {
                initializationLock.Release();
            }
        }

        private async Task InitializeCoreAsync(string channelName, string broadcasterId, string accessToken)
        {
            connectedAccessToken = accessToken;

            if (twitchChatService is not null && connectedBroadcasterId == broadcasterId)
                return;

            if (twitchChatService is not null && connectedBroadcasterId != broadcasterId)
            {
                await twitchChatService.DisconnectAsync();
                if (twitchEventSubService is not null)
                    await twitchEventSubService.DisposeAsync();
                twitchChatService = null;
                twitchEventSubService = null;
            }

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
            chatEntranceTracker.Clear();
            chatEntranceTracker.Restore(
                TwitchHelper.CurrentStreamId,
                DAO_StreamChatUserCount.SelectByStreamId(TwitchHelper.CurrentStreamId)
                    .Select(x => x.UserId));
            StreamSupportTracker.StartStream(TwitchHelper.CurrentStreamId);

            if(twitchChatService == null)
            {
                var accountApi = new TwitchAPI();
                accountApi.Settings.ClientId = TwitchHelper.ClientID;
                accountApi.Settings.AccessToken = accessToken;
                twitchChatService = new TwitchChatService(channelName);
                twitchEventSubService = new TwitchEventSubService(accountApi, broadcasterId);
                connectedBroadcasterId = broadcasterId;

                twitchChatService.MessageReceived += message =>
                {
                    // 入力必須のチャンネルポイント交換は、IRCの通常チャットと
                    // EventSubの交換通知の両方で届く。交換通知側へ入力文もまとめるため、
                    // IRC側はチャット一覧へ重複追加しない。
                    if (!string.IsNullOrWhiteSpace(message.CustomRewardId)) return;

                    SpeakChatMessage(message.Message);

                    var isFirstEntrance = chatEntranceTracker.TryEnter(
                        TwitchHelper.CurrentStreamId,
                        message.UserId);

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
                        }, false, isFirstEntrance);
                    });

                    // チャットの発火条件確認
                    var chatPlaceholders = new ChatPlaceholderValues(message.DisplayName, message.Username);
                    _ = streamExpansionService.HandleAsync(
                        StreamExpansionTriggerType.Chat,
                        message.Message,
                        chatPlaceholders,
                        chatUser: new StreamExpansionChatUserContext(
                            message.UserId == connectedBroadcasterId,
                            message.UserDetail.IsModerator,
                            message.UserDetail.IsVip,
                            message.UserDetail.IsSubscriber));

                    if (isFirstEntrance)
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
                    // IRC側の交換メッセージは重複防止で除外しているため、
                    // ユーザー入力はEventSub側から読み上げへ渡す。
                    SpeakChatMessage(channelPoint.UserInput);

                    var isFirstEntrance = chatEntranceTracker.TryEnter(
                        TwitchHelper.CurrentStreamId,
                        channelPoint.UserId);

                    Dispatcher.InvokeAsync(() =>
                    {
                        var message = ChannelPointChatFormatter.Format(
                            channelPoint.RewardTitle,
                            channelPoint.UserInput);

                        ChatAddAsync(new TwitchChatForm
                        {
                            UserId = channelPoint.UserId,
                            UserName = channelPoint.UserLogin,
                            DisplayName = channelPoint.UserName,
                            HexColor = "White",
                            Message = message,
                            MessageParts = ChannelPointChatFormatter.CreateParts(
                                channelPoint.RewardTitle,
                                channelPoint.UserInput)
                        }, true, isFirstEntrance);
                    });

                    _ = streamExpansionService.HandleAsync(
                        StreamExpansionTriggerType.ChannelPoint,
                        channelPoint.RewardId,
                        channelPointInput: channelPoint.UserInput);

                    if (isFirstEntrance)
                    {
                        var chatPlaceholders = new ChatPlaceholderValues(channelPoint.UserName, channelPoint.UserLogin);
                        _ = streamExpansionService.HandleAsync(
                            StreamExpansionTriggerType.FirstChat,
                            channelPoint.UserLogin,
                            chatPlaceholders);
                    }
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
                RecreateChatOverlayWindow();
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
        /// <param name="isFirstEntrance">現在の配信で最初のチャット入室か。</param>
        private async void ChatAddAsync(
            TwitchChatForm form,
            bool isChannelPoint,
            bool isFirstEntrance)
        {
            DAO_StreamChatUserCount.Increment(
                DateTime.Now,
                form.UserId,
                form.UserName,
                form.DisplayName,
                TwitchHelper.CurrentStreamId);

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

            if (isFirstEntrance)
            {
                if (JoinChatReader != null && JoinChatPlayer != null)
                {
                    JoinChatReader.Position = 0;
                    JoinChatPlayer.Volume = (float)JoinChatVolumeSlider.Value / 100f;
                    JoinChatPlayer.Play();
                }

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
                if (ChatNotificationReader != null && ChatNotificationPlayer != null)
                {
                    ChatNotificationReader.Position = 0;
                    ChatNotificationPlayer.Volume = (float)ChatNotificationVolumeSlider.Value / 100f;
                    ChatNotificationPlayer.Play();
                }
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

        private void ChatNotificationAudioChangeButton_Click(object sender, RoutedEventArgs e)
        {
            var path = SelectAudioFile();
            if (path == null) return;

            if (TryReplaceAudio(path, ref ChatNotificationReader, ref ChatNotificationPlayer, "チャット通知音"))
                DAO_Setting.InsertUpdate(DAO_Setting.SettingName.ChatNotificationAudioPath, path);
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

        private void JoinChatAudioChangeButton_Click(object sender, RoutedEventArgs e)
        {
            var path = SelectAudioFile();
            if (path == null) return;

            if (TryReplaceAudio(path, ref JoinChatReader, ref JoinChatPlayer, "チャット参加音"))
                DAO_Setting.InsertUpdate(DAO_Setting.SettingName.JoinChatAudioPath, path);
        }

        private static string? SelectAudioFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "音声ファイルを選択",
                Filter = "音声ファイル|*.wav;*.mp3;*.aac;*.wma;*.m4a|すべてのファイル|*.*",
                CheckFileExists = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private void LoadChatNotificationAudio()
        {
            var path = DAO_Setting.SelectOneById(
                DAO_Setting.SettingName.ChatNotificationAudioPath)?.Value;

            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path) &&
                TryReplaceAudio(path, ref ChatNotificationReader, ref ChatNotificationPlayer, "チャット通知音", false))
                return;

            ReplaceWithDefaultAudio(
                new WaveFileReader(Properties.Resources.CommentNotification),
                ref ChatNotificationReader,
                ref ChatNotificationPlayer);
        }

        private void LoadJoinChatAudio()
        {
            var path = DAO_Setting.SelectOneById(
                DAO_Setting.SettingName.JoinChatAudioPath)?.Value;

            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path) &&
                TryReplaceAudio(path, ref JoinChatReader, ref JoinChatPlayer, "チャット参加音", false))
                return;

            ReplaceWithDefaultAudio(
                new WaveFileReader(Properties.Resources.JoinChat),
                ref JoinChatReader,
                ref JoinChatPlayer);
        }

        private static bool TryReplaceAudio(
            string path,
            ref WaveStream? reader,
            ref WaveOutEvent? player,
            string audioName,
            bool showError = true)
        {
            AudioFileReader? newReader = null;
            WaveOutEvent? newPlayer = null;
            try
            {
                newReader = new AudioFileReader(path);
                newPlayer = new WaveOutEvent();
                newPlayer.Init(newReader);

                player?.Stop();
                player?.Dispose();
                reader?.Dispose();
                reader = newReader;
                player = newPlayer;
                return true;
            }
            catch (Exception ex)
            {
                newPlayer?.Dispose();
                newReader?.Dispose();
                if (showError)
                    MessageBox.Show($"{audioName}を読み込めませんでした。\n{ex.GetBaseException().Message}", "音声ファイル変更");
                return false;
            }
        }

        private static void ReplaceWithDefaultAudio(
            WaveStream newReader,
            ref WaveStream? reader,
            ref WaveOutEvent? player)
        {
            var newPlayer = new WaveOutEvent();
            newPlayer.Init(newReader);
            player?.Stop();
            player?.Dispose();
            reader?.Dispose();
            reader = newReader;
            player = newPlayer;
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
                CreateChatOverlayWindow();
            }
        }

        /// <summary>停止・破損した可能性があるオーバーレイウィンドウを破棄して作り直す。</summary>
        private void TransparentWindowRecreate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RecreateChatOverlayWindow();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"チャットオーバーレイを再生成できませんでした。\n{ex.GetBaseException().Message}",
                    "チャットオーバーレイ");
            }
        }

        private void RecreateChatOverlayWindow()
        {
            var oldWindow = transparentWindow;
            transparentWindow = null;
            try
            {
                oldWindow?.Close();
            }
            catch
            {
                // エラー停止したウィンドウは正常に閉じられない場合があるため、再生成を続行する。
            }

            CreateChatOverlayWindow();
        }

        private void CreateChatOverlayWindow()
        {
            var window = new ChatOverlayWindow(Application.Current.MainWindow, TwitchChatFormList);
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(transparentWindow, window))
                    transparentWindow = null;
            };
            transparentWindow = window;
            ApplyOverlayAppearance();
            window.Show();
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
