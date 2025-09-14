using System.Threading.Tasks;
using System.Windows;
using Velopack;
using Velopack.Sources;

namespace JTSA
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            await UpdateCheck();
            base.OnStartup(e);
        }

        private static async Task UpdateCheck()
        {
            try
            {
                var mgr = new UpdateManager(
                    new GithubSource("https://github.com/hirenjak/JTSA", null, false),
                    new UpdateOptions
                    {
                        AllowVersionDowngrade = false
                    });

                if (!mgr.IsInstalled) return; // 開発実行などインストール外ならスキップ

                var info = await mgr.CheckForUpdatesAsync();
                if (info == null || info.TargetFullRelease == null) return; // 更新なし

                var latest = info.TargetFullRelease;
                var result = MessageBox.Show(
                    $"新しいバージョン（{latest.Version}）があります。アップデートしますか？",
                    "アップデート確認",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    await mgr.DownloadUpdatesAsync(info);
                    // 暗黙変換により UpdateInfo をそのまま渡せる
                    mgr.ApplyUpdatesAndRestart(info);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("アップデート確認中にエラーが発生しました: " + ex.Message);
            }
        }
    }
}
