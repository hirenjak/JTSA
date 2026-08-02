using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TwitchLib.Client;

namespace JTSA.Panels
{
    /// <summary>
    /// UserControl1.xaml の相互作用ロジック
    /// </summary>
    public partial class UserControl1 : UserControl
    {
        public ObservableCollection<ChatListForm> ChatListForms { get; set; }
        public UserControl1()
        {
            InitializeComponent();

            DataContext = this;
            var client = new TwitchClient();
            client.OnMessageReceived += Client_OnMessageReceived;
        }

        public event Action<TwitchChatData>? MessageReceived;

        private Task Client_OnMessageReceived(object? sender, TwitchLib.Client.Events.OnMessageReceivedArgs e)
        {
            var message = e.ChatMessage;

            var data = new TwitchChatData
            {
                Channel = message.Channel,
                UserId = message.UserId,
                UserName = message.Username,
                DisplayName = message.DisplayName,
                Message = message.Message,
                ColorHex = message.HexColor,
                MessageId = message.Id,

                IsModerator = message.UserDetail.IsModerator,
                IsSubscriber = message.UserDetail.IsSubscriber,
                IsVip = message.UserDetail.IsVip
            };

            ChatListForms.Add(new ChatListForm()
            {
                UserName = data.UserName,
                Comment = data.Message
            });

            return Task.CompletedTask;
        }
    }


    public sealed class TwitchChatData
    {
        public string Channel { get; set; } = "";
        public string UserId { get; set; } = "";
        public string UserName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Message { get; set; } = "";
        public string ColorHex { get; set; } = "";
        public string MessageId { get; set; } = "";

        public bool IsModerator { get; set; }
        public bool IsSubscriber { get; set; }
        public bool IsVip { get; set; }
    }

    public class ChatListForm()
    {
        public string UserName;
        public string Comment;
        }
    
}
