using JTSA.Utility;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
        public ChatPanel()
        {
            InitializeComponent();
        }

        private TwitchChatService? twitchChatService;

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            twitchChatService = new TwitchChatService("hiren_jak");

            twitchChatService.MessageReceived += message =>
            {
                Dispatcher.Invoke(() =>
                {
                    ChatListBox.Items.Add(
                        $"{message.DisplayName}: {message.Message}");

                    ChatListBox.ScrollIntoView(
                        ChatListBox.Items[^1]);
                });
            };

            await twitchChatService.ConnectAsync();
        }
    }
}
