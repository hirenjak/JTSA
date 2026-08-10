using JTSA.Dao;
using JTSA.Forms;
using JTSA.Models;
using JTSA.Utility;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using NAudio.Wave;

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
        private TwitchChatService? twitchChatService;

        private TwitchEventSubService? twitchEventSubService;

        public ObservableCollection<TwitchChatForm> TwitchChatFormList { get; } = new();

        public ObservableCollection<TwitchChatForm> PinedTwitchChatFormList { get; } = new();

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
            var result = await TwitchHelper.PinedChat(chatId);

            sendChatTextBox.Text = string.Empty;
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
            ChatNotificationVolumeSlider.Value =
               double.Parse(DAO_Setting.SelectOneById(DAO_Setting.SettingName.ChatNotificationVolume)?.Value ?? "50");

            JoinChatVolumeSlider.Value =
               double.Parse(DAO_Setting.SelectOneById(DAO_Setting.SettingName.JoinChatVolume)?.Value ?? "50");

            DAO_ChatUser.AllDelete();
            StreamSupportTracker.Reset();

            if(twitchChatService == null)
            {
                twitchChatService = new TwitchChatService(JTSAHelper.LoginName);
                twitchEventSubService = new TwitchEventSubService(TwitchHelper.api, TwitchHelper.BroadcasterId);

                twitchChatService.MessageReceived += message =>
                {
                    Dispatcher.InvokeAsync(() =>
                    {
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
                };

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
                };

                await twitchChatService.ConnectAsync();
                await twitchEventSubService.ConnectAsync();
            }


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
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="form"></param>
        /// <param name="isChannelPoint"></param>
        private async void ChatAddAsync(TwitchChatForm form, bool isChannelPoint)
        {
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
                    LastUsedDateTime = DateTime.Now,
                    CreatedDateTime = DateTime.Now,
                    UpdatedDateTime = DateTime.Now
                };

                DAO_User.Insert(insertData);

                userData = insertData;
            }

            form.ProfielImageUrl = userData?.ProfielImageUrl.Replace("-300x300.png", "-70x70.png");
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

            TwitchChatFormList.Insert(0, form);

            await PinedChatLoad();
        }

        private async Task PinedChatLoad()
        {
            PinedTwitchChatFormList.Clear();

            var message = await TwitchHelper.GetPinedChat();

            PinedTwitchChatFormList.Add(message);
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
        ChatOverlayWindow transparentWindow;


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
            await TwitchHelper.PinedDeleteChat();
        }
    }
}
