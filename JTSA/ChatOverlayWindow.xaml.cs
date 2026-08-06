using JTSA.Forms;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace JTSA
{
    /// <summary>
    /// ChatOverlayWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class ChatOverlayWindow : Window
    {
        public ObservableCollection<TwitchChatForm> TwitchChatFormList { get; } = new();
        public ObservableCollection<TwitchChatForm> OverlayTwitchChatFormList { get; } = new();
        
        public ChatOverlayWindow(Window owner, ObservableCollection<TwitchChatForm> twitchChatFormList)
        {
            this.Owner = owner;
            InitializeComponent();

            TwitchChatFormList = twitchChatFormList;
            
            DataContext = this;


            SourceInitialized += ChatOverlayWindow_SourceInitialized;

            TwitchChatFormList.CollectionChanged += TwitchChatFormList_CollectionChanged;


            Loaded += ChatOverlayWindow_Loaded;

        }

        private void ChatOverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            IsSettingEnabled = false;
            SetClickThrough(IsSettingEnabled);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TwitchChatFormList_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add &&
                e.NewItems is not null)
            {
                foreach (TwitchChatForm item in e.NewItems)
                {
                    // 新着を先頭に追加
                    OverlayTwitchChatFormList.Add(item);

                    OverlayTwitchChatListBox.ScrollIntoView(OverlayTwitchChatListBox.Items[^1]);
                }
            }
        }


        #region クリック貫通用

        private const int GwlExstyle = -20;

        private const int WsExTransparent = 0x00000020;
        private const int WsExLayered = 0x00080000;
        private const int WsExToolwindow = 0x00000080;


        private void ChatOverlayWindow_SourceInitialized(
            object? sender,
            EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            var extendedStyle = GetWindowLongPtr(hwnd, GwlExstyle).ToInt64();

            extendedStyle |= WsExTransparent;
            extendedStyle |= WsExLayered;
            extendedStyle |= WsExToolwindow;

            SetWindowLongPtr(
                hwnd,
                GwlExstyle,
                new IntPtr(extendedStyle));
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(
            IntPtr hwnd,
            int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(
            IntPtr hwnd,
            int index);

        private static IntPtr GetWindowLongPtr(
            IntPtr hwnd,
            int index)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hwnd, index)
                : GetWindowLongPtr32(hwnd, index);
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(
            IntPtr hwnd,
            int index,
            IntPtr newStyle);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLongPtr32(
            IntPtr hwnd,
            int index,
            IntPtr newStyle);

        private static IntPtr SetWindowLongPtr(
            IntPtr hwnd,
            int index,
            IntPtr newStyle)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hwnd, index, newStyle)
                : SetWindowLongPtr32(hwnd, index, newStyle);
        }
        #endregion

        #region クリック貫通切替用
        private const long WsExTransparentLong = 0x00000020;

        private bool isSettingEnabled;
        public bool IsSettingEnabled { 
            get { return isSettingEnabled; } 
            set 
            {
                isSettingEnabled = value;
                if (value)
                {
                    WindowBackgroundBorder.Background = new SolidColorBrush(Color.FromArgb(0x44, 0x00, 0x00, 0x00));
                }
                else
                {
                    WindowBackgroundBorder.Background = new SolidColorBrush(Color.FromArgb(0x44, 0x11, 0x11, 0x44));
                }
            } 
        }

        public void SwitchSettingClick()
        {
            if(IsSettingEnabled)
            {
                SetClickThrough(false);
                IsSettingEnabled = false;
            }
            else
            {
                SetClickThrough(true);
                IsSettingEnabled = true;
            }
        }

        public void SetClickThrough(bool enabled)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            var style = GetWindowLongPtr(hwnd, GwlExstyle).ToInt64();

            if (enabled)
            {
                style |= WsExTransparentLong;
            }
            else
            {
                style &= ~WsExTransparentLong;
            }

            SetWindowLongPtr(
                hwnd,
                GwlExstyle,
                new IntPtr(style));
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
