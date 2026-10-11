import { app, BrowserWindow, ipcMain, Menu, nativeTheme, screen, session } from 'electron';
import type { MenuItemConstructorOptions } from 'electron';
import * as fs from 'fs';
import * as path from 'path';

const APP_TITLE = '表情选择器';
const SELFTEST = process.argv.includes('--selftest');
const SHOT = process.argv.find((a) => a.startsWith('--shot='))?.slice('--shot='.length);
// `--type` is a reserved Chromium switch (renderer / gpu-process / ...), so the
// probe flag must be named otherwise or the process never comes up.
const QUERY = process.argv.find((a) => a.startsWith('--query='))?.slice('--query='.length);
const PICK = process.argv.find((a) => a.startsWith('--pick='))?.slice('--pick='.length);
const LAYOUT = process.argv.includes('--layout');
const SEND = process.argv.find((a) => a.startsWith('--send='))?.slice('--send='.length);
// Negative control for the offline gate: asks the renderer for the one CDN URL
// that is still present as a dead string in the bundle, so a guard that never
// fires cannot be mistaken for a clean run.
const NETPROBE = process.argv.includes('--netprobe');
// The popover-mounted probes need the picker open; --layout and --send= read
// the closed state, so they must not force it open.
const OPENS_POPOVER = SELFTEST || !!SHOT || !!QUERY || !!PICK;
const PROBING = OPENS_POPOVER || LAYOUT || !!SEND;

// 单明暗工程：显式钉住 light，不留 'system'，否则系统切深色时控件与滚动条会跟内容打架。
nativeTheme.themeSource = 'light';

// Electron 44 的默认应用菜单不跟随 l10n：实测 app.getLocale() 已是 zh-CN，
// 默认菜单仍是 File/Edit/View/Window，连 role 生成的子项都是 "Exit"。
// 所以这里逐项显式给 label，role 只用来保行为与快捷键。
const menuTemplate: MenuItemConstructorOptions[] = [
  {
    label: '文件',
    submenu: [
      { label: '退出', accelerator: 'CmdOrCtrl+Q', role: 'quit' }
    ]
  },
  {
    label: '编辑',
    submenu: [
      { label: '撤销', role: 'undo' },
      { label: '重做', role: 'redo' },
      { type: 'separator' },
      { label: '剪切', role: 'cut' },
      { label: '拷贝', role: 'copy' },
      { label: '粘贴', role: 'paste' },
      { label: '全选', role: 'selectAll' }
    ]
  },
  {
    label: '视图',
    submenu: [
      { label: '重新加载', role: 'reload' },
      { label: '强制重新加载', role: 'forceReload' },
      { label: '开发者工具', role: 'toggleDevTools' },
      { type: 'separator' },
      { label: '实际大小', role: 'resetZoom' },
      { label: '放大', role: 'zoomIn' },
      { label: '缩小', role: 'zoomOut' }
    ]
  },
  {
    label: '窗口',
    submenu: [
      { label: '最小化', role: 'minimize' },
      { label: '关闭', role: 'close' }
    ]
  }
];

Menu.setApplicationMenu(Menu.buildFromTemplate(menuTemplate));

function createWindow(): BrowserWindow {
  const win = new BrowserWindow({
    title: APP_TITLE,
    width: 400,
    height: 560,
    minWidth: 320,
    minHeight: 420,
    backgroundColor: '#f7f8f8',
    show: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true
    }
  });

  // 没有这一行，index.html 的 <title> 与渲染层的 document.title 会盖掉主进程设定。
  win.on('page-title-updated', (e) => e.preventDefault());

  if (process.env.VITE_DEV_SERVER_URL) {
    win.loadURL(process.env.VITE_DEV_SERVER_URL);
  } else {
    // The probe modes need the popover mounted, and file:// has no other way
    // to ask for it. --layout and --send= are the exception: they measure the
    // closed state, which is exactly what auto-open would hide.
    win.loadFile(path.join(__dirname, '..', 'dist', 'index.html'), {
      search: OPENS_POPOVER ? 'open=1' : undefined
    });
  }
  return win;
}

ipcMain.handle('app:ping', () => {
  console.log('ipc-roundtrip=ok');
  return 'pong';
});

// 离线判据：拦截并计数所有非 file:// 的请求。
// 只报 remote=0 是假绿——钩子没挂上时同样是 0，所以同一条钩子必须再数一遍
// file://，本地计数 > 0 才证明它真的在收事件。
const remoteRequests: string[] = [];
const localFiles = new Map<string, number>();
function installNetworkGuard(): void {
  session.defaultSession.webRequest.onBeforeRequest({ urls: ['<all_urls>'] }, (details, callback) => {
    const scheme = details.url.slice(0, details.url.indexOf(':')).toLowerCase();
    if (scheme === 'file' || scheme === 'data' || scheme === 'blob') {
      if (scheme === 'file') {
        const name = details.url.split('/').pop()!.split('?')[0];
        localFiles.set(name, (localFiles.get(name) || 0) + 1);
      }
      callback({});
      return;
    }
    remoteRequests.push(details.url);
    callback({ cancel: true });
  });
}

function reportNetwork(): void {
  // Sorted so the two runs of the same build compare line for line.
  const listed = [...localFiles.entries()].sort().map(([n, c]) => n + (c > 1 ? 'x' + c : ''));
  console.log(
    `network remote=${remoteRequests.length} local-files=${localFiles.size} [${listed.join(', ')}]` +
      (remoteRequests.length ? ' ' + JSON.stringify(remoteRequests.slice(0, 5)) : '')
  );
}

function quitAfterReport(): void {
  reportNetwork();
  app.quit();
}

ipcMain.handle('app:env', () => ({
  themeSource: nativeTheme.themeSource,
  shouldUseDarkColors: nativeTheme.shouldUseDarkColors
}));

// Measures the closed-state layout: which regions exist, what size they got,
// and whether the recent-strip's sprite spans actually paint.
const LAYOUT_JS = `(() => {
  const r = (sel) => {
    const e = document.querySelector(sel);
    if (!e) return 'MISSING';
    const b = e.getBoundingClientRect();
    const cs = getComputedStyle(e);
    return Math.round(b.width) + 'x' + Math.round(b.height)
      + ' display=' + cs.display + ' vis=' + cs.visibility;
  };
  const span = document.querySelector('.recent__item > span');
  let spanInfo = 'no-span';
  if (span) {
    const b = span.getBoundingClientRect();
    const cs = getComputedStyle(span);
    spanInfo = Math.round(b.width) + 'x' + Math.round(b.height)
      + ' display=' + cs.display + ' bg=' + (cs.backgroundImage === 'none' ? 'none' : 'set');
  }
  const de = document.documentElement;
  const ta = document.querySelector('textarea');
  return JSON.stringify({
    inner: window.innerWidth + 'x' + window.innerHeight,
    doc: de.scrollWidth + 'x' + de.scrollHeight + ' client=' + de.clientWidth + 'x' + de.clientHeight,
    chat: r('.chat'),
    log: r('.chat__log'),
    recent: r('.recent'),
    recentItems: document.querySelectorAll('.recent__item').length,
    recentSpan: spanInfo,
    composer: r('.composer'),
    input: r('.composer__input'),
    inputBox: ta ? ta.scrollHeight + '/' + ta.clientHeight + ' value=' + ta.value.length : 'none',
    popover: !!document.querySelector('.popover')
  });
})()`;

// Runs inside the renderer: proves the sprite path really painted (a decoded
// atlas bitmap + a measured glyph box), not just that React mounted.
const PROBE_JS = `(async () => {
  await new Promise(r => setTimeout(r, 250));
  const cells = document.querySelectorAll('[data-epr-unified]').length;
  // cells is a DOM count and the grid is virtualized, so it under-reads the
  // total on any query with more results than fit on screen. The library's
  // turned out to be a per-category count (reads 0 during search), so the
  // result total is taken from the live region the library announces through.
  const announced =
    [...document.querySelectorAll('[aria-live],[role="status"]')]
      .map(e => e.textContent.trim())
      .filter(Boolean)
      .join(' | ') || 'n/a';
  const catIcons = document.querySelectorAll('.ep-cat-icon').length;
  const g = document.querySelector('[data-epr-unified] .ep-glyph');
  const bg = g ? getComputedStyle(g).backgroundImage : '';
  const url = bg.startsWith('url(') ? bg.slice(5, -1).replace(/^"|"$/g, '') : '';
  const decoded = url
    ? await new Promise(r => { const i = new Image(); i.onload = () => r(i.naturalWidth + 'x' + i.naturalHeight); i.onerror = () => r('decode-error'); i.src = url; })
    : 'no-background-image';
  const box = g ? Math.round(g.getBoundingClientRect().width) + 'x' + Math.round(g.getBoundingClientRect().height) : 'none';
  return JSON.stringify({
    inputValue: document.querySelector('input')?.value ?? '(no input)',
    cells,
    announced,
    catIcons,
    glyphBox: box,
    atlasFile: url.split('/').pop() || 'none',
    decoded,
    dark: matchMedia('(prefers-color-scheme: dark)').matches,
    surface: getComputedStyle(document.documentElement).getPropertyValue('--ep-surface').trim(),
    dpr: devicePixelRatio
  });
})()`;

app.whenReady().then(() => {
  // Dev mode serves the renderer over http://localhost, which the guard would
  // cancel; the offline claim is only about the packaged (file://) build.
  if (!process.env.VITE_DEV_SERVER_URL) installNetworkGuard();
  const win = createWindow();

  win.once('ready-to-show', () => {
    win.show();
    const display = screen.getDisplayMatching(win.getBounds());
    console.log(`window-shown title=${win.getTitle()}`);
    console.log(`scaleFactor=${display.scaleFactor} zoomFactor=${win.webContents.getZoomFactor()}`);
    console.log(`themeSource=${nativeTheme.themeSource} shouldUseDarkColors=${nativeTheme.shouldUseDarkColors}`);

    if (PROBING) {
      const m = Menu.getApplicationMenu();
      const labels: string[] = [];
      const walk = (items: Electron.MenuItem[] | null) => {
        for (const it of items || []) {
          if (it.label) labels.push(it.label);
          if (it.submenu) walk(it.submenu.items);
        }
      };
      walk(m ? m.items : null);
      const latin = labels.filter((l) => /^[A-Za-z .]+$/.test(l));
      console.log(`menu locale=${app.getLocale()} top=${JSON.stringify((m ? m.items : []).map((i) => i.label))}`);
      console.log(`menu labels=${labels.length} latin-leftover=${latin.length} ${JSON.stringify(latin)}`);
    }

    if (!PROBING) return;

    if (SEND !== undefined) {
      const runSend = async () => {
        const focused = await win.webContents.executeJavaScript(
          `(() => { const t = document.querySelector('textarea'); if (t) t.focus(); return !!t; })()`
        );
        // Real keystrokes into the focused element: setting .value from JS
        // would not move a React controlled input.
        win.webContents.insertText(SEND);
        await new Promise((r) => setTimeout(r, 200));
        const clicked = await win.webContents.executeJavaScript(
          `(() => { const b = document.querySelector('.composer__send');
             if (!b) return 'no-send-button';
             const was = b.disabled ? 'was-disabled' : 'enabled';
             b.click(); return was; })()`
        );
        await new Promise((r) => setTimeout(r, 250));
        const after = await win.webContents.executeJavaScript(`(() => {
          const bs = [...document.querySelectorAll('.bubble')];
          const log = document.querySelector('.chat__log');
          const ta = document.querySelector('textarea');
          return JSON.stringify({
            bubbles: bs.length,
            last: bs.length ? bs[bs.length - 1].textContent : '',
            inputLeft: ta ? ta.value : 'no-textarea',
            gapToBottom: log ? Math.round(log.scrollHeight - log.clientHeight - log.scrollTop) : -1
          });
        })()`);
        console.log(`send focused=${focused} click=${clicked} after=${after}`);
      };
      runSend()
        .catch((err) => console.log('send=FAIL ' + String(err)))
        .finally(quitAfterReport);
      return;
    }

    if (LAYOUT) {
      const finish = async () => {
        if (SHOT) {
          const image = await win.webContents.capturePage();
          fs.writeFileSync(SHOT, image.toPNG());
          console.log(`screenshot=${SHOT} bytes=${image.toPNG().length}`);
        }
      };
      const runLayout = async () => {
        // ready-to-show fires before the window settles; measuring immediately
        // reported innerHeight 26px larger than a later measurement, which
        // looked exactly like a scrollbar appearing. Give it the same grace the
        // popover probe uses before reading anything.
        await new Promise((r) => setTimeout(r, 250));
        const first = await win.webContents.executeJavaScript(LAYOUT_JS);
        console.log('layout(closed) ' + String(first));
        const clicked = await win.webContents.executeJavaScript(
          `(() => { const b = document.querySelector('.composer__trigger');` +
          ` if (!b) return 'no-trigger'; b.click(); return 'clicked'; })()`
        );
        await new Promise((r) => setTimeout(r, 300));
        const after = await win.webContents.executeJavaScript(LAYOUT_JS);
        console.log(`trigger=${clicked} layout(open) ${String(after)}`);
        await finish();
      };
      runLayout()
        .catch((err) => console.log('layout=FAIL ' + String(err)))
        .finally(quitAfterReport);
      return;
    }

    const run = async () => {
      if (QUERY) {
        await win.webContents.executeJavaScript(
          `(() => { const i = document.querySelector('input'); if (i) i.focus(); return !!i; })()`
        );
        win.webContents.insertText(QUERY);
        await new Promise((r) => setTimeout(r, 500)); // the list is debounced
      }
      const raw = await win.webContents.executeJavaScript(PROBE_JS);
      const r = JSON.parse(String(raw));
      console.log(
        `renderer-probe typed=${r.inputValue} cells=${r.cells} announced="${r.announced}" catIcons=${r.catIcons} glyphBox=${r.glyphBox} atlas=${r.atlasFile} decoded=${r.decoded}`
      );
      console.log(`theme-consistent=${String(r.dark === nativeTheme.shouldUseDarkColors)} surface=${r.surface} dpr=${r.dpr}`);

      if (PICK !== undefined) {
        const idx = Number(PICK);
        const picked = await win.webContents.executeJavaScript(
          '(() => { const c = document.querySelectorAll(\'[data-epr-unified]\')[' + idx + '];' +
          ' if (!c) return \'no-cell-at-index\'; c.click(); return c.getAttribute(\'data-epr-unified\'); })()'
        );
        await new Promise((rr) => setTimeout(rr, 250));
        const after = await win.webContents.executeJavaScript(
          'JSON.stringify({ textarea: document.querySelector(\'textarea\') ? document.querySelector(\'textarea\').value : \'none\',' +
          ' strip: document.querySelectorAll(\'.recent__item\').length,' +
          ' stored: localStorage.getItem(\'ep.recent\') || \'none\' })'
        );
        console.log(`pick[${idx}]=${picked} after=${after}`);
      }

      if (NETPROBE) {
        const probed = await win.webContents.executeJavaScript(
          `fetch('https://cdn.jsdelivr.net/npm/emoji-datasource-apple@16.0.0/img/apple/64/1f600.png', {cache:'no-store'})` +
          ` .then(r => 'RESOLVED status=' + r.status)` +
          ` .catch(e => 'REJECTED ' + String(e && e.message).slice(0, 60))`
        );
        console.log(`netprobe control-fetch=${String(probed)}`);
      }

      if (SHOT) {
        const image = await win.webContents.capturePage();
        fs.writeFileSync(SHOT, image.toPNG());
        console.log(`screenshot=${SHOT} bytes=${image.toPNG().length}`);
      }
    };

    run().catch((err) => console.log('renderer-probe=FAIL ' + String(err))).finally(quitAfterReport);
  });
});

app.on('window-all-closed', () => app.quit());
