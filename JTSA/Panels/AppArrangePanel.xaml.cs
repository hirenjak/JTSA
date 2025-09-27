using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

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


        /// <summary> マウスでの選択用 </summary>
        //private DispatcherTimer mouseHookTimer;

        // タイマー用フィールドを追加
        private DispatcherTimer statusUpdateTimer;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public AppArrangePanel()
        {
            InitializeComponent();

            DataContext = this;

            ReloadRegistAppList();

            // タイマー初期化（例：5秒ごとに更新）
            statusUpdateTimer = new DispatcherTimer();
            statusUpdateTimer.Interval = TimeSpan.FromSeconds(1);
            statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
            statusUpdateTimer.Start();
        }


        #region =============== メソッド ===============

        /// <summary>
        /// ウィンドウ位置を取得して記録
        /// </summary>
        /// <param name="processName"></param>
        /// <returns></returns>
        public (int X, int Y, int Width, int Height) GetAppWindowRect(string processName)
        {
            var proc = Process.GetProcessesByName(processName).FirstOrDefault();
            if (proc == null || proc.MainWindowHandle == IntPtr.Zero) return new (0, 0, 0, 0);

            if (Win32Helper.GetWindowRect(proc.MainWindowHandle, out RECT rect))
            {
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                return (rect.Left, rect.Top, width, height);
            }

            return new(0, 0, 0, 0);
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
                    AppExePath = record.AppExePath,
                    X = record.X,
                    Y = record.Y,
                    Width = record.Width,
                    Height = record.Height
                });
            }

            UpdateRegistAppStatus();
        }


        /// <summary>
        /// タイマーTickイベントで状態更新
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StatusUpdateTimer_Tick(object? sender, EventArgs e)
        {
            UpdateRegistAppStatus();
        }


        /// <summary>
        /// 
        /// </summary>
        public void UpdateRegistAppStatus()
        {
            foreach (var app in RegistAppList)
            {
                var procs = Process.GetProcessesByName(app.ProcessName);
                if (procs.Length == 0)
                {
                    app.Status = "停止";
                    app.OldStatus = "停止";
                }
                else
                {
                    // ウィンドウハンドルが有効かどうかで判定
                    var proc = procs.FirstOrDefault(p => p.MainWindowTitle == app.WindowTitle);
                    if (proc == null)
                    {
                        if (app.OldStatus == "起動中")
                        {
                            app.Status = "停止途中";
                        }
                        else
                        {
                            app.Status = "起動途中";
                        }
                    }
                    else if (proc.MainWindowHandle != IntPtr.Zero)
                    {
                        app.Status = "起動中";
                        app.OldStatus = "起動中";
                    }
                    else
                    {
                        if (app.OldStatus == "起動中")
                        {
                            app.Status = "停止途中";
                        }
                        else
                        {
                            app.Status = "起動途中";
                        }
                    }
                }
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

            //mouseHookTimer.Stop();

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
                // 停止状態なら起動
                if (app.Status == "停止" && !string.IsNullOrEmpty(app.AppExePath))
                {
                    mainWindow.AppLogPanel.AddSwitchLog(RegistListRunStart(app.AppExePath), GetType().Name,
                        $"アプリを起動しました：{app.ProcessName}",
                        $"起動失敗"
                    );
                }
                else
                {
                    // ウィンドウ移動処理
                    mainWindow.AppLogPanel.AddSwitchLog(Win32Helper.SetAppWindowRect(app), GetType().Name,
                        $"アプリを移動しました：{app.ProcessName}",
                        $"移動失敗：対象が起動中か、権限/タイトル一致をご確認ください。"
                    );
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="exePath"></param>
        private bool RegistListRunStart(string exePath)
        {
            try
            {
                string obsExe = exePath; // 実環境のパスに
                var psi = new ProcessStartInfo
                {
                    FileName = obsExe,
                    WorkingDirectory = Path.GetDirectoryName(obsExe), // ★これが重要
                    UseShellExecute = false,                          // true でもよいが下記注意
                                                                        // 例: 引数を付けたい場合
                    Arguments = "--multi --startvirtualcam"           // 任意
                };

                Process.Start(psi);

                return true;
            }
            catch (Exception)
            {
                return false;
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

                var proc = Process.GetProcessesByName(run.ProcessName).FirstOrDefault();

#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
                string exePath = proc.MainModule.FileName;
#pragma warning restore CS8602 // null 参照の可能性があるものの逆参照です。

                M_StreamWindow.Insert(
                    new M_StreamWindow
                    {
                        ProcessName = run.ProcessName,
                        WindowTitle = run.WindowTitle,
                        AppExePath = exePath,
                        X = (int)rect.X,
                        Y = (int)rect.Y,
                        Width = (int)rect.Width,
                        Height = (int)rect.Height,
                        CreatedDateTime = DateTime.Now,
                        UpdateDateTime = DateTime.Now
                    }
                );
            }

            // 選択状態を解除
            RunAppListBox.SelectedIndex = -1;

            ReloadRegistAppList();
        }


        /// <summary>
        /// リストボックスアイテムクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RunAppListBox_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // クリックされたアイテムを取得
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var item = ItemsControl.ContainerFromElement(listBox, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item == null) return;

            // すでに選択されている場合は一度選択解除
            if (item != null && item.IsSelected)
            {
                listBox.SelectedIndex = -1;
            }
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

                var target = new M_StreamWindow()
                {
                    ProcessName = item.ProcessName,
                    WindowTitle = item.WindowTitle,
                    AppExePath = item.AppExePath,
                    X = (int)rect.X,
                    Y = (int)rect.Y,
                    Width = (int)rect.Width,
                    Height = (int)rect.Height,
                    CreatedDateTime = DateTime.Now,
                    UpdateDateTime = DateTime.Now
                };

                M_StreamWindow.Update(target);
            }

            // 選択状態を解除
            RunAppListBox.SelectedIndex = -1;

            ReloadRegistAppList();
        }


        /// <summary>
        /// リストボックスアイテムクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegistAppListBox_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // クリックされたアイテムを取得
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var item = ItemsControl.ContainerFromElement(listBox, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item == null) return;

            // すでに選択されている場合は一度選択解除
            if (item != null && item.IsSelected)
            {
                listBox.SelectedIndex = -1;
            }
        }


        /// <summary>
        /// 登録アプリ一括設定ボタン：クリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegisterAppAllSetButton_Click(object sender, RoutedEventArgs e)
        {
            RegistAppAllMove();
        }


        /// <summary>
        /// 
        /// </summary>
        public void RegistAppAllMove()
        {
            foreach (var item in RegistAppList)
            {
                Win32Helper.SetAppWindowRect(item);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegistAppStopButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is AppInfoForm item)
            {
                RegistAppStop(item);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="target"></param>
        private void RegistAppStop(AppInfoForm target)
        {
            // 起動中のみ停止可能
            if (target.Status == "起動中")
            {
                try
                {
                    // プロセス名とウィンドウタイトルで該当プロセスを特定
                    var procs = Process.GetProcessesByName(target.ProcessName);
                    foreach (var proc in procs)
                    {
                        if (proc.MainWindowTitle == target.WindowTitle)
                        {
                            proc.Kill();
                            mainWindow.StatusTextBlock.Text = $"アプリを停止しました: {target.ProcessName}";
                            mainWindow.StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
                            return;
                        }
                    }
                    mainWindow.StatusTextBlock.Text = "該当プロセスが見つかりませんでした。";
                    mainWindow.StatusTextBlock.Foreground = System.Windows.Media.Brushes.OrangeRed;
                }
                catch (Exception ex)
                {
                    mainWindow.StatusTextBlock.Text = $"停止失敗: {ex.Message}";
                    mainWindow.StatusTextBlock.Foreground = System.Windows.Media.Brushes.OrangeRed;
                }
            }
            else
            {
                mainWindow.StatusTextBlock.Text = "アプリが起動中でないため停止できません。";
                mainWindow.StatusTextBlock.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegistAppExePathResetButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is AppInfoForm item)
            {
                var dialog = new OpenFileDialog();
                dialog.Filter = "実行ファイル (*.exe)|*.exe";
                dialog.Title = "アプリの実行ファイルを選択してください";

                // 既に設定済みなら初期表示をその場所に
                if (!string.IsNullOrEmpty(item.AppExePath))
                {
                    try
                    {
                        dialog.InitialDirectory = System.IO.Path.GetDirectoryName(item.AppExePath);
                        dialog.FileName = System.IO.Path.GetFileName(item.AppExePath);
                    }
                    catch { /* パスが不正な場合は無視 */ }
                }

                if (dialog.ShowDialog() == true)
                {
                    item.AppExePath = dialog.FileName;
                    mainWindow.StatusTextBlock.Text = $"起動ファイルを設定しました: {item.AppExePath}";
                    // 必要ならDBにも保存
                    M_StreamWindow.Update(new M_StreamWindow
                    {
                        ProcessName = item.ProcessName,
                        WindowTitle = item.WindowTitle,
                        AppExePath = item.AppExePath,
                        X = item.X ?? 0,
                        Y = item.Y ?? 0,
                        Width = item.Width ?? 0,
                        Height = item.Height ?? 0,
                        UpdateDateTime = DateTime.Now
                    });
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegisterAllAppStartButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.StatusTextBlock.Text = "アプリ起動中...";
            mainWindow.StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;

            foreach (var item in RegistAppList)
            {
                RegistListRunStart(item.AppExePath);
            }

            mainWindow.StatusTextBlock.Text = "アプリ起動完了";
            mainWindow.StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
        }


        /// <summary>
        /// 
        /// </summary>
        public void RegistAllAppStart()
        {
            foreach (var item in RegistAppList)
            {
                RegistListRunStart(item.AppExePath);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegisterAllAppStopButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.StatusTextBlock.Text = "アプリ停止中...";
            mainWindow.StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;

            RegistAllAppStop();

            mainWindow.StatusTextBlock.Text = "アプリ停止完了";
            mainWindow.StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
        }


        /// <summary>
        /// 
        /// </summary>
        public void RegistAllAppStop()
        {
            foreach (var item in RegistAppList)
            {
                RegistAppStop(item);
            }
        }
    }
}
