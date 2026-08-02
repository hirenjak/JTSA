using System.Net;
using System.Text;

public class ObsHttpServer
{
    private readonly HttpListener listener = new();

    private readonly Func<string> htmlProvider;
    private readonly Func<string> jsonProvider;

    public ObsHttpServer(
        Func<string> htmlProvider,
        Func<string> jsonProvider)
    {
        this.htmlProvider = htmlProvider;
        this.jsonProvider = jsonProvider;

        listener.Prefixes.Add("http://localhost:8080/");
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

        string text;
        string contentType;

        switch (path)
        {
            case "/obs":
                text = htmlProvider();
                contentType = "text/html";
                break;

            case "/data":
                text = jsonProvider();
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
}