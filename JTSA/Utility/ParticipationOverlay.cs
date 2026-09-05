namespace JTSA.Utility;

internal static class ParticipationOverlay
{
    internal static string CreateHtml() => """
        <!doctype html>
        <html lang="ja">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>JTSA 参加一覧</title>
        <style>
        * { box-sizing: border-box; }
        /* One rem equals one design pixel at a 300px viewport; redraw at the source resolution. */
        html { font-size:calc(100vw / 300); overflow:hidden; }
        html,body { margin:0; background:transparent; color:white; font-family:"Yu Gothic UI","Meiryo",sans-serif; }
        body { visibility:hidden; padding:3rem 8rem; font-size:23rem; }
        section { margin-bottom:6rem; padding:6rem 12rem; background:rgba(25,29,33,.55); border-radius:8rem; }
        section:last-child { margin-bottom:0; }
        h2 { font-size:20rem; margin:0 0 3rem; color:#fff; }
        .row { display:flex; align-items:center; gap:10rem; padding:3rem 0; }
        .avatar { width:44rem; height:44rem; flex:none; border-radius:50%; background:#485055; object-fit:cover; }
        .details { min-width:0; }
        .name { font-size:27rem; font-weight:600; overflow-wrap:anywhere; }
        .count { color:#e5fff8; font-size:20rem; font-weight:700; }
        .empty { margin:2rem 0; color:#b5bec5; font-size:20rem; }
        </style>
        </head>
        <body>
        <section><h2 id="playing-title">プレイ中</h2><div id="playing"></div></section>
        <section><h2 id="waiting-title">参加待ち</h2><div id="waiting"></div></section>
        <script>
        let previous = '';
        function render(id, users, heading, suffix, slotCount) {
            const capacity = id === 'playing' ? '/' + (slotCount > 0 ? slotCount : '未設定') : '';
            document.getElementById(id + '-title').textContent = heading + '（' + users.length + capacity + '）';
            const fragment = document.createDocumentFragment();
            for (const user of users) {
                const row = document.createElement('div'); row.className = 'row';
                const avatar = document.createElement('div'); avatar.className = 'avatar';
                try {
                    const url = new URL(user.icon);
                    if (url.protocol === 'https:') {
                        const img = document.createElement('img'); img.className = 'avatar'; img.alt = '';
                        img.src = url.href; img.onerror = () => img.remove(); avatar.append(img);
                    }
                } catch {}
                const details = document.createElement('div'); details.className = 'details';
                const name = document.createElement('div'); name.className = 'name'; name.textContent = user.name;
                const count = document.createElement('div'); count.className = 'count'; count.textContent = suffix(user.count);
                details.append(name, count); row.append(avatar, details); fragment.append(row);
            }
            if (!users.length) {
                const empty = document.createElement('p'); empty.className = 'empty'; empty.textContent = '現在いません'; fragment.append(empty);
            }
            document.getElementById(id).replaceChildren(fragment);
        }
        async function refresh() {
            try {
                const response = await fetch('/participants-data', {cache:'no-store'});
                if (!response.ok) throw new Error(response.status);
                const data = await response.json();
                document.body.style.visibility = data.visible === true ? 'visible' : 'hidden';
                const signature = JSON.stringify(data);
                if (signature !== previous) {
                    render('playing', data.playing, 'プレイ中', n => n + '試合参加済み', data.slotCount);
                    render('waiting', data.waiting, '参加待ち', n => '参加 ' + n + '回');
                    previous = signature;
                }
            } catch { /* Keep the last successful display during a temporary disconnection. */ }
            finally { setTimeout(refresh, 1000); }
        }
        refresh();
        </script>
        </body></html>
        """;
}
