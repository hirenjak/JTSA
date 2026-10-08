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
        .tz-redeemable{display:flex;align-items:center;gap:10px;flex-wrap:wrap;margin-top:10px}
        .tz-redeemable-label{margin-left:5px;font-size:1.3em;font-weight:bold;color:#000;white-space:nowrap;text-shadow:2px 2px 0 #fff,-2px -2px 0 #fff,2px -2px 0 #fff,-2px 2px 0 #fff,0 2px 0 #fff,0 -2px 0 #fff,2px 0 0 #fff,-2px 0 0 #fff}
        .tz-card{width:50px;height:50px;border-radius:12px;background:#fff;box-shadow:3px 4px 0 rgba(0,0,0,.35);border:2px solid #d0d0d0;display:flex;align-items:center;justify-content:center;font-size:2.6em;animation:tz-in .5s ease-out both}
        .tz-card img{width:40px;height:40px;object-fit:contain;border-radius:4px;display:block}
        .tz-cp{margin-top:20px;display:flex;flex-direction:column;gap:4px}
        .tz-cooldown{font-size:22px;font-weight:bold;color:#70a9ff;text-shadow:1px 1px 2px rgba(0,0,0,.8);animation:tz-in .5s ease-out both}
        .tz-todo{font-size:24px;color:#ffbd6d;text-shadow:1px 1px 2px rgba(0,0,0,.8);animation:tz-in .5s ease-out both}
        .tz-todo::before{content:'☑ '}
        @keyframes tz-in{from{transform:translateX(-30px);opacity:0}to{transform:none;opacity:1}}
        </style>
        """;

    public static string Render(TomozotchiGame game) => Render(game, null, DateTimeOffset.UtcNow);

    public static string Render(TomozotchiGame game, ChannelPointBoard? board, DateTimeOffset now)
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
        if (board is not null) AppendChannelPoints(html, game.Config.ChannelPoints!, board, now);

        if (game.Notice is { } notice)
        {
            var start = new DateTimeOffset(DateTime.SpecifyKind(notice.StartedAtUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            html.Append($"<div><div class=\"tz-notice tz-{notice.Kind}\" data-jtsa-animation-start=\"{start}\">")
                .Append(WebUtility.HtmlEncode(notice.Message))
                .Append("</div></div>");
        }
        return html.Append("</div>").ToString();
    }

    // 元アプリと同じく、交換可能アイコン → クールダウン → TODO の順に出す
    private static void AppendChannelPoints(StringBuilder html, ChannelPointHooks hooks, ChannelPointBoard board, DateTimeOffset now)
    {
        var shown = new HashSet<string>();
        string Start(string key)
        {
            shown.Add(key);
            return board.FirstSeen(key, now).ToUnixTimeMilliseconds().ToString();
        }

        var redeemables = board.Redeemables(hooks, now);
        if (redeemables.Count > 0)
        {
            html.Append("<div class=\"tz-redeemable\"><span class=\"tz-redeemable-label\">交換可能：</span>");
            foreach (var reward in redeemables)
            {
                html.Append($"<div class=\"tz-card\" data-jtsa-animation-start=\"{Start("r:" + reward.Id)}\">");
                html.Append(string.IsNullOrEmpty(reward.ImageUrl)
                    ? WebUtility.HtmlEncode(LeadingEmoji(reward.Title))
                    : $"<img src=\"{WebUtility.HtmlEncode(reward.ImageUrl)}\" alt=\"\">");
                html.Append("</div>");
            }
            html.Append("</div>");
        }

        var cooldowns = board.Cooldowns(hooks, now);
        var todos = board.Todos;
        if (cooldowns.Count + todos.Count > 0)
        {
            html.Append("<div class=\"tz-cp\">");
            foreach (var reward in cooldowns)
                html.Append($"<div class=\"tz-cooldown\" data-jtsa-animation-start=\"{Start("c:" + reward.Id)}\">⌛️クールダウン中 ")
                    .Append(WebUtility.HtmlEncode(reward.Title)).Append(' ')
                    .Append(ChannelPointBoard.FormatRemaining(reward.CooldownExpiresAt!.Value - now))
                    .Append("</div>");
            foreach (var todo in todos)
                html.Append($"<div class=\"tz-todo\" data-jtsa-animation-start=\"{Start("t:" + todo.Id)}\">")
                    .Append(WebUtility.HtmlEncode(todo.RewardTitle))
                    .Append("</div>");
            html.Append("</div>");
        }
        board.ForgetExcept(shown);
    }

    /// <summary>リワード名の先頭が絵文字ならそれを、なければ 🎁 を返す。</summary>
    public static string LeadingEmoji(string title)
    {
        if (string.IsNullOrEmpty(title)) return "🎁";
        var first = System.Globalization.StringInfo.GetNextTextElement(title);
        return Rune.TryGetRuneAt(first, 0, out var rune) &&
               (Rune.GetUnicodeCategory(rune) == System.Globalization.UnicodeCategory.OtherSymbol || rune.Value >= 0x1F000)
            ? first
            : "🎁";
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
