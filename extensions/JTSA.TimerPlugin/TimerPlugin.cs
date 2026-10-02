using JTSA.Plugin.Abstractions;
using System.Windows;
using System.Text.Json;

namespace JTSA.TimerPlugin;

public sealed class TimerPlugin : IJtsaPlugin
{
    private IJtsaPluginContext? context;
    private TimerWindow? window;

    public string Id => "jtsa.timer";
    public string Name => "タイマー";
    public string Description => "配信中に使える独立ウィンドウのカウントダウンタイマーです。";
    public Version Version => new(1, 0, 20260929);

    public void Initialize(IJtsaPluginContext pluginContext)
    {
        context = pluginContext;
        if (pluginContext is IJtsaRemotePanelPluginContext remote)
            remote.SetInteractiveRemotePanel(new RemotePluginPanelContent("timer", "タイマー", RemoteHtml),
                ApplyRemoteAction, GetRemoteState);
        context.Log("タイマーを読み込みました。");
    }

    private string GetRemoteState() => JsonSerializer.Serialize(window?.GetRemoteState() ??
        new TimerRemoteState("05:00", "待機中", false, "5"));

    private bool ApplyRemoteAction(string action, string? value)
    {
        if (action is not ("start" or "stop" or "reset" or "setMinutes")) return false;
        if (window is not { IsLoaded: true }) Open();
        return window!.ApplyRemoteAction(action, value);
    }

    private const string RemoteHtml = """
        <!doctype html><html lang="ja"><head><meta name="viewport" content="width=device-width,initial-scale=1">
        <style>body{margin:0;padding:20px;background:#17191d;color:#fff;font-family:system-ui,sans-serif}h1{font-size:20px}
        #time{font:700 64px Consolas,monospace;text-align:center;margin:24px 0}#status{text-align:center;color:#b7becb}
        label{display:flex;align-items:center;gap:8px;margin:22px 0}input{width:90px;padding:10px;font-size:18px;background:#30343b;color:white;border:1px solid #777;border-radius:8px}
        .buttons{display:flex;gap:10px}button{flex:1;padding:15px;font-size:17px;color:white;background:#355d9a;border:0;border-radius:9px}button:last-child{background:#555}#error{color:#ff9999}</style></head>
        <body><h1>タイマー</h1><div id="time">05:00</div><div id="status">待機中</div>
        <label>時間 <input id="minutes" type="number" min="0.01" max="1440" step="any" value="5"> 分</label>
        <div class="buttons"><button id="toggle">開始</button><button id="reset">リセット</button></div><p id="error" role="alert"></p>
        <script>
        const send=(action,value)=>parent.postMessage({type:'jtsa-plugin-action',action,value},'*');
        const minutes=document.getElementById('minutes');
        minutes.onchange=()=>send('setMinutes',minutes.value);
        document.getElementById('toggle').onclick=()=>send(document.getElementById('toggle').textContent==='停止'?'stop':'start',minutes.value);
        document.getElementById('reset').onclick=()=>send('reset',minutes.value);
        addEventListener('message',event=>{
          if(event.data?.type==='jtsa-plugin-state'){
            const state=JSON.parse(event.data.state||'{}');
            document.getElementById('time').textContent=state.Time||'05:00';
            document.getElementById('status').textContent=state.Status||'待機中';
            document.getElementById('toggle').textContent=state.Running?'停止':'開始';
            if(document.activeElement!==minutes) minutes.value=state.Minutes||'5';
            document.getElementById('error').textContent='';
          } else if(event.data?.type==='jtsa-plugin-error') document.getElementById('error').textContent=event.data.message;
        });
        </script></body></html>
        """;

    public void Open()
    {
        if (window is { IsLoaded: true })
        {
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
            return;
        }

        window = new TimerWindow(context!);
        window.Closed += (_, _) => window = null;
        window.Show();
    }

    public void Shutdown()
    {
        window?.Close();
        context?.RemoveExpansionOverlay("timer");
        (context as IJtsaRemotePanelPluginContext)?.RemoveRemotePanel("timer");
        window = null;
    }
}
