namespace JTSA.Utility;

internal static class TodoOverlay
{
    internal static string CreateHtml() => """
        <!doctype html>
        <html lang="ja">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>JTSA ToDo一覧</title>
        <style>
        * { box-sizing:border-box; }
        html { font-size:calc(100vw / 300); overflow:hidden; }
        html,body { margin:0; background:transparent; color:white; font-family:"Yu Gothic UI","Meiryo",sans-serif; }
        body { visibility:hidden; padding:3rem 8rem; font-size:22rem; }
        section { padding:8rem 12rem; background:rgba(25,29,33,.62); border-radius:8rem; }
        h1 { margin:0; font-size:22rem; }
        .category { margin:1rem 0 6rem; color:#b5bec5; font-size:16rem; }
        .row { display:flex; gap:8rem; align-items:flex-start; padding:6rem 0; border-top:1rem solid white; border-bottom:1rem solid white; }
        .row.current { margin:2rem -6rem; padding:6rem; background:rgba(32,142,130,.72); border-radius:6rem; }
        .mark { flex:none; width:18rem; color:white; font-weight:700; }
        .text { min-width:0; font-size:22rem; font-weight:600; overflow-wrap:anywhere; }
        .completed .mark,.completed .text { color:#899298; }
        .completed .text { text-decoration:line-through; }
        .empty { margin:3rem 0; color:#b5bec5; font-size:18rem; }
        </style>
        </head>
        <body>
        <section>
          <h1>ToDo</h1><div id="category" class="category"></div><div id="items"></div>
        </section>
        <script>
        let previous = '';
        function render(data) {
          document.getElementById('category').textContent = data.category || 'カテゴリ未設定';
          const fragment = document.createDocumentFragment();
          for (const item of data.items || []) {
            const row = document.createElement('div');
            row.className = 'row' + (item.completed ? ' completed' : '') + (item.current ? ' current' : '');
            const mark = document.createElement('span'); mark.className = 'mark'; mark.textContent = item.current ? '⇒' : '';
            const text = document.createElement('span'); text.className = 'text'; text.textContent = item.text;
            row.append(mark, text); fragment.append(row);
          }
          if (!data.items || !data.items.length) {
            const empty = document.createElement('p'); empty.className = 'empty'; empty.textContent = 'ToDoはありません'; fragment.append(empty);
          }
          document.getElementById('items').replaceChildren(fragment);
        }
        async function refresh() {
          try {
            const response = await fetch('/todos-data', {cache:'no-store'});
            if (!response.ok) throw new Error(response.status);
            const data = await response.json();
            document.body.style.visibility = data.visible === true ? 'visible' : 'hidden';
            const signature = JSON.stringify(data);
            if (signature !== previous) { render(data); previous = signature; }
          } catch { }
          finally { setTimeout(refresh, 1000); }
        }
        refresh();
        </script>
        </body></html>
        """;
}
