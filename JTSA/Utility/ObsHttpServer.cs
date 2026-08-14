using System.Net;
using System.Text;

public class ObsHttpServer
{
    private readonly HttpListener listener = new();

    private readonly Func<string> htmlProvider;
    private readonly Func<string> jsonProvider;
    private readonly Func<string> chatHtmlProvider;
    private readonly Func<string> chatJsonProvider;

    public ObsHttpServer(
        Func<string> htmlProvider,
        Func<string> jsonProvider,
        Func<string> chatHtmlProvider,
        Func<string> chatJsonProvider)
    {
        this.htmlProvider = htmlProvider;
        this.jsonProvider = jsonProvider;
        this.chatHtmlProvider = chatHtmlProvider;
        this.chatJsonProvider = chatJsonProvider;

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

        string text;
        string contentType;

        switch (path)
        {
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

    private static void WriteExpansionImage(HttpListenerContext ctx)
    {
        var image = JTSA.Utility.StreamExpansionOverlayService.GetImage();
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
