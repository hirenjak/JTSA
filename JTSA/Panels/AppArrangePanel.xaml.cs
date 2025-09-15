using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace JTSA.Panels
{
    /// <summary>
    /// 配信時アプリ配置パネル
    /// </summary>
    public partial class AppArrangePanel : UserControl
    {
        /// <summary> メインウィンドウ </summary>
        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;


        /// <summary> 登録アプリリスト </summary>
        public ObservableCollection<AppInfoForm> RegistAppList { get; set; } = new();

        /// <summary> 起動中アプリリスト </summary>
        public ObservableCollection<AppInfoForm> RunAppList { get; set; } = new();


        private bool isWaitingForAppClick = false;
        private System.Windows.Threading.DispatcherTimer mouseHookTimer;


        /// <summary>
        /// コンストラクタ
        /// </summary>
        public AppArrangePanel()
        {
            InitializeComponent();

            DataContext = this;

            ReloadRegistAppList();
        }


        #region =============== メソッド ===============

        /// <summary>
        /// ウィンドウ位置を取得して記録
        /// </summary>
        /// <param name="processName"></param>
        /// <returns></returns>
        public (int X, int Y, int Width, int Height)? GetAppWindowRect(string processName)
        {
            var proc = Process.GetProcessesByName(processName).FirstOrDefault();
            if (proc == null || proc.MainWindowHandle == IntPtr.Zero) return null;

            if (Win32Helper.GetWindowRect(proc.MainWindowHandle, out RECT rect))
            {
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                return (rect.Left, rect.Top, width, height);
            }
            return null;
        }


        /// <summary>
        /// 現在起動しているアプリのリストを取得
        /// </summary>
        public void LoadRunningApps()
        {
            RunAppList.Clear();

            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (proc.MainWindowHandle != IntPtr.Zero)
                    {
                        string title = proc.MainWindowTitle;
                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            RunAppList.Add(new AppInfoForm
                            {
                                ProcessName = proc.ProcessName,
                                WindowTitle = title
                            });
                        }
                    }
                }
                catch
                {
                    // アクセス拒否などの例外は無視
                }
            }
        }


        /// <summary>
        /// 登録アプリリストを再読み込み処理
        /// </summary>
        private void ReloadRegistAppList()
        {
            RegistAppList.Clear();

            foreach (var record in M_StreamWindow.SelectAllOrderbyProcessName())
            {
                RegistAppList.Add(new AppInfoForm
                {
                    ProcessName = record.ProcessName,
                    WindowTitle = record.WindowTitle,
                    X = record.X,
                    Y = record.Y,
                    Width = record.Width,
                    Height = record.Height
                });
            }
        }

        #endregion


        #region ============== アプリクリック取得処理関連 ===============

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MouseHookTimer_Tick(object? sender, EventArgs e)
        {
            const int VK_LBUTTON = 0x01;
            if ((Win32Helper.GetAsyncKeyState(VK_LBUTTON) & 0x8000) == 0) return;

            mouseHookTimer.Stop();
            isWaitingForAppClick = false;

            if (!Win32Helper.GetCursorPos(out System.Drawing.Point cursorPos)) return;

            IntPtr hWnd = GetTopLevelWindowFromPoint(cursorPos); // ★
            if (hWnd == IntPtr.Zero) return;

            Win32Helper.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == (uint)Process.GetCurrentProcess().Id) return; // ★ 自アプリ除外

            var sb = new System.Text.StringBuilder(256);
            Win32Helper.GetWindowText(hWnd, sb, sb.Capacity);

            var proc = Process.GetProcessById((int)pid);

            if (!Win32Helper.GetWindowRect(hWnd, out RECT r)) return;
            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;

            var dup = RegistAppList.FirstOrDefault(x => x.ProcessName == proc.ProcessName && x.WindowTitle == sb.ToString());
            if (dup != null) RegistAppList.Remove(dup);

            RegistAppList.Add(new AppInfoForm
            {
                ProcessName = proc.ProcessName,
                WindowTitle = sb.ToString(),
                X = r.Left,
                Y = r.Top,
                Width = w,
                Height = h
            });

            MessageBox.Show("アプリを登録しました: " + sb.ToString());
        }


        /// <summary>
        /// カーソル位置のウィンドウをリストに登録
        /// </summary>
        public void RegisterAppUnderCursor()
        {
            if (!Win32Helper.GetCursorPos(out System.Drawing.Point cursorPos)) return;

            IntPtr hWnd = GetTopLevelWindowFromPoint(cursorPos); // ★ 最上位を取る
            if (hWnd == IntPtr.Zero) return;

            // 自分自身は除外
            Win32Helper.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == (uint)Process.GetCurrentProcess().Id) return;

            // タイトル
            var sb = new System.Text.StringBuilder(256);
            Win32Helper.GetWindowText(hWnd, sb, sb.Capacity);

            // プロセス
            var proc = Process.GetProcessById((int)pid);

            // 位置サイズ
            if (!Win32Helper.GetWindowRect(hWnd, out RECT r)) return;
            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;

            // 重複（同名＆同タイトル）を除外
            var dup = RegistAppList.FirstOrDefault(x => x.ProcessName == proc.ProcessName && x.WindowTitle == sb.ToString());
            if (dup != null) RegistAppList.Remove(dup);

            RegistAppList.Add(new AppInfoForm
            {
                ProcessName = proc.ProcessName,
                WindowTitle = sb.ToString(),
                X = r.Left,
                Y = r.Top,
                Width = w,
                Height = h
            });
        }


        /// <summary>
        /// クリック位置から最上位ウィンドウを取得
        /// </summary>
        /// <param name="pt"></param>
        /// <returns></returns>
        private static IntPtr GetTopLevelWindowFromPoint(System.Drawing.Point pt)
        {
            var child = Win32Helper.WindowFromPoint(pt);
            if (child == IntPtr.Zero) return IntPtr.Zero;
            return Win32Helper.GetAncestor(child, Win32Helper.GA_ROOT);
        }

        #endregion


        /// <summary>
        /// 登録リストボックス：アイテム選択時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegistAppListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RegistAppListBox.SelectedItem is AppInfoForm app)
            {
                if (!Win32Helper.SetAppWindowRect(app))
                {
                    mainWindow.StatusTextBlock.Text = "移動失敗：対象が起動中か、権限/タイトル一致をご確認ください。";
                }
            }
        }


        /// <summary>
        /// 登録ボタン：クリック時
        /// （TODO：未実装）
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegisterAppButton_Click(object sender, RoutedEventArgs e)
        {
            //TODO：直接指定の登録

            //isWaitingForAppClick = true;
            //mouseHookTimer = new System.Windows.Threading.DispatcherTimer();
            //mouseHookTimer.Interval = TimeSpan.FromMilliseconds(50);
            //mouseHookTimer.Tick += MouseHookTimer_Tick;
            //mouseHookTimer.Start();
            //MessageBox.Show("登録したいアプリのウィンドウをクリックしてください。");
        }


        /// <summary>
        /// 登録アプリ削除ボタン：クリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegistAppDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is AppInfoForm item)
            {
                M_StreamWindow.Delete(item.ProcessName);
            }

            ReloadRegistAppList();
        }


        /// <summary>
        /// 起動中アプリリスト更新ボタン：クリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RunAppButton_Click(object sender, RoutedEventArgs e)
        {
            LoadRunningApps();
        }


        /// <summary>
        /// 起動中アプリリスト：選択時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RunAppListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RunAppListBox.SelectedItem is AppInfoForm run)
            {
                var rect = GetAppWindowRect(run.ProcessName);

                M_StreamWindow.Insert(
                    new M_StreamWindow
                    {
                        ProcessName = run.ProcessName,
                        WindowTitle = run.WindowTitle,
                        X = (int)rect?.X,
                        Y = (int)rect?.Y,
                        Width = (int)rect?.Width,
                        Height = (int)rect?.Height,
                        CreatedDateTime = DateTime.Now,
                        UpdateDateTime = DateTime.Now
                    }
                );
            }

            ReloadRegistAppList();
        }


        /// <summary>
        /// 登録アプリ再設定ボタン：クリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegistAppResetButton_Click(object sender, RoutedEventArgs e)
        {
            // ボタンのDataContextから削除対象を取得
            if ((sender as Button)?.DataContext is AppInfoForm item)
            {
                var rect = GetAppWindowRect(item.ProcessName);
                if (rect.HasValue)
                {
                    var target = new M_StreamWindow()
                    {
                        ProcessName = item.ProcessName,
                        WindowTitle = item.WindowTitle,
                        X = (int)rect?.X,
                        Y = (int)rect?.Y,
                        Width = (int)rect?.Width,
                        Height = (int)rect?.Height,
                        CreatedDateTime = DateTime.Now,
                        UpdateDateTime = DateTime.Now
                    };

                    M_StreamWindow.Update(target);
                }
            }

            ReloadRegistAppList();
        }


        /// <summary>
        /// 登録アプリ一括設定ボタン：クリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegisterAppAllSetButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in RegistAppList)
            {
                if (!Win32Helper.SetAppWindowRect(item))
                {
                    mainWindow.StatusTextBlock.Text = "移動失敗：対象が起動中か、権限/タイトル一致をご確認ください。";
                }
            }
        }
    }
}
