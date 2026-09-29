using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using JTSA.Plugin.Abstractions;

namespace JTSA.DesktopWallPlugin;

public sealed class DesktopWallPlugin : IJtsaPlugin
{
    IJtsaPluginContext? context;
    CalendarBridgeServer? server;
    Window? window;
    TextBlock? status;
    Dispatcher? dispatcher;
    public string Id => "jtsa.desktop-wall";
    public string Name => "Desktop Wall 連携";
    public string Description => "同じPCのDesktop WallにJTSAのカレンダー予定を共有します。";
    public Version Version => new(1, 0, 0);

    public void Initialize(IJtsaPluginContext pluginContext)
    {
        context = pluginContext;
        dispatcher = Dispatcher.CurrentDispatcher;
        Start();
    }

    void Start()
    {
        if (server?.IsRunning == true) return;
        server?.Dispose();
        server = null;
        if (context is not IJtsaCalendarPluginContext calendar)
        {
            SetStatus("このJTSAはカレンダー共有に対応していません。");
            return;
        }
        try
        {
            server = new CalendarBridgeServer(calendar, dispatcher!, ex => context.LogError("Desktop Wall連携でエラーが発生しました。", ex));
            SetStatus("共有中 · Desktop Wallの設定で「JTSAの予定を表示」をオンにしてください。");
        }
        catch (Exception ex)
        {
            context.LogError("Desktop Wall連携を開始できませんでした。", ex);
            SetStatus("共有を開始できません。他のJTSAが起動していないか確認し、開始を押してください。");
        }
    }

    string message = "";
    void SetStatus(string value) { message = value; if (status != null) status.Text = value; }

    public void Open()
    {
        if (window != null) { window.WindowState = WindowState.Normal; window.Activate(); return; }
        status = new TextBlock { Text = server?.IsRunning == true ? message : "共有停止中。開始を押すと再接続します。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12) };
        var content = new StackPanel { Margin = new Thickness(20) };
        content.Children.Add(new TextBlock { Text = "Desktop Wall カレンダー共有", FontSize = 20 });
        content.Children.Add(status);
        content.Children.Add(new TextBlock { Text = "表示月の予定・開始時刻・タイトル・カテゴリを閲覧用に共有します。\nJTSAとDesktop Wallを同じWindowsユーザーで起動してください。\n拡張機能の読込時に共有を開始します。停止は次の再読込まで有効です。", TextWrapping = TextWrapping.Wrap });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        var start = new Button { Content = "共有を開始", Padding = new Thickness(12, 6, 12, 6) };
        var stop = new Button { Content = "共有を停止", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(8, 0, 0, 0) };
        start.Click += (_, _) => Start();
        stop.Click += (_, _) => { server?.Dispose(); server = null; SetStatus("共有停止中"); };
        buttons.Children.Add(start); buttons.Children.Add(stop); content.Children.Add(buttons);
        window = new Window { Title = Name, Width = 500, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.CanMinimize, Content = content };
        window.Closed += (_, _) => { window = null; status = null; };
        window.Show();
    }

    public void Shutdown()
    {
        server?.Dispose(); server = null;
        window?.Close(); window = null;
    }
}
