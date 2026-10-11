#!/usr/bin/env node
/* ============================================================================
   check-wiring.mjs —— 窗口壳接线审计(零依赖,纯静态)

   用法:
     node <skill>/scripts/check-wiring.mjs [--root <工程目录>] [--verbose]

   干什么:在目标工程里扫 src/ + electron/ + app/ + main/ + renderer/,把"自绘拖拽 +
   右键三项控制条 + 自绘最小化退场"这套东西能不能真跑起来的 13 条前置逐条判定。
   有 FAIL 就 exit 1 —— 这条脚本的设计目标是**会自己失败**:每条都对应一个已经实测过的
   静默失效(见 references/pitfalls.md),不要把它当 lint 忽略。

   不干什么:不证明动画帧率、不证明真实光标手感。那两条要真窗口跑探针,见
   references/verification.md。静态审计只能证明"接线在",不能证明"效果对"。
   ========================================================================== */

import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { join, posix } from 'node:path';

const argv = process.argv.slice(2);
const ROOT = (() => {
  const i = argv.indexOf('--root');
  return i >= 0 && argv[i + 1] ? argv[i + 1] : process.cwd();
})();
const VERBOSE = argv.includes('--verbose');

const SCAN_DIRS = ['src', 'electron', 'app', 'main', 'renderer'];
const CODE_EXT = ['.ts', '.tsx', '.js', '.jsx', '.mjs', '.mts'];
const CSS_EXT = ['.css'];
const SKIP_DIR = new Set(['node_modules', 'dist', 'dist-electron', 'out', 'release', 'build', '.git', 'coverage', '.cache']);

/* ---------- 收集文件 ---------- */
const files = [];
function walk(dir, rel) {
  let ents;
  try { ents = readdirSync(dir); } catch { return; }
  for (const name of ents) {
    const abs = join(dir, name);
    const r = rel ? `${rel}/${name}` : name;
    let st;
    try { st = statSync(abs); } catch { continue; }
    if (st.isDirectory()) {
      if (SKIP_DIR.has(name)) continue;
      walk(abs, r);
    } else if (CODE_EXT.some((e) => name.endsWith(e)) || CSS_EXT.some((e) => name.endsWith(e))) {
      const text = readText(abs);
      files.push({ abs, rel: posix.normalize(r), text, blank: blankCommentLines(text) });
    }
  }
}
function readText(abs) {
  try { return readFileSync(abs, 'utf8'); } catch { return ''; }
}

/* 把注释抹成等长空格(字符串与正则字面量原样保留,换行保留 ⇒ 下标与行号一一对应)。
   为什么要抹:这套东西的源码里到处是解释性注释("原先这两条是 -webkit-app-region: drag"、
   "读了 reduced-motion"),对原文跑正则就会把注释当成接线 —— 那正是假绿。
   长度不变是关键:命中下标可以直接拿去数行号。
   自证:扫完统计块注释的开/合标记是否配平,不配平就 WARN(说明这个文件的注释结构把我的
   状态机绕过去了,此时判定不可信)。 */
function stripComments(text) {
  let out = '';
  let i = 0;
  const n = text.length;
  let quote = null;   // 当前字符串/模板字面量的引号
  let lastCode = '';  // 上一个非空代码字符,用来判"这是正则字面量还是除号"
  while (i < n) {
    const c = text[i], d = text[i + 1];
    if (quote) {
      if (c === '\\') { out += c + (d ?? ''); i += 2; continue; }
      if (c === quote) quote = null;
      out += c; i++; continue;
    }
    if (c === '/' && d === '/') {
      const k = text.indexOf('\n', i); const end = k < 0 ? n : k;
      out += ' '.repeat(end - i); i = end; continue;
    }
    if (c === '/' && d === '*') {
      const k = text.indexOf('*/', i + 2); const end = k < 0 ? n : k + 2;
      out += text.slice(i, end).replace(/[^\n]/g, ' '); i = end; continue;
    }
    if (c === "'" || c === '"' || c === '`') { quote = c; out += c; i++; continue; }
    if (c === '/' && /[=(,:;[{!&|?%+\-*~^]$/.test(lastCode)) {
      // 正则字面量:上一个代码字符是"前缀位" ⇒ 这个 / 开一条正则,原样保留(里面有引号和 //)
      let j = i + 1, inClass = false;
      while (j < n) {
        const x = text[j];
        if (x === '\\') { j += 2; continue; }
        if (x === '[') inClass = true;
        else if (x === ']') inClass = false;
        else if (x === '/' && !inClass) break;
        else if (x === '\n') break;  // 换行还没闭合 ⇒ 不是正则,退回当除号
        j++;
      }
      if (j < n && text[j] === '/') {
        let k = j + 1;
        while (k < n && /[a-z]/i.test(text[k])) k++;
        out += text.slice(i, k); i = k; lastCode = '/'; continue;
      }
    }
    out += c;
    if (!/\s/.test(c)) lastCode = c;
    i++;
  }
  return out;
}
function blankCommentLines(text) {
  return stripComments(text);
}

for (const d of SCAN_DIRS) if (existsSync(join(ROOT, d))) walk(join(ROOT, d), d);
const rootPkg = existsSync(join(ROOT, 'package.json')) ? readText(join(ROOT, 'package.json')) : '';

const code = files.filter((f) => CODE_EXT.some((e) => f.rel.endsWith(e)));
const css = files.filter((f) => f.rel.endsWith('.css'));

/* ---------- 报告器 ---------- */
const rows = [];
let nFail = 0, nWarn = 0, nPass = 0;
function rec(level, id, msg, where) {
  rows.push({ level, id, msg, where });
  if (level === 'FAIL') nFail++; else if (level === 'WARN') nWarn++; else nPass++;
}
const PASS = (id, msg, where) => rec('PASS', id, msg, where);
const FAIL = (id, msg, where) => rec('FAIL', id, msg, where);
const WARN = (id, msg, where) => rec('WARN', id, msg, where);

/** 返回第一条命中的 {file, line}。line 是 1 起的行号,方便直接跳。 */
/** 正则在 blank(注释行已抹平)上跑,行号用原文算 —— 两者长度一一对应。 */
function hit(list, re) {
  for (const f of list) {
    const m = re.exec(f.blank);
    if (m) {
      const line = f.text.slice(0, m.index).split('\n').length;
      return { file: f.rel, line, text: f.text, blank: f.blank, match: m[0], g: m.slice(1), index: m.index };
    }
  }
  return null;
}
/** 抠出一个 handler/规则体的近似文本:从命中点起,按大括号配平走。 */
function blockFrom(text, index) {
  const open = text.indexOf('{', index);
  if (open < 0) return '';
  let depth = 0;
  for (let i = open; i < text.length; i++) {
    if (text[i] === '{') depth++;
    else if (text[i] === '}') { depth--; if (depth === 0) return text.slice(open, i + 1); }
  }
  return text.slice(open, text.length);
}

/** handler 常把逻辑转给一个本地函数(资产就是这么写的:handler 只调 maximizeWindow)。
 *  只跟一跳 —— 够把"这个文件别处也有 isMaximized"那种误判挡掉,又不至于变成类型系统。 */
function bodyOrOneHop(text, index, want) {
  const body = parenFrom(text, index);
  if (want.every((re) => re.test(body))) return body;
  for (const m of body.matchAll(/\b([A-Za-z_$][\w$]*)\s*\(/g)) {
    const name = m[1];
    const re = new RegExp(`(?:function|const)\\s+${name}\\b`);
    const di = re.exec(text);
    if (!di) continue;
    const def = blockFrom(text, di.index);
    if (want.every((w) => w.test(def))) return def;
  }
  return body;
}

/* ---------- ⓪ 自证:注释剥离器有没有被绕过 ---------- */
{
  const bad = files.filter((f) => {
    const open = (f.text.match(/\/\*/g) || []).length;
    const close = (f.text.match(/\*\//g) || []).length;
    return open !== close;
  });
  if (bad.length) WARN('stripper', `这 ${bad.length} 个文件的 /* 与 */ 不配平(${bad.slice(0, 3).map((f) => f.rel).join(', ')} 等)` +
    `—— 注释剥离器可能没吃干净,本文件相关的判定要人工看一眼行号。`);
  else PASS('stripper', '注释剥离自证通过(所有文件 /* 与 */ 配平)');
}

/* ---------- ① -webkit-app-region 必须整条撤掉 ----------
   只认"声明位":CSS 里的 `属性: drag`,和 JS 里给元素设 webkitAppRegion='drag'。
   探针读的 `getComputedStyle(el)['-webkit-app-region']` 不算声明,注释已被抹平也不算。 */
{
  const h = hit(css, /-webkit-app-region\s*:\s*drag/);
  const hc = hit(code, /webkitAppRegion\s*[:=]\s*['"`]drag|['"]-webkit-app-region['"]\s*[:,]?\s*['"`]drag/);
  const at = h || hc;
  if (at) FAIL('app-region', `还留着 -webkit-app-region: drag(@${at.file}:${at.line}) —— 这条会把区域变成 OS 的 HTCAPTION,` +
    `右键被 DefWindowProc 接走弹深色系统菜单,DOM 收不到 contextmenu。必须删掉,只留 .drag-region 类标记。`);
  else PASS('app-region', '无 -webkit-app-region: drag 声明');
  const h2 = hit(css, /-webkit-app-region/);
  if (h2) WARN('app-region-leftover', '还有 -webkit-app-region 残留(no-drag 那类)。拖拽改由 JS 接管后它已无用,建议一并删掉,免得日后误读成"这条带是 OS 在管"。', `${h2.file}:${h2.line}`);
}

/* ---------- ② frame:false + transparent:true ---------- */
{
  const h = hit(code, /frame\s*:\s*false/);
  if (h) PASS('frame-false', '有 frame:false', `${h.file}:${h.line}`);
  else FAIL('frame-false', '没找到 frame:false —— 有 OS 边框时系统自己有一套标题带,自绘控制条会与它打架');
  const t = hit(code, /transparent\s*:\s*true/);
  if (t) PASS('transparent', '有 transparent:true(退场过程露出真桌面,与系统动画同向)');
  else WARN('transparent', '没找到 transparent:true —— 退场时窗口不透明的话,收缩过程中露出的是窗口自己的底色,观感是"整块卡片变淡"而不是"缩进桌面"');
}

/* ---------- ③ 三个控制 IPC ---------- */
{
  for (const ch of ['window:minimize', 'window:maximize', 'window:close']) {
    const h = hit(code, new RegExp(`ipcMain\\.handle\\(\\s*['"]${ch}['"]`));
    if (h) PASS('ipc-' + ch.split(':')[1], `主进程有 ${ch}`, `${h.file}:${h.line}`);
    else FAIL('ipc-' + ch.split(':')[1], `主进程没有 ipcMain.handle('${ch}') —— 那颗按钮点了不会有反应`);
  }
  const h = hit(code, /ipcMain\.handle\(\s*['"]window:maximize['"]/);
  if (h) {
    /* 双向判断必须在"这条 handler 真会执行到的那段"里 —— 同文件别处的 isMaximized(拖拽分支要用)
       不能替它作证,所以 bodyOrOneHop 只跟一跳:handler 内联,或 handler 调一个本地函数。 */
    const f = code.find((x) => x.rel === h.file);
    const body = f ? bodyOrOneHop(f.blank, h.index, [/isMaximized/, /unmaximize/]) : '';
    if (/isMaximized/.test(body) && /unmaximize/.test(body)) PASS('max-toggle', '最大化是双向切换(最大化 ⇄ 还原)', `${h.file}:${h.line}`);
    else FAIL('max-toggle', 'window:maximize 这条路径(含它调用的本地函数)里没有 isMaximized/unmaximize 双向判断 ' +
      '—— 已经最大化的窗口再点一次不会还原,双击标题带也就恢复不了', `${h.file}:${h.line}`);
  }
}

/* ---------- ④ 拖拽三件套 + 光标在主进程读 ---------- */
let dragStartBlock = null, dragMoveBlock = null;
{
  for (const ch of ['window:drag-start', 'window:drag-move', 'window:drag-end']) {
    const h = hit(code, new RegExp(`ipcMain\\.on\\(\\s*['"]${ch}['"]`));
    if (h) PASS('ipc-' + ch, `主进程有 ${ch}`, `${h.file}:${h.line}`);
    else FAIL('ipc-' + ch, `主进程没有 ipcMain.on('${ch}') —— 拖拽整条链断在这里`);
    if (ch === 'window:drag-start') dragStartBlock = h ? parenFrom(h.blank, h.index) : null;
    if (ch === 'window:drag-move') dragMoveBlock = h ? { body: parenFrom(h.blank, h.index), where: `${h.file}:${h.line}` } : null;
  }
  /* 光标必须在 drag-move 那一团里读 —— 文件别处调了 getCursorScreenPoint 不算,
     否则"渲染层传坐标 + 主进程另有光标读取"这种混着写的工程会假绿。 */
  const gc = (dragStartBlock && /getCursorScreenPoint\(\)/.test(dragStartBlock)) ||
    (dragMoveBlock && /getCursorScreenPoint\(\)/.test(dragMoveBlock.body));
  if (gc) PASS('cursor-in-main', '位移用的是主进程现读光标(不累计误差、丢帧下一帧补上)');
  else FAIL('cursor-in-main', 'drag-start/drag-move 里没读到 getCursorScreenPoint() —— 若改由渲染层传坐标,IPC 延迟会被算进位移,手感变成"窗口追光标"');
  const dts = code.filter((f) => /contextBridge|ipcRenderer/.test(f.blank));
  const badCoord = hit(dts, /drag(?:Start|Move|End)\s*:\s*\(\s*[a-zA-Z_$]/);
  if (badCoord) FAIL('drag-no-coords', `preload 的 drag* 带了参数(${badCoord.match}) —— 契约是零参数,坐标由主进程读`, `${badCoord.file}:${badCoord.line}`);
  else PASS('drag-no-coords', 'preload 的 drag* 是零参数');
}

/* ---------- ⑤ 单击不能把最大化的窗口拽下来(本轮实测过的真 bug) ---------- */
{
  if (dragStartBlock !== null) {
    if (/unmaximize/.test(dragStartBlock)) {
      FAIL('slop-gate', 'window:drag-start 的 handler 里有 unmaximize —— 起手就还原等于"双击最大化之后单击即恢复"。' +
        '必须记 wasMax,等位移超过起拖阈值(SM_CXDRAG≈4px)才在 drag-move 里还原。');
    } else PASS('slop-gate', 'drag-start 不做还原(没有"单击即恢复"的隐患)');
  }
  if (dragMoveBlock) {
    const hasUn = /unmaximize/.test(dragMoveBlock.body);
    const hasSlop = /Math\.abs\([^)]*\)\s*>=/.test(dragMoveBlock.body) || /pastDragSlop|DRAG_SLOP|dragSlop/.test(dragMoveBlock.body);
    if (hasUn && hasSlop) PASS('slop-gate-move', 'drag-move 里"还原"被阈值挡住', dragMoveBlock.where);
    else if (hasUn && !hasSlop) FAIL('slop-gate-move', 'drag-move 直接 unmaximize 而没有位移阈值判断 —— 最大化状态下一动就还原,单击会误触发', dragMoveBlock.where);
    else WARN('slop-gate-move', 'drag-move 里没有从最大化拖下来的分支:拖最大化的窗口会整带不动(Windows 的原生行为是能拖下来还原)', dragMoveBlock.where);
  }
}

/** 从命中点起取"这一整团圆括号"的内容。
 *  为什么不用花括号:listener 常写成箭头不带花括号(`win.on('restore', () => send(...))`),
 *  找下一个 { 会跳到文件里别的函数体上去 —— 判定就会假绿。 */
function parenFrom(text, index) {
  const open = text.indexOf('(', index);
  if (open < 0) return '';
  let depth = 0;
  for (let i = open; i < text.length; i++) {
    if (text[i] === '(') depth++;
    else if (text[i] === ')') { depth--; if (depth === 0) return text.slice(open, i + 1); }
  }
  return text.slice(open, text.length);
}

/* ---------- ⑥ restore 推送贯通三处 ---------- */
{
  const m = hit(code, /\.on\(\s*['"]restore['"]/);
  /* 只看这条 listener 自己那一团括号里的内容:同文件别处的 send 不能替它作证。
     频道名可能是变量(资产用 restoreChannel),所以先判"这段里有 webContents.send",
     再把频道字面量单独对到 preload 的监听上。 */
  const body = m ? parenFrom(m.blank, m.index) : '';
  const sends = /webContents\.send\s*\(/.test(body);
  const chanLit = /webContents\.send\(\s*['"]([^'"]+)['"]/.exec(body);
  const preload = hit(code.filter((f) => /ipcRenderer\.on/.test(f.blank)), /ipcRenderer\.on\(\s*['"]([^'"]+)['"]/);
  const chanMain = chanLit ? chanLit[1] : (m && /window:restored/.test(m.text) ? 'window:restored' : null);
  if (!m) FAIL('restore-main', "主进程没有 win.on('restore') —— 退场留下的类没机会摘,从任务栏点回来还是透明/缩小的中间态");
  else if (!sends) FAIL('restore-main', "win.on('restore') 的回调里没有 webContents.send(...) —— 渲染层收不到恢复信号,同上那条症状", `${m.file}:${m.line}`);
  else if (!chanMain) WARN('restore-main', 'restore 回调发的频道名对不出字面量,没法自动和 preload 比 —— 人工确认两端频道名一致', `${m.file}:${m.line}`);
  else PASS('restore-main', `主进程 win.on('restore') 会推 ${chanMain}`, `${m.file}:${m.line}`);
  if (!preload) FAIL('restore-preload', 'preload 里没有 ipcRenderer.on(...) —— 主进程的推送没人接');
  else if (chanMain && preload.g[0] !== chanMain) FAIL('restore-chan-match', `频道名两端不一致:主进程 ${chanMain} vs preload ${preload.g[0]} —— 恢复信号永远收不到`);
  else PASS('restore-preload', `preload 在监听 ${preload.g[0]}`);
}

/* ---------- ⑦ 渲染层组件接线 ---------- */
{
  const comp = code.find((f) => /drag-region/.test(f.blank) && /dragStart/.test(f.blank) && /contextmenu/i.test(f.blank));
  if (!comp) {
    FAIL('component', '没找到一个同时含 .drag-region / dragStart / contextmenu 的渲染层组件 —— WindowChrome.tsx 没落位或没被引用');
  } else {
    PASS('component', `窗口壳组件在 ${comp.rel}`);
    const need = [
      [/e\.preventDefault\(\)/, 'contextmenu 里吃了默认菜单(不吃就还是 OS 的深色菜单)'],
      [/setPointerCapture/, '起手捕获指针(不捕获 ⇒ 光标甩出窗口后窗口停在半路)'],
      [/pointercancel/, '监听了 pointercancel'],
      [/'blur'|window\.addEventListener\(['"]blur/, '失焦时结束拖拽(否则窗口换焦点后下一次 pointermove 会隐形搬窗)'],
      [/dblclick/, '双击拖拽带切最大化'],
      [/transitionend/, '退场靠 transitionend 收尾'],
      [/setTimeout/, '有兜底计时器(reduced-motion 下 transition 不跑、事件永远不来)'],
      [/window--noanim|noanim/, '恢复时挂 noanim 不反向播'],
      [/offsetWidth|offsetHeight|getBoundingClientRect/, '摘类前有强制回流(否则两条 class 操作合并进同一帧,transition 照样播)'],
      [/prefers-reduced-motion|reduceMotion|matchMedia/, '读了 reduced-motion(自己 matchMedia 或 import 一个共享的 reduceMotion 常量都算)'],
      [/createPortal/, '控制条 portal 到 body(否则活在拖拽带的层叠上下文里,会被祖先 transform/backdrop-filter 裁掉)'],
      [/Math\.max\(8,\s*Math\.min|clamp|innerWidth\s*-\s*WIN_MENU_W/, '菜单位置做了贴边 clamp'],
    ];
    for (const [re, why] of need) {
      const at = comp.blank.search(re);
      const line = at >= 0 ? comp.text.slice(0, at).split('\n').length : 0;
      if (at >= 0) VERBOSE && PASS('comp:' + re.source.slice(0, 18), 'ok', `${comp.rel}:${line}`);
      else FAIL('comp:' + re.source.slice(0, 24), why, comp.rel);
    }
    const used = files.some((f) => f !== comp && /<WindowChrome/.test(f.blank));
    if (used) PASS('component-mounted', '窗口壳已被渲染树挂载', f_mount_hint(files));
    else FAIL('component-mounted', '组件文件在,但渲染树里没有任何地方挂它(搜过 <WindowChrome) —— 于是整套逻辑一行都不会跑', comp.rel);
  }
  const bands = files.filter((f) => /className="[^"]*drag-region|class="[^"]*drag-region/.test(f.blank)).length;
  if (bands === 0) FAIL('drag-band-marked', '没有任何元素标 .drag-region ⇒ 无处可以起手拖窗,整窗拖不动');
  else PASS('drag-band-marked', `${bands} 个文件里有拖拽带标记`);
}
function f_mount_hint(list) {
  const h = hit(list, /<WindowChrome/);
  return h ? `${h.file}:${h.line}` : '';
}

/* ---------- ⑧ 退场动画的 CSS 要件 ---------- */
{
  const h = hit(css, /\.window--minimizing\s*\{[^}]*\}/s);
  if (!h) FAIL('exit-css', 'CSS 里没有 .window--minimizing 规则(退场没有可挂的类)');
  else {
    const body = h.match;
    if (/scale\(/.test(body)) PASS('exit-scale', '退场有缩放');
    else FAIL('exit-scale', '.window--minimizing 里没有 scale() —— 只剩位移,不像收进任务栏', `${h.file}:${h.line}`);
    if (/transform-origin\s*:\s*50%\s+100%/.test(body)) PASS('exit-origin', '收拢点取窗口自身底边中心(不瞄任务栏按钮 ⇒ Win10/Win11 同一条 transition)');
    else FAIL('exit-origin', '.window--minimizing 没写 transform-origin: 50% 100% —— 默认 50% 50% 会原地缩,观感是"卡片变小"而不是"收下去"', `${h.file}:${h.line}`);
    if (/will-change\s*:\s*transform/.test(body)) PASS('exit-layer', '退场期间提升合成层');
    else WARN('exit-layer', '.window--minimizing 没有 will-change —— 整窗按新缩放比重栅,里面有 WebGL 或 backdrop blur 时会明显掉帧(两轮实测:有 will-change 时呈现帧 15-17/18-19,去掉后 11-13/16)', `${h.file}:${h.line}`);
  }
  const tr = hit(css, /\.window\s*\{[^}]*transition[^}]*transform[^}]*\}/s);
  if (tr) PASS('exit-transition', '根元素上有 transform 的 transition', `${tr.file}:${tr.line}`);
  else FAIL('exit-transition', '窗口根元素没有 transition: transform —— transitionend 永远不来,最小化只能等兜底计时器,手感慢一拍');
  const rm = hit(css, /prefers-reduced-motion[\s\S]{0,200}transition\s*:\s*none/);
  if (rm) PASS('reduced-motion-css', 'reduced-motion 下关掉了过渡');
  else WARN('reduced-motion-css', 'CSS 里没看到 prefers-reduced-motion 分支 —— 系统开了"减少动态效果"时仍会播动画');
}

/* ---------- ⑨ 控制条盒尺寸:常量要和 CSS 对上(clamp 用它) ---------- */
{
  const comp = code.find((f) => /WIN_MENU_W\s*=\s*(\d+)/.test(f.blank));
  const cw = comp && /WIN_MENU_W\s*=\s*(\d+)/.exec(comp.blank);
  const ch = comp && /WIN_MENU_H\s*=\s*(\d+)/.test(comp.blank) ? /WIN_MENU_H\s*=\s*(\d+)/.exec(comp.blank) : null;
  const menu = hit(css, /\.win-menu\s*\{[^}]*\}/s);
  const btn = hit(css, /\.win-menu-btn\s*\{[^}]*\}/s);
  if (!cw || !menu || !btn) {
    WARN('menu-box', '没能凑齐"常量 + .win-menu + .win-menu-btn"三样,跳过尺寸配平(若你换了类名,自己确认 clamp 用的宽高与实际一致)');
  } else {
    const num = (re, d) => { const m = re.exec(menu.match + btn.match); return m ? parseInt(m[1], 10) : d; };
    const pad = num(/padding:\s*(\d+)px/, 0);
    const gap = num(/gap:\s*(\d+)px/, 0);
    const bw = num(/width:\s*(\d+)px/, 0);
    const bh = num(/height:\s*(\d+)px/, 0);
    const expW = 2 * pad + 3 * bw + 2 * gap;
    const expH = 2 * pad + bh;
    const gotW = parseInt(cw[1], 10);
    const gotH = ch ? parseInt(ch[1], 10) : NaN;
    const where = `${comp.rel}:${comp.text.slice(0, cw.index).split('\n').length}`;
    if (expW === gotW && expH === gotH) PASS('menu-box', `clamp 用的 ${gotW}×${gotH} 与 CSS 算出的 ${expW}×${expH} 一致`);
    else FAIL('menu-box', `clamp 用的是 ${gotW}×${gotH},CSS 实际盒尺寸是 ${expW}×${expH} —— 差 ${Math.abs(expW - gotW)}×${Math.abs(expH - gotH)}。` +
      `贴屏幕边缘右键时菜单会被画到屏幕外(改按钮尺寸/内边距/间距要同步改常量)`, where);
  }
  const fix = hit(css, /\.win-menu-wrap\s*\{[^}]*position\s*:\s*fixed[^}]*\}/s);
  const fixOnMenu = /\.win-menu\s*\{[^}]*position\s*:\s*fixed/s.test(css.map((f) => f.blank).join('\n'));
  if (fix) PASS('menu-fixed-wrap', 'fixed 在包裹层上(动效的 translate/scale 不会把 fixed 的包含块抢走)');
  else FAIL('menu-fixed-wrap', '.win-menu-wrap 上没有 position:fixed —— 定位会被父级层叠上下文接管');
  if (fixOnMenu) WARN('menu-fixed-nested', '.win-menu 自己也写了 position:fixed —— 若包裹层带动效,祖先 transform 会让菜单位置跟着位移抖 8px,把 fixed 只留在包裹层');
}

/* ---------- ⑩ package.json 里不该再有 OS 标题带的痕迹 ---------- */
if (rootPkg && !/electron/.test(rootPkg)) WARN('not-electron', `${ROOT} 的 package.json 里没有 electron 依赖 —— 这套东西只在 Electron 里有意义`);

/* ---------- 输出 ---------- */
const ICON = { PASS: ' ok ', WARN: 'warn', FAIL: 'FAIL' };
for (const r of rows) {
  if (r.level === 'PASS' && !VERBOSE) continue;
  console.log(`[${ICON[r.level]}] ${r.id}${r.where ? '  @' + r.where : ''}`);
  console.log(`        ${r.msg}`);
}
if (!VERBOSE) {
  const passed = rows.filter((r) => r.level === 'PASS').length;
  console.log(`(通过的 ${passed} 条已省略,加 --verbose 全列)`);
}
console.log('');
console.log(`扫了 ${files.length} 个文件(${ROOT})`);
console.log(`结果 PASS=${nPass} WARN=${nWarn} FAIL=${nFail}`);
if (nFail) {
  console.log('结论:接线不完整。上面的 FAIL 每条都对应一个已实测的静默失效 —— 症状往往是"点了没反应"或"还是弹系统菜单",不会报错。');
  process.exit(1);
}
console.log(nWarn
  ? `结论:接线完整,${nWarn} 条警告待确认。静态审计通过 ≠ 效果正确,真窗口三腿验收见 references/verification.md。`
  : '结论:接线完整。静态审计通过 ≠ 效果正确,真窗口三腿验收见 references/verification.md。');
