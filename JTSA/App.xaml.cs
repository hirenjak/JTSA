using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Velopack;
using Velopack.Sources;

namespace JTSA
{
    public partial class App : Application
    {
        private static readonly object CrashLogLock = new();
        private static int isFatalErrorDialogOpen;

        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            {
                var exception = eventArgs.ExceptionObject as Exception
                    ?? new Exception(eventArgs.ExceptionObject?.ToString() ?? "不明な例外");
                WriteCrashLog("AppDomain.UnhandledException", exception);
            };

            try
            {
                VelopackApp.Build().Run();
                var app = new App();
                app.DispatcherUnhandledException += App_DispatcherUnhandledException;
                app.InitializeComponent();
                app.Run();
            }
            catch (Exception ex)
            {
                ShowFatalError("アプリケーションの起動に失敗しました。", "App.Main", ex);
            }
        }

        private static void App_DispatcherUnhandledException(
            object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            // MessageBox は独自のメッセージループを動かすため、表示中にもタイマー等から
            // 別の未処理例外が到着し得る。ダイアログの再帰的な増殖を防止する。
            if (Interlocked.Exchange(ref isFatalErrorDialogOpen, 1) != 0)
            {
                WriteCrashLog("DispatcherUnhandledException (reentrant)", e.Exception);
                return;
            }

            ShowFatalError(
                "予期しないエラーが発生したため、アプリケーションを終了します。",
                "DispatcherUnhandledException",
                e.Exception);
            Current?.Shutdown(-1);
        }

        private static void ShowFatalError(string message, string source, Exception exception)
        {
            var logPath = WriteCrashLog(source, exception);
            var logInformation = string.IsNullOrEmpty(logPath)
                ? "ログファイルの保存にも失敗しました。"
                : $"エラーログ:\n{logPath}";

            try
            {
                MessageBox.Show(
                    $"{message}\n\n{exception.Message}\n\n{logInformation}",
                    "JTSA 起動エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch
            {
                // OSの終了処理中など、ダイアログを表示できない場合はログだけを残す。
            }
        }

        private static string? WriteCrashLog(string source, Exception exception)
        {
            try
            {
                var logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "JTSA", "logs");
                Directory.CreateDirectory(logDirectory);

                var logPath = Path.Combine(logDirectory, $"crash-{DateTime.Now:yyyyMMdd}.log");
                var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                    ?? "不明";
                var contents = new StringBuilder()
                    .AppendLine("============================================================")
                    .AppendLine($"発生日時: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff zzz}")
                    .AppendLine($"発生箇所: {source}")
                    .AppendLine($"JTSA: {version}")
                    .AppendLine($"OS: {RuntimeInformation.OSDescription}")
                    .AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}")
                    .AppendLine($"Process: {RuntimeInformation.ProcessArchitecture}")
                    .AppendLine()
                    .AppendLine(exception.ToString())
                    .ToString();

                lock (CrashLogLock)
                {
                    File.AppendAllText(logPath, contents, Encoding.UTF8);
                }

                return logPath;
            }
            catch
            {
                return null;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
        }

        internal static async Task UpdateCheck(MainWindow window)
        {
            try
            {
                var mgr = new UpdateManager(
                    new GithubSource("https://github.com/hirenjak/JTSA", null, false),
                    new UpdateOptions
                    {
                        AllowVersionDowngrade = false
                    });

                if (!mgr.IsInstalled) return;

                var info = await mgr.CheckForUpdatesAsync();
                if (info == null || info.TargetFullRelease == null) return;

                var latest = info.TargetFullRelease;
                window.ShowNotification("update", "アプリの更新があります",
                    $"バージョン {latest.Version} を利用できます。更新するとJTSAが再起動します。", "更新する", async () =>
                {
                    await mgr.DownloadUpdatesAsync(info);
                    mgr.ApplyUpdatesAndRestart(info);
                });
            }
            catch (Exception ex)
            {
                window.ShowNotification("update-check", "更新を確認できませんでした", "通信状態を確認して再試行してください。", "再試行", async () =>
                {
                    window.RemoveNotification("update-check");
                    await UpdateCheck(window);
                });
            }
        }
    }
}
