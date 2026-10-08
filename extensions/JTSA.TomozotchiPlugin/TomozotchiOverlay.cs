using System.Net;
using System.Text;

namespace JTSA.TomozotchiPlugin;

/// <summary>配信拡張へ描画する HTML。innerHTML で差し込まれるためスクリプトは使わない。</summary>
public static class TomozotchiOverlay
{
    // 見た目は tamagotchi-twitch/tamagotchi.html の CSS を移植
    private const string Style = """
        <style>
        .tz-root{font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif}
        .tz-stats{display:flex;flex-wrap:wrap;gap:15px;justify-content:flex-start}
        .tz-stat{background:#fff;padding:5px 20px;display:flex;flex-direction:column;align-items:center;text-align:center;border:6px solid #222;min-width:120px;flex:1 1 auto;max-width:200px}
        .tz-name{font-size:20px;font-weight:bold;color:#333;margin-bottom:3px;white-space:nowrap}
        .tz-hearts{font-size:20px;letter-spacing:2px;line-height:1.2}
        .tz-full{color:#ff6b9d;text-shadow:0 0 3px rgba(255,107,157,.3)}
        .tz-empty{color:#d0d0d0}
        .tz-empty-emoji{filter:grayscale(100%) brightness(.7);opacity:.5}
        .tz-notice{display:inline-block;margin-top:15px;color:#fff;padding:10px 15px;border-radius:8px;font-size:24px;font-weight:bold;animation:tz-notice 3.3s ease-out forwards}
        .tz-recover{background:#28a745}.tz-config{background:#007bff}.tz-reset{background:#ffc107;color:#212529}
        .tz-set{background:#17a2b8}.tz-modify{background:#6610f2}
        @keyframes tz-notice{0%{transform:translateX(-100%);opacity:0}9%,91%{transform:translateX(0);opacity:1}100%{transform:translateX(100%);opacity:0}}
        </style>
        """;

    public static string Render(TomozotchiGame game)
    {
        var html = new StringBuilder(Style);
        html.Append("<div class=\"tz-root\"><div class=\"tz-stats\">");
        foreach (var stat in game.Config.Stats)
        {
            html.Append("<div class=\"tz-stat\"><div class=\"tz-name\">")
                .Append(WebUtility.HtmlEncode(stat.Name))
                .Append("</div><div class=\"tz-hearts\">")
                .Append(Hearts(game.ValueOf(stat.Name), stat.MaxHearts, stat.MarkFull))
                .Append("</div></div>");
        }
        html.Append("</div>");

        if (game.Notice is { } notice)
        {
            var start = new DateTimeOffset(DateTime.SpecifyKind(notice.StartedAtUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            html.Append($"<div><div class=\"tz-notice tz-{notice.Kind}\" data-jtsa-animation-start=\"{start}\">")
                .Append(WebUtility.HtmlEncode(notice.Message))
                .Append("</div></div>");
        }
        return html.Append("</div>").ToString();
    }

    /// <summary>満タン分はマーク、空き分は ♥→♡、絵文字はグレー化、その他の1文字は「・」。</summary>
    public static string Hearts(int current, int max, string markFull)
    {
        var mark = string.IsNullOrEmpty(markFull) ? "♥" : markFull;
        var encoded = WebUtility.HtmlEncode(mark);
        var empty = mark.Length > 1
            ? $"<span class=\"tz-empty tz-empty-emoji\">{encoded}</span>"
            : $"<span class=\"tz-empty\">{(mark == "♥" ? "♡" : "・")}</span>";
        var html = new StringBuilder();
        for (var i = 0; i < max; i++)
            html.Append(i < current ? $"<span class=\"tz-full\">{encoded}</span>" : empty);
        return html.ToString();
    }
}
