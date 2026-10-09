using System.Net;
using System.IO;
using System.Text;

public class ObsHttpServer
{
    private readonly HttpListener listener = new();

    private readonly Func<string> htmlProvider;
    private readonly Func<string> jsonProvider;
    private readonly Func<string> chatHtmlProvider;
    private readonly Func<string> chatJsonProvider;
    private readonly Func<string> participationJsonProvider;
    private readonly Func<string> todoJsonProvider;

    public ObsHttpServer(
        Func<string> htmlProvider,
        Func<string> jsonProvider,
        Func<string> chatHtmlProvider,
        Func<string> chatJsonProvider,
        Func<string>? participationJsonProvider = null,
        Func<string>? todoJsonProvider = null)
    {
        this.htmlProvider = htmlProvider;
        this.jsonProvider = jsonProvider;
        this.chatHtmlProvider = chatHtmlProvider;
        this.chatJsonProvider = chatJsonProvider;
        this.participationJsonProvider = participationJsonProvider ?? (() => "{\"playing\":[],\"waiting\":[]}");
        this.todoJsonProvider = todoJsonProvider ?? (() => "{\"visible\":false,\"items\":[]}");

        listener.Prefixes.Add("http://localhost:8026/");
    }

    public async Task StartAsync()
    {
        listener.Start();

        while (listener.IsListening)
        {
            var ctx = await listener.GetContextAsync();

            _ = Task.Run(() => Process(ctx));
        }
    }

    private void Process(HttpListenerContext ctx)
    {
        string path = ctx.Request.Url!.AbsolutePath;

        if (path == "/expansion-image")
        {
            WriteExpansionImage(ctx);
            return;
        }

        if (path == "/expansion-media")
        {
            WriteExpansionMedia(ctx);
            return;
        }

        if (path == "/expansion-clips-ready")
        {
            StartExpansionClip(ctx);
            return;
        }

        string text;
        string contentType;

        switch (path)
        {
            case "/participants":
                text = JTSA.Utility.ParticipationOverlay.CreateHtml();
                contentType = "text/html";
                break;
            case "/participants-data":
                text = participationJsonProvider();
                contentType = "application/json";
                ctx.Response.AddHeader("Cache-Control", "no-store");
                break;
            case "/todos":
                text = JTSA.Utility.TodoOverlay.CreateHtml();
                contentType = "text/html";
                break;
            case "/todos-data":
                text = todoJsonProvider();
                contentType = "application/json";
                ctx.Response.AddHeader("Cache-Control", "no-store");
                break;
            case "/":
            case "/chat":
                text = chatHtmlProvider();
                contentType = "text/html";
                break;

            case "/chat-data":
                text = chatJsonProvider();
                contentType = "application/json";
                break;

            case "/obs":
                text = htmlProvider();
                contentType = "text/html";
                break;

            case "/data":
                text = jsonProvider();
                contentType = "application/json";
                break;

            case "/expansion-clips":
                text = JTSA.Utility.StreamExpansionClipOverlay.CreateHtml();
                contentType = "text/html";
                ctx.Response.AddHeader("Cache-Control", "no-store, no-cache, must-revalidate");
                ctx.Response.AddHeader("Pragma", "no-cache");
                break;

            case "/expansion-clips-data":
                text = JTSA.Utility.StreamExpansionClipOverlay.CreateJson();
                contentType = "application/json";
                ctx.Response.AddHeader("Cache-Control", "no-store");
                break;

            case "/expansion":
                text = JTSA.Utility.StreamExpansionOverlayService.CreateHtml();
                contentType = "text/html";
                break;

            case "/expansion-data":
                text = JTSA.Utility.StreamExpansionOverlayService.CreateJson();
                contentType = "application/json";
                break;

            default:
                ctx.Response.StatusCode = 404;
                ctx.Response.Close();
                return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(text);

        ctx.Response.ContentType = contentType + "; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;

        ctx.Response.OutputStream.Write(bytes);
        ctx.Response.Close();
    }

    private static void StartExpansionClip(HttpListenerContext ctx)
    {
        try
        {
            var id = ctx.Request.QueryString["id"] ?? string.Empty;
            if (!JTSA.Utility.StreamExpansionClipOverlay.TryStart(id))
            {
                ctx.Response.StatusCode = 404;
            }
            else
            {
                JTSA.Utility.StreamExpansionClipAudioPlayer.StartPrepared();
                ctx.Response.StatusCode = 204;
            }
        }
        catch
        {
            ctx.Response.StatusCode = 500;
        }
        finally
        {
            ctx.Response.Close();
        }
    }

    // ponytail: Range 非対応（頭からの再生のみ）。動画のシークが必要になったら 206 応答を追加する。
    private static void WriteExpansionMedia(HttpListenerContext ctx)
    {
        try
        {
            var path = JTSA.Utility.StreamExpansionOverlayService.GetMediaPath(ctx.Request.QueryString["id"] ?? "");
            if (path is null)
            {
                ctx.Response.StatusCode = 404;
                return;
            }

            using var file = File.OpenRead(path);
            ctx.Response.ContentType = JTSA.Utility.StreamExpansionOverlayService.ContentTypeOf(path);
            ctx.Response.ContentLength64 = file.Length;
            ctx.Response.AddHeader("Cache-Control", "max-age=3600");
            file.CopyTo(ctx.Response.OutputStream);
        }
        catch
        {
            // ブラウザ側の切断などで送信が止まっても配信拡張全体は止めない
        }
        finally
        {
            ctx.Response.Close();
        }
    }

    private static void WriteExpansionImage(HttpListenerContext ctx)
    {
        if (!long.TryParse(ctx.Request.QueryString["id"], out var id))
        {
            ctx.Response.StatusCode = 400;
            ctx.Response.Close();
            return;
        }

        var image = JTSA.Utility.StreamExpansionOverlayService.GetImage(id);
        if (image is null)
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.Close();
            return;
        }

        ctx.Response.ContentType = image.Value.ContentType;
        ctx.Response.ContentLength64 = image.Value.Data.Length;
        ctx.Response.AddHeader("Cache-Control", "no-store, no-cache, must-revalidate");
        ctx.Response.OutputStream.Write(image.Value.Data);
        ctx.Response.Close();
    }
}
