using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace JTSA.Panels
{
    /// <summary>
    /// AppArrangePanel.xaml の相互作用ロジック
    /// </summary>
    public partial class AppArrangePanel : UserControl
    {
        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;

        // Win32 APIの宣言
        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(System.Drawing.Point p);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out System.Drawing.Point lpPoint);

        // 既存のP/Invokeの下に追加
        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
        private const uint GA_ROOT = 2;

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);   // 最小化？

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);   // 最大化？
        
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        private const int SW_RESTORE = 9;

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        private bool isWaitingForAppClick = false;
        private System.Windows.Threading.DispatcherTimer mouseHookTimer;


        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }


        // アプリ情報用クラス
        public class AppInfo
        {
            public string ProcessName { get; set; }
            public string WindowTitle { get; set; }
            public int? X { get; set; }
            public int? Y { get; set; }
            public int? Width { get; set; }
            public int? Height { get; set; }
        }


        /// <summary> 登録アプリリスト </summary>
        public ObservableCollection<AppInfo> RegistAppList { get; set; } = new();

        /// <summary> 起動中アプリリスト </summary>
        public ObservableCollection<AppInfo> RunAppList { get; set; } = new();

        public AppArrangePanel()
        {
            InitializeComponent();

            DataContext = this;
        }


        #region =============== メソッド ===============

        /// <summary>
        /// クリック位置から最上位ウィンドウを取得
        /// </summary>
        /// <param name="pt"></param>
        /// <returns></returns>
        private static IntPtr GetTopLevelWindowFromPoint(System.Drawing.Point pt)
        {
            var child = WindowFromPoint(pt);
            if (child == IntPtr.Zero) return IntPtr.Zero;
            return GetAncestor(child, GA_ROOT);
        }


        /// <summary>
        /// AppInfo を受けて「タイトル一致」を優先してプロセスを見つける
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        private static Process? FindProcessFor(AppInfo app)
        {
            var list = Process.GetProcessesByName(app.ProcessName);
            // タイトル完全一致を最優先
            var exact = list.FirstOrDefault(p => !string.IsNullOrEmpty(p.MainWindowTitle)
                                              && p.MainWindowTitle == app.WindowTitle);
            if (exact != null) return exact;

            // タイトル部分一致（保険）
            var partial = list.FirstOrDefault(p => !string.IsNullOrEmpty(p.MainWindowTitle)
                                                && app.WindowTitle != null
                                                && p.MainWindowTitle.Contains(app.WindowTitle));
            if (partial != null) return partial;

            // 最後の手段：メインウィンドウハンドルがあるもの
            return list.FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
        }


        /// <summary>
        /// カーソル位置のウィンドウをリストに登録
        /// </summary>
        public void RegisterAppUnderCursor()
        {
            if (!GetCursorPos(out System.Drawing.Point cursorPos)) return;

            IntPtr hWnd = GetTopLevelWindowFromPoint(cursorPos); // ★ 最上位を取る
            if (hWnd == IntPtr.Zero) return;

            // 自分自身は除外
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == (uint)Process.GetCurrentProcess().Id) return;

            // タイトル
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);

            // プロセス
            var proc = Process.GetProcessById((int)pid);

            // 位置サイズ
            if (!GetWindowRect(hWnd, out RECT r)) return;
            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;

            // 重複（同名＆同タイトル）を除外
            var dup = RegistAppList.FirstOrDefault(x => x.ProcessName == proc.ProcessName && x.WindowTitle == sb.ToString());
            if (dup != null) RegistAppList.Remove(dup);

            RegistAppList.Add(new AppInfo
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
        /// ウィンドウ位置を取得して記録
        /// </summary>
        /// <param name="processName"></param>
        /// <returns></returns>
        public (int X, int Y, int Width, int Height)? GetAppWindowRect(string processName)
        {
            var proc = Process.GetProcessesByName(processName).FirstOrDefault();
            if (proc == null || proc.MainWindowHandle == IntPtr.Zero) return null;

            if (GetWindowRect(proc.MainWindowHandle, out RECT rect))
            {
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                return (rect.Left, rect.Top, width, height);
            }
            return null;
        }


        /// <summary>
        /// ウィンドウ位置を制御
        /// </summary>
        /// <param name="processName"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public bool SetAppWindowRect(string processName, int x, int y, int width, int height)
        {
            var proc = Process.GetProcessesByName(processName).FirstOrDefault();
            if (proc == null || proc.MainWindowHandle == IntPtr.Zero) return false;

            return SetWindowPos(proc.MainWindowHandle, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
        }


        /// <summary>
        /// 
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
                            RunAppList.Add(new AppInfo
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
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MouseHookTimer_Tick(object? sender, EventArgs e)
        {
            const int VK_LBUTTON = 0x01;
            if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) == 0) return;

            mouseHookTimer.Stop();
            isWaitingForAppClick = false;

            if (!GetCursorPos(out System.Drawing.Point cursorPos)) return;

            IntPtr hWnd = GetTopLevelWindowFromPoint(cursorPos); // ★
            if (hWnd == IntPtr.Zero) return;

            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == (uint)Process.GetCurrentProcess().Id) return; // ★ 自アプリ除外

            var sb = new System.Text.StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);

            var proc = Process.GetProcessById((int)pid);

            if (!GetWindowRect(hWnd, out RECT r)) return;
            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;

            var dup = RegistAppList.FirstOrDefault(x => x.ProcessName == proc.ProcessName && x.WindowTitle == sb.ToString());
            if (dup != null) RegistAppList.Remove(dup);

            RegistAppList.Add(new AppInfo
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


        public bool SetAppWindowRect(AppInfo app)
        {
            if (!(app.X.HasValue && app.Y.HasValue && app.Width.HasValue && app.Height.HasValue))
                return false;

            var proc = FindProcessFor(app);
            if (proc == null || proc.MainWindowHandle == IntPtr.Zero) return false;

            var hWnd = proc.MainWindowHandle;

            // 最小化/最大化解除
            if (IsIconic(hWnd) || IsZoomed(hWnd))
            {
                ShowWindow(hWnd, SW_RESTORE);
                // ほんの少し待たせたいなら DispatcherTimer/Task.Delay を使う
            }

            return SetWindowPos(hWnd, IntPtr.Zero, app.X.Value, app.Y.Value, app.Width.Value, app.Height.Value,
                                SWP_NOZORDER | SWP_NOACTIVATE);
        }

        #endregion


        /// <summary>
        /// 登録リストボックス：アイテム選択時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegistAppListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RegistAppListBox.SelectedItem is AppInfo app)
            {
                if (!SetAppWindowRect(app))
                {
                    mainWindow.StatusTextBlock.Text = "移動失敗：対象が起動中か、権限/タイトル一致をご確認ください。";
                }
            }
        }


        /// <summary>
        /// 登録ボタン：クリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegisterAppButton_Click(object sender, RoutedEventArgs e)
        {
            isWaitingForAppClick = true;
            mouseHookTimer = new System.Windows.Threading.DispatcherTimer();
            mouseHookTimer.Interval = TimeSpan.FromMilliseconds(50);
            mouseHookTimer.Tick += MouseHookTimer_Tick;
            mouseHookTimer.Start();
            MessageBox.Show("登録したいアプリのウィンドウをクリックしてください。");
        }


        /// <summary>
        /// 削除ボタン：クリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegistAppDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (RegistAppListBox.SelectedItem is AppInfo app)
                RegistAppList.Remove(app);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RunAppButton_Click(object sender, RoutedEventArgs e)
        {
            LoadRunningApps();
        }





        /// <summary>
        /// 起動中アプリリスト削除ボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RunAppDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (RunAppListBox.SelectedItem is AppInfo app)
                RunAppList.Remove(app);
        }


        /// <summary>
        /// 起動中アプリリスト選択時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RunAppListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RunAppListBox.SelectedItem is AppInfo run)
            {
                var rect = GetAppWindowRect(run.ProcessName);

                var item = new AppInfo
                {
                    ProcessName = run.ProcessName,
                    WindowTitle = run.WindowTitle,
                    X = rect?.X,
                    Y = rect?.Y,
                    Width = rect?.Width,
                    Height = rect?.Height
                };

                var dup = RegistAppList.FirstOrDefault(x => x.ProcessName == item.ProcessName && x.WindowTitle == item.WindowTitle);
                if (dup != null) RegistAppList.Remove(dup);

                RegistAppList.Add(item);
            }
        }
    }
}
