using JTSA.Dao;
using JTSA.Forms;
using JTSA.Utility;
using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO.Packaging;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TwitchLib.Api.Helix;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace JTSA.Panels
{
    /// <summary>
    /// ChatPanel.xaml の相互作用ロジック
    /// </summary>
    public partial class ChatPanel : UserControl
    {
        private TwitchChatService? twitchChatService;

        private TwitchEventSubService? twitchEventSubService;

        public ObservableCollection<TwitchChatForm> TwitchChatFormList { get; } = new();


        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ChatPanel()
        {
            DataContext = this;

            InitializeComponent();
        }


        /// <summary>
        /// コントロール読み込み時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public async Task Initialize()
        {
            if(twitchChatService == null)
            {
                twitchChatService = new TwitchChatService("hiren_jak");
                twitchEventSubService = new TwitchEventSubService(TwitchHelper.api, TwitchHelper.BroadcasterId);

                twitchChatService.MessageReceived += message =>
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        ChatAddAsync(new TwitchChatForm
                        {
                            Channel = message.Channel,
                            UserId = message.UserId,
                            UserName = message.UserName,
                            DisplayName = message.DisplayName,
                            Message = message.Message,
                            ColorHex = message.ColorHex,
                            MessageId = message.MessageId,
                            IsModerator = message.IsModerator,
                            IsSubscriber = message.IsSubscriber,
                            IsVip = message.IsVip,
                        }, false);
                    });
                };

                twitchEventSubService.ChannelPointRedeemed += channelPoint =>
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        ChatAddAsync(new TwitchChatForm
                        {
                            UserName = channelPoint.UserLogin,
                            DisplayName = "ChannelPonint",
                            Message = channelPoint.RewardTitle + " by." + channelPoint.UserName,
                        }, true);
                    });
                };

                await twitchChatService.ConnectAsync();
                await twitchEventSubService.ConnectAsync();
            }
        }

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
                form.ColorHex = "#FFFFFF";
            }

            TwitchChatFormList.Insert(0, form);
        }
    }
}