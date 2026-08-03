using JTSA.Utility;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using JTSA.Forms;

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
                        ChatAdd(new TwitchChatForm
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
                            IsVip = message.IsVip
                        });
                    });
                };

                twitchEventSubService.ChannelPointRedeemed += channelPoint =>
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        ChatAdd(new TwitchChatForm
                        {
                            UserName = channelPoint.UserLogin,
                            DisplayName = channelPoint.UserName,
                            Message = "CP交換：" + channelPoint.RewardTitle,
                        });
                    });
                };

                await twitchChatService.ConnectAsync();
                await twitchEventSubService.ConnectAsync();
            }
        }

        private void ChatAdd(TwitchChatForm form)
        {
            TwitchChatFormList.Add(form);
        }
    }
}
