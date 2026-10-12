import { app, BrowserWindow, clipboard, ipcMain, nativeImage, nativeTheme, screen, session } from 'electron';
import { createHash } from 'node:crypto';
import { writeFileSync } from 'node:fs';
import path from 'node:path';

// 探针是靠 console.log 往 stdout 写长行的:读端先退(把输出接给一个立刻结束的
// grep/head)会让写入抛 EPIPE,而 Electron 把主进程未捕获异常弹成模态错误框 ——
// 症状是"跑探针时突然弹一个 Error 窗",跟被测功能毫无关系。写不出去不该影响应用。
process.stdout.on('error', () => {});
process.stderr.on('error', () => {});

const APP_TITLE = 'ChatAPP';
const SELFTEST = process.argv.includes('--selftest');
const shotIndex = process.argv.indexOf('--shot');
const SHOT_PATH = shotIndex >= 0 ? process.argv[shotIndex + 1] : '';
// --crop x,y,w,h:只取这一块的位图(页面 CSS px)。整窗 1280x800 缩进截图里看圆角,
// 细节全被重采样抹掉,必须按 1:1 裁才有判据。
const cropIndex = process.argv.indexOf('--crop');
const CROP = cropIndex >= 0 ? process.argv[cropIndex + 1].split(',').map(Number) : null;
const scrollIndex = process.argv.indexOf('--scroll');
const SCROLL_TOP = scrollIndex >= 0 ? Number(process.argv[scrollIndex + 1]) : NaN;
// --height n:出图前把内容高度改成 n。默认 1280x800 下种子消息放得下、列表根本不可滚,
// "上划后才出现"的那类控件必须先把窗口压矮才取得到证。
const heightIndex = process.argv.indexOf('--height');
const SHOT_HEIGHT = heightIndex >= 0 ? Number(process.argv[heightIndex + 1]) : 0;
// --hover x,y:出图前把指针停在 x,y(页面 CSS px),让 BorderGlow 的 hover 辉光进入采样帧
const hoverIndex = process.argv.indexOf('--hover');
const HOVER = hoverIndex >= 0 ? process.argv[hoverIndex + 1].split(',').map(Number) : null;
const framesIndex = process.argv.indexOf('--frames');
const FRAMES_MS = framesIndex >= 0 ? Number(process.argv[framesIndex + 1]) || 3000 : 0;
const foldIndex = process.argv.indexOf('--fold');
const FOLD_ROW = foldIndex >= 0 ? Number(process.argv[foldIndex + 1]) || 0 : -1;
const winMenuShotIndex = process.argv.indexOf('--winmenu-shot');
const WINMENU_SHOT = winMenuShotIndex >= 0 ? process.argv[winMenuShotIndex + 1] : '';
const WINMENU = process.argv.includes('--winmenu') || WINMENU_SHOT !== '';
const RESIZE = process.argv.includes('--resize');
const PHRASE = process.argv.includes('--phrase');
const gnIndex = process.argv.indexOf('--gn');
const GN_ROW = gnIndex >= 0 ? Number(process.argv[gnIndex + 1]) : -1;
const gnShotIndex = process.argv.indexOf('--gnshot');
const GN_BASE = gnShotIndex >= 0 ? process.argv[gnShotIndex + 1] : '';
const TABS = process.argv.includes('--tabs');
const ROWS = process.argv.includes('--rows');
const CORNER = process.argv.includes('--corner');
const protoIndex = process.argv.indexOf('--proto');
const PROTO = protoIndex >= 0;
const CORNERSHOT = process.argv.includes('--cornershot');
const LEAK = process.argv.includes('--leak');
const colsWIndex = process.argv.indexOf('--cols-w');
const COLS = process.argv.includes('--cols');
const COLS_SHOT = process.argv.includes('--cols-shot');
// --cornershot 8,9 → 只单独出这两档(1-based,对应上一张对照表的编号);不给参数就是全 10 档
const csArg = process.argv[process.argv.indexOf('--cornershot') + 1] ?? '';
const CS_PICK = /^\d/.test(csArg) ? csArg.split(',').map((s) => Number(s.trim()) - 1).filter((n) => n >= 0) : null;
const protoArg = PROTO ? process.argv[protoIndex + 1] : '';
const PROTO_BOX = protoArg && !protoArg.startsWith('--') ? protoArg.split(',').map(Number) : null;
const tabsClickIndex = process.argv.indexOf('--tabs-click');
const TABS_CLICK = tabsClickIndex >= 0 ? Number(process.argv[tabsClickIndex + 1]) : -1;

/**
 * 帧间隔自证:在真实窗口里先采一段 G2(corner-shape: superellipse)生效的,
 * 再把 --g2-shape 换成 round(等价于不开)采一段对照。
 * 两段都回读 cornerShape 计算值——不然"没有差异"可能只是两边都没换形状,判据会假绿。
 */
function frameProbe(ms: number): string {
  return `(async () => {
    const MS = ${ms};
    const sample = () => new Promise((res) => {
      const d = [];
      let prev = performance.now();
      const t0 = prev;
      const tick = (t) => { d.push(t - prev); prev = t; if (t - t0 < MS) requestAnimationFrame(tick); else res(d); };
      requestAnimationFrame(tick);
    });
    const stat = (d) => {
      const s = d.slice(1).sort((a, b) => a - b);
      const q = (p) => +s[Math.min(s.length - 1, Math.floor(s.length * p))].toFixed(2);
      return { n: s.length, p50: q(0.5), p95: q(0.95), max: +s[s.length - 1].toFixed(2) };
    };
    const state = () => ({
      shape: getComputedStyle(document.querySelector('.sidebar')).cornerShape,
      token: getComputedStyle(document.documentElement).getPropertyValue('--g2-shape').trim(),
      vis: document.visibilityState,
    });
    const on = { ...stat(await sample()), ...state() };
    document.documentElement.style.setProperty('--g2-shape', 'round');
    void document.body.offsetWidth;
    const off = { ...stat(await sample()), ...state() };
    document.documentElement.style.removeProperty('--g2-shape');
    return { on, off };
  })()`;
}

/**
 * 群名折叠动画自证:点第 row 条会话,趁动画进行中采一次,落定后再采一次。
 * 必须两帧不同——只看落定那一帧分不清"播过折叠动画"和"根本没有动画"。
 * 顺带回读头部四项(图标/名称/人数+群号/底色),证明它跟选中条目同源。
 */
function foldProbe(row: number): string {
  return `(async () => {
    const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
    const head = () => {
      const t = document.querySelector('.header-pill .title');
      const pieces = [...t.querySelectorAll('.fold-text-piece')];
      const ops = pieces.map((p) => +getComputedStyle(p).opacity);
      const mid = pieces[Math.floor(pieces.length / 2)];
      const av = document.querySelector('.header-pill .avatar');
      return {
        text: document.querySelector('.fold-text-sr-only').textContent,
        subtitle: document.querySelector('.header-pill .subtitle').textContent,
        avatar: av.textContent,
        avatarBg: getComputedStyle(av).backgroundColor,
        n: pieces.length,
        opMin: +Math.min(...ops).toFixed(3),
        opMax: +Math.max(...ops).toFixed(3),
        midTransform: getComputedStyle(mid).transform
      };
    };
    const rows = [...document.querySelectorAll('.chat-item')];
    const before = head();
    const clicked = rows[${row}] ? rows[${row}].querySelector('.name').textContent : null;
    if (rows[${row}]) rows[${row}].click();
    await sleep(150);
    const during = head();
    await sleep(1400);
    const after = head();
    return { rows: rows.length, clicked, before, during, after, vis: document.visibilityState };
  })()`;
}

nativeTheme.themeSource = 'light';

/**
 * 水珠颜色自证:静止帧 → 点第 row 个 tab → 700ms 后的飞行帧 → 再等 3s 的落定帧,三张连拍。
 * 为什么要静止/飞行两张:"这一帧里没有彩色水珠"本身证不了什么(挂载本来就不放水珠),
 * 必须先证明飞行确实在进行(两帧有差),再看那帧里水珠是什么色。
 * 工程里 animationTime 临时调到 30000ms 把动画冻在飞行中段,取证后改回 450。
 */
async function runGnProbe(win: BrowserWindow, row: number, base: string): Promise<void> {
  const wc = win.webContents;
  const rest = await wc.capturePage();
  const clicked = await wc.executeJavaScript(
    `(() => {
      const b = document.querySelectorAll('button.gooey-nav__link')[${row}];
      const before = document.querySelectorAll('.chat-item').length;
      b.click();
      return { label: b.textContent.trim(), before };
    })()`
  );
  await sleep(700);
  const fly = await wc.capturePage();
  await sleep(3000);
  const end = await wc.capturePage();
  const after = await wc.executeJavaScript(
    `(() => ({ items: document.querySelectorAll('.chat-item').length,
       head: [...document.querySelectorAll('.gooey-nav__list--active .gooey-nav__link')].map((l) => l.textContent.trim()) }))()`
  );
  if (base) {
    writeFileSync(base + '.rest.png', rest.toPNG());
    writeFileSync(base + '.fly.png', fly.toPNG());
    writeFileSync(base + '.end.png', end.toPNG());
  }
  console.log('gn ' + JSON.stringify({ row: clicked, after, shots: base && [base + '.rest.png', base + '.fly.png', base + '.end.png'] }));
}

/**
 * 左栏过滤行几何自证:tab 右侧那段留白到底归哪个盒管,只有逐盒量出来才能定。
 * 链路是 nav(出外框) → stage → list → 4 个 item,谁的 right 落在 nav 内沿之前,谁就是留白的归属。
 */
const TABS_STATE = `(() => {
  const box = (el) => {
    const r = el.getBoundingClientRect();
    return { x: +r.x.toFixed(1), y: +r.y.toFixed(1), w: +r.width.toFixed(1), h: +r.height.toFixed(1), right: +r.right.toFixed(1) };
  };
  const nav = document.querySelector('.filter-nav');
  const ncs = getComputedStyle(nav);
  const side = document.querySelector('.sidebar');
  const scs = getComputedStyle(side);
  const tabs = document.querySelector('.filter-tabs');
  const tcs = getComputedStyle(tabs);
  const stage = nav.querySelector('.gooey-nav__stage');
  const list = nav.querySelector('.gooey-nav__list:not(.gooey-nav__list--active)');
  const items = [...list.children].map((li) => ({ ...box(li), label: li.textContent.trim() }));
  const last = items[items.length - 1];
  return {
    sidebar: { w: +box(side).w.toFixed(1), padL: scs.paddingLeft, padR: scs.paddingRight },
    tabs: { ...box(tabs), padL: tcs.paddingLeft, padR: tcs.paddingRight },
    nav: { ...box(nav), width: ncs.width, pad: ncs.padding, gnPad: ncs.getPropertyValue('--gn-pad').trim() },
    stage: box(stage),
    list: { ...box(list), cssW: getComputedStyle(list).width },
    items,
    distinctTops: [...new Set(items.map((i) => i.y))].length,
    innerRight: +(nav.getBoundingClientRect().right - parseFloat(ncs.paddingRight)).toFixed(1),
    gapToNavInner: +(nav.getBoundingClientRect().right - parseFloat(ncs.paddingRight) - last.right).toFixed(1),
    gapToStage: +(stage.getBoundingClientRect().right - last.right).toFixed(1),
    fallbackDisplay: getComputedStyle(nav.querySelector('.gooey-nav__fallback')).display
  };
})()`;

async function runTabsProbe(win: BrowserWindow): Promise<void> {
  const wc = win.webContents;
  const out: Record<string, unknown> = { on: (await wc.executeJavaScript(TABS_STATE)) as unknown };
  if (TABS_CLICK >= 0) {
    // 水珠是 canvas 画的,DOM 里读不到;只能"点第 n 格 → 等落定 → 按 1:1 裁这块位图",
    // 再交给 .cache/tabs-pill.mjs 从像素里量白色 pill 的包围盒。
    out.clicked = await wc.executeJavaScript(
      `(() => { const b = document.querySelectorAll('button.gooey-nav__link')[${TABS_CLICK}];
         const label = b.textContent.trim(); b.click(); return label; })()`
    );
    await sleep(1400);
    // 两张图用同一个矩形:nav 是 width:100%,stage 收不收缩都不动它,所以前后位图可逐像素比。
    const rect = (await wc.executeJavaScript(
      `(() => { const r = document.querySelector('.filter-nav').getBoundingClientRect();
         return { x: r.x - 12, y: r.y - 12, width: r.width + 24, height: r.height + 24 }; })()`
    )) as Electron.Rectangle;
    const shot = async (file: string) => {
      const image = await wc.capturePage(rect);
      writeFileSync(file, image.toPNG());
      return image.getSize();
    };
    out.onShot = { rect, size: await shot('.cache/tabs-on.png') };
    // 对照:把 stage 的拉伸临时收回成改前的默认值(flex:0 1 auto)。"留白 18.2px"要有像素读数,
    // 不能只靠 DOM 推——组件里的 ResizeObserver 会 measure() 重量 pill,所以水珠会跟着缩回去。
    out.controlWidth = await wc.executeJavaScript(
      `(() => { const st = document.querySelector('.filter-nav .gooey-nav__stage');
         st.style.flex = '0 1 auto'; void st.offsetWidth; return getComputedStyle(st).width; })()`
    );
    await sleep(700);
    out.off = { state: await wc.executeJavaScript(TABS_STATE), size: await shot('.cache/tabs-off.png') };
    await wc.executeJavaScript(
      `(() => { document.querySelector('.filter-nav .gooey-nav__stage').style.removeProperty('flex'); return 1; })()`
    );
  }
  console.log('tabs ' + JSON.stringify(out));
}

/**
 * 左栏条目间距 + 中间卡圆角的几何自证。
 * 间距要看三层:列表盒(display/row-gap/padding)、条目盒(高/外边距)、头像在条目里的上下空档
 * —— 用户眼里的"间距"是最后一层,只量前两层的 gap=0 会得出"没问题"的假绿结论。
 * 圆角那侧:DOM 只能给 computed 值(上一轮就是这么判绿的),真正的轮廓在像素层,配 .cache/g2-fit.mjs。
 */
const ROWS_STATE = `(() => {
  const box = (el) => { const r = el.getBoundingClientRect(); return { x: +r.x.toFixed(1), y: +r.y.toFixed(1), w: +r.width.toFixed(1), h: +r.height.toFixed(1) }; };
  const list = document.querySelector('.chat-list');
  const lcs = getComputedStyle(list);
  const rows = [...document.querySelectorAll('.chat-item')];
  const r0 = rows[0];
  const rcs = getComputedStyle(r0);
  const items = rows.map((li) => {
    const b = box(li);
    const av = box(li.querySelector('.avatar'));
    return {
      name: li.querySelector('.name').textContent.slice(0, 6), box: b,
      insetTop: +(av.y - b.y).toFixed(1), insetBottom: +(b.y + b.h - av.y - av.h).toFixed(1),
      bg: getComputedStyle(li).backgroundColor
    };
  });
  const pitch = items.slice(1).map((it, i) => +(it.box.y - items[i].box.y).toFixed(1));
  const gap = items.slice(1).map((it, i) => +(it.box.y - (items[i].box.y + items[i].box.h)).toFixed(1));
  const area = document.querySelector('.chat-area');
  const acs = getComputedStyle(area);
  const win = document.querySelector('.window');
  const wcs = getComputedStyle(win);
  const comp = document.querySelector('.composer-wrap');
  return {
    list: { ...box(list), display: lcs.display, rowGap: lcs.rowGap, padding: lcs.padding },
    item: { height: rcs.height, marginTop: rcs.marginTop, marginBottom: rcs.marginBottom, radius: rcs.borderRadius, shape: rcs.cornerShape },
    n: items.length, pitch, gap, items,
    scrollable: { client: list.clientHeight, content: list.scrollHeight, over: list.scrollHeight > list.clientHeight },
    chatArea: { ...box(area), radius: acs.borderRadius, shape: acs.cornerShape, overflow: acs.overflow, bg: acs.backgroundColor, shadow: acs.boxShadow },
    window: { ...box(win), radius: wcs.borderRadius, shape: wcs.cornerShape, bg: wcs.backgroundColor },
    composerWrap: { ...box(comp), bg: getComputedStyle(comp).backgroundColor }
  };
})()`;

async function runRowsProbe(win: BrowserWindow): Promise<void> {
  const out = await win.webContents.executeJavaScript(ROWS_STATE);
  console.log('rows ' + JSON.stringify(out));
}

/**
 * 中间卡圆角自证。洗色 `--chat-wash` 与窗口底几乎同亮(实测差 8~9 级),低对比下
 * "看不见边"和"根本没裁"在截图里长得一样,直接量原图会得到一片"没找到边"——那是判据空转。
 * 所以三态:① 原样 ② 卡片刷成不透明红 + 子级全隐(只留这只盒子自己) ③ 在 ② 上把 radius 归 0。
 * ② 的轮廓若贴着 G2 而 ③ 是直角,才说明"角"真的被裁掉了。
 */
const CORNER_INJECT = (extra: string) =>
  `(() => {
    document.getElementById('corner-probe')?.remove();
    const s = document.createElement('style');
    s.id = 'corner-probe';
    s.textContent = '.chat-area{background:#f00 !important;transition:none !important;${extra}}.chat-area>*{visibility:hidden !important}';
    document.head.appendChild(s);
    const a = document.querySelector('.chat-area');
    const cs = getComputedStyle(a);
    return { text: s.textContent, bg: cs.backgroundColor, radius: cs.borderRadius, shape: cs.cornerShape, childVis: getComputedStyle(a.firstElementChild).visibility };
  })()`;

/** 页内"取或建"探针样式表:out.mint 那步会把它 remove 掉,后面的状态必须能自己补回来
    (不补就是 s.textContent 撞 null → 整条探针在最后一个状态上抛掉)。 */
const SHEET_FN = `const SHEET = () => { let el = document.getElementById('corner-probe'); if (!el) { el = document.createElement('style'); el.id = 'corner-probe'; document.head.appendChild(el); } return el; };`;

async function runCornerProbe(win: BrowserWindow): Promise<void> {
  const wc = win.webContents;
  const reRead = () =>
    wc.executeJavaScript(
      `(() => new Promise((res) => requestAnimationFrame(() => {
         const a = document.querySelector('.chat-area'); const cs = getComputedStyle(a);
         res({ radius: cs.borderRadius, bg: cs.backgroundColor, childVis: getComputedStyle(a.firstElementChild).visibility });
       })))()`
    );
  const out: Record<string, unknown> = {};
  const grab = async (file: string) => {
    await sleep(320); // 注入后必须等合成器出帧:capturePage 取的是"已呈现的那一帧",
    //                    紧跟在 executeJavaScript 后面拍,拿到的还是改样式之前的图(实测 cor-b 一片正常色)。
    const img = await wc.capturePage();
    writeFileSync(file, img.toPNG());
    return img.getSize();
  };
  out.a = { state: 'baseline', size: await grab('.cache/cor-a.png') };
  out.b = { state: await wc.executeJavaScript(CORNER_INJECT('')), reRead: await reRead(), size: await grab('.cache/cor-b.png') };
  out.c = { state: await wc.executeJavaScript(CORNER_INJECT('border-radius:0 !important;')), reRead: await reRead(), size: await grab('.cache/cor-c.png') };
  // ④ 回到真实外观,只把中间卡的 radius 归 0(不刷色、不隐子级)。
  //    与 ① 逐像素比:洗色与窗口底只差 ~9 级亮度,所以"有没有弧"必须靠 on/off 差出来,不能看单张。
  out.d = {
    state: await wc.executeJavaScript(
      `(() => { document.getElementById('corner-probe')?.remove();
         const s = document.createElement('style'); s.id = 'corner-probe';
         s.textContent = '.chat-area{border-radius:0 !important}';
         document.head.appendChild(s);
         return getComputedStyle(document.querySelector('.chat-area')).borderRadius; })()`
    ),
    size: await grab('.cache/cor-d.png')
  };
  // ⑤ 真实外观 + 只关掉那圈浮影 → 与 ④/原图构成"影在/影不在"的同构建对照
  out.e = {
    state: await wc.executeJavaScript(
      `(() => { const s = document.getElementById('corner-probe');
         s.textContent = '.chat-area{box-shadow:none !important}';
         return getComputedStyle(document.querySelector('.chat-area')).boxShadow; })()`
    ),
    size: await grab('.cache/cor-e.png')
  };
  // ⑥ 量角内的平均亮度时,输入栏那颗白胶囊正好压在角方格里(实测 x≥342 起 lum=252),
  //     会把"弧内/弧外"两类都污染。这两态把子级隐掉,只留卡片自己的边界;两态只差那圈影。
  const hideKids = (shadow: string) =>
    `(() => { ${SHEET_FN} const s = SHEET();
       s.textContent = '.chat-area>*{visibility:hidden !important}.chat-area{${shadow}}';
       const cs = getComputedStyle(document.querySelector('.chat-area'));
       return { shadow: cs.boxShadow, kid: getComputedStyle(document.querySelector('.chat-header')).visibility }; })()`;
  out.f = { state: await wc.executeJavaScript(hideKids('box-shadow:var(--shadow-float)')), size: await grab('.cache/cor-f.png') };
  out.g = { state: await wc.executeJavaScript(hideKids('box-shadow:none')), size: await grab('.cache/cor-g.png') };
  // ⑦ 另外两种"让角看得见"的候选:亮内环(贴着自己的轮廓走)、内环 + 外影。
  out.h = { state: await wc.executeJavaScript(hideKids('box-shadow:var(--shadow-float),inset 0 0 0 1px rgba(255,255,255,.55)')), size: await grab('.cache/cor-h.png') };
  out.i = { state: await wc.executeJavaScript(hideKids('box-shadow:inset 0 0 0 1px rgba(255,255,255,.55)')), size: await grab('.cache/cor-i.png') };
  out.j = { state: await wc.executeJavaScript(hideKids('box-shadow:var(--shadow-float),inset 0 0 0 1.5px rgba(255,255,255,.7)')), size: await grab('.cache/cor-j.png') };
  // ⑧ 反方向:把洗色本身加深(色差型边界),外影开/关各一态。
  const WASH = 'linear-gradient(165deg, rgba(255,236,198,.68) 0%, rgba(255,216,170,.55) 45%, rgba(203,175,255,.55) 100%)';
  const washOn = async (shadow: string, file: string) => {
    const st = await wc.executeJavaScript(
      `(() => { const s = document.getElementById('corner-probe');
         s.textContent = '.chat-area>*{visibility:hidden !important}.chat-area{${shadow}}';
         document.documentElement.style.setProperty('--chat-wash', '${WASH}');
         return getComputedStyle(document.querySelector('.chat-area')).backgroundImage.length; })()`
    );
    const size = await grab(file);
    await wc.executeJavaScript(`document.documentElement.style.removeProperty('--chat-wash'); 1`);
    return { state: st, size };
  };
  out.k = await washOn('box-shadow:none', '.cache/cor-k.png');
  out.l = await washOn('box-shadow:var(--shadow-float)', '.cache/cor-l.png');
  out.m = { state: await wc.executeJavaScript(hideKids('box-shadow:inset 0 0 0 1.5px rgba(255,255,255,.7)')), size: await grab('.cache/cor-m.png') };
  // ⑨ 上面 ⑥⑦⑧ 全在"子级隐掉"的状态下量,而真实渲染里底部 64px 被 `.chat-fade--bottom`
  //     的 `--chat-veil`(α .82)盖着 —— 它和窗口底一样亮,所以弧被抹平。这三态回到真实外观。
  const real = (css: string) =>
    `(() => { ${SHEET_FN} const s = SHEET(); s.textContent = ${JSON.stringify(css)};
       const cs = getComputedStyle(document.querySelector('.chat-area'));
       return { border: cs.borderTopWidth, shadow: cs.boxShadow, veil: getComputedStyle(document.documentElement).getPropertyValue('--chat-veil').trim() }; })()`;
  out.n = { state: await wc.executeJavaScript(real('.chat-area{border:1px solid rgba(30,20,50,.12)}')), size: await grab('.cache/cor-n.png') };
  // veil 覆盖必须写在元素上:`:root[data-chat-theme=…]` 的特异度(0,2,0)高于裸 `:root`(0,1,0),
  // 上一版用 `:root{--chat-veil:…}` 被主题块吃掉,量出来与不覆盖完全相同(假对照)。
  const veilOn = (v: string) =>
    `(() => { ${SHEET_FN} const s = SHEET(); s.textContent = '';
       document.querySelector('.chat-area').style.setProperty('--chat-veil', '${v}');
       return getComputedStyle(document.querySelector('.chat-fade--bottom')).background.slice(0, 46); })()`;
  out.o = { state: await wc.executeJavaScript(veilOn('rgba(255, 214, 164, 0.95)')), size: await grab('.cache/cor-o.png') };
  await wc.executeJavaScript(`document.querySelector('.chat-area').style.removeProperty('--chat-veil'); 1`);
  out.q = {
    state: await wc.executeJavaScript(
      `(() => { const s = document.getElementById('corner-probe');
         s.textContent = '.chat-area{border:1px solid rgba(30,20,50,.12)}';
         document.querySelector('.chat-area').style.setProperty('--chat-veil', 'rgba(255, 214, 164, 0.95)');
         return 1; })()`
    ),
    size: await grab('.cache/cor-q.png')
  };
  out.t = {
    state: await wc.executeJavaScript(
      `(() => { const s = document.getElementById('corner-probe');
         s.textContent = '.chat-area{border:1px solid rgba(30,20,50,.12)}';
         document.querySelector('.chat-area').style.removeProperty('--chat-veil');
         return 1; })()`
    ),
    size: await grab('.cache/cor-t.png')
  };
  // ⑩ veil 扫档:对比度来自"色比窗口底暗",不是"更不透明"——(255,240,214) 拉满 alpha 也只有 246,
  //     与窗口底 255 差 9。所以必须换更暖更深的色。找能压过右卡(16.3 / 15.3)的最浅一档。
  for (const [tag, v] of [
    ['u', 'rgba(255, 226, 186, 0.90)'],
    ['v', 'rgba(255, 214, 164, 0.88)'],
    ['w', 'rgba(255, 220, 172, 0.92)']
  ] as const) {
    out[tag] = { state: await wc.executeJavaScript(veilOn(v)), size: await grab(`.cache/cor-${tag}.png`) };
    await wc.executeJavaScript(`document.querySelector('.chat-area').style.removeProperty('--chat-veil'); 1`);
  }
  // ⑪ 第二套主题(mint)走真实路径:点第 1 条会话(themeId=mint),按出厂 CSS 量同一只角
  out.mint = {
    veil: await wc.executeJavaScript(
      `(() => { document.getElementById('corner-probe')?.remove();
         document.querySelectorAll('.chat-item')[1].click();
         return new Promise((res) => setTimeout(() => res(getComputedStyle(document.documentElement).getPropertyValue('--chat-veil').trim()), 700)); })()`
    ),
    size: await grab('.cache/cor-mint.png')
  };
  // mint 那套的 veil 与 warm 同亮度却量出更低的弧对比(15.5 vs 26.5),按实测再扫两档加深
  for (const [tag, v] of [
    ['m1', 'rgba(176, 226, 212, 0.92)'],
    ['m2', 'rgba(158, 214, 198, 0.95)']
  ] as const) {
    out[tag] = { state: await wc.executeJavaScript(veilOn(v)), size: await grab(`.cache/cor-${tag}.png`) };
  }
  await wc.executeJavaScript(`(() => { document.getElementById('corner-probe')?.remove(); return 1; })()`);
  console.log('corner ' + JSON.stringify(out));
}


const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

/**
 * 右键窗口控制条自证。四条各打一层,合成事件证不了原生输入那一层:
 * ① 前置断言(菜单本来关着)② 合成 contextmenu → 是否出现 + 落点/盒尺寸/G2
 * ③ 外部 pointerdown 与 Escape 两条关闭路径 ④ sendInputEvent 打真实右键,
 *    单独判"顶部 drag 区到底吞不吞右键"——这决定这条功能在真窗里到底能不能用。
 */
const MENU_STATE = `(() => {
  const m = document.querySelector('.win-menu');
  if (!m) return { open: false };
  const r = m.getBoundingClientRect();
  const cs = getComputedStyle(m);
  return {
    open: true, x: Math.round(r.x), y: Math.round(r.y),
    w: Math.round(r.width), h: Math.round(r.height),
    btns: m.querySelectorAll('.win-menu-btn').length,
    pos: cs.position, region: cs.getPropertyValue('-webkit-app-region'),
    radius: cs.borderRadius, shape: cs.cornerShape,
    btnRadius: getComputedStyle(m.querySelector('.win-menu-btn')).borderRadius,
    btnShape: getComputedStyle(m.querySelector('.win-menu-btn')).cornerShape
  };
})()`;

const SYNTH_CTX = (x: number, y: number) =>
  `(() => {
    const h = document.querySelector('.chat-header');
    h.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: ${x}, clientY: ${y}, button: 2 }));
    return h.className;
  })()`;

const CLICK_ELSEWHERE = `(() => {
  document.querySelector('.sidebar').dispatchEvent(new PointerEvent('pointerdown', { bubbles: true }));
  return 'sent';
})()`;

const PRESS_ESC = `(() => {
  window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
  return 'sent';
})()`;

async function runWinMenuProbe(win: BrowserWindow): Promise<void> {
  const wc = win.webContents;
  const read = () => wc.executeJavaScript(MENU_STATE) as Promise<Record<string, unknown>>;
  const out: Record<string, unknown> = {};
  // 顺带在真窗里量布局三挡之一:浏览器预览的视口只有 1234 CSS px,撑不出 1280 那一挡
  out.layout = await wc.executeJavaScript(`(() => {
    const R = (s) => document.querySelector(s).getBoundingClientRect();
    const w = R('.window'), ca = R('.chat-area'), pill = R('.header-pill'), acts = R('.header-actions');
    const sb = R('.sidebar'), rp = R('.right-panel');
    return {
      winW: +w.width.toFixed(1), chatW: +ca.width.toFixed(1),
      pillActionsGap: +(acts.left - pill.right).toFixed(1),
      inputW: +R('.draft-box').width.toFixed(1),
      innerW: window.innerWidth,
      heights: { side: +sb.height.toFixed(1), chat: +ca.height.toFixed(1), right: +rp.height.toFixed(1) },
      topInsets: { side: +(sb.top - w.top).toFixed(1), chat: +(ca.top - w.top).toFixed(1), right: +(rp.top - w.top).toFixed(1) }
    };
  })()`);
  out.idle = await read();
  out.headerClass = await wc.executeJavaScript(SYNTH_CTX(640, 40));
  await sleep(300); // 菜单入场是 AnimatedContent 的 160ms scale/translate,读早了盒会小一圈(115×42)
  out.synthetic = await read();
  out.afterOutsidePointerdown = await (wc.executeJavaScript(CLICK_ELSEWHERE) as Promise<unknown>).then(read);
  await wc.executeJavaScript(SYNTH_CTX(640, 40));
  out.afterEsc = await (wc.executeJavaScript(PRESS_ESC) as Promise<unknown>).then(read);
  // clamp 取证:用越界光标位开一次,读回的位置应被拉回窗口内
  await wc.executeJavaScript(SYNTH_CTX(99999, 99999));
  out.clamped = await read();
  out.inner = await wc.executeJavaScript('({ w: window.innerWidth, h: window.innerHeight })');
  await wc.executeJavaScript(CLICK_ELSEWHERE);
  out.beforeRealClick = await read();
  // 兜底通路的取证:主进程 context-menu 是否收到(拖拽区若吞事件,这里也不会有)
  let nativeHits = 0;
  const onCtx = (_e: unknown, params: { x: number; y: number }) => {
    nativeHits += 1;
    out.contextMenuEvent = { x: params.x, y: params.y };
  };
  wc.on('context-menu', onCtx);
  // 真实输入:坐标是相对 web 视图的 DIP。取"拖拽带内"的一点 —— 头部那颗 .header-pill 是
  // no-drag,写死的 (700,36) 正落在它上面,量到的"不出菜单"是正确答案但不验功能。
  const rp = (await wc.executeJavaScript(
    `(() => { const el = document.querySelector('.chat-header'); const r = el.getBoundingClientRect();
       const y = Math.round(r.top + r.height / 2);
       for (let x = Math.ceil(r.left + 4); x < r.right - 4; x += 4) { const h = document.elementFromPoint(x, y);
         if (h && h.closest('.drag-region') && !h.closest('.no-drag')) return { x, y }; }
       return null; })()`)) as { x: number; y: number } | null;
  out.realPoint = rp;
  if (rp) {
    wc.sendInputEvent({ type: 'mouseMove', x: rp.x, y: rp.y });
    wc.sendInputEvent({ type: 'mouseDown', x: rp.x, y: rp.y, button: 'right', clickCount: 1 });
    wc.sendInputEvent({ type: 'mouseUp', x: rp.x, y: rp.y, button: 'right', clickCount: 1 });
  }
  await sleep(320);
  out.realRightClick = await read();
  out.nativeHits = nativeHits;
  wc.removeListener('context-menu', onCtx);
  if (WINMENU_SHOT) {
    // 此刻菜单还开着(上一步真实右键留下的),抓一张带浮出条的实景
    const image = await wc.capturePage();
    writeFileSync(WINMENU_SHOT, image.toPNG());
    out.shot = WINMENU_SHOT + ' ' + JSON.stringify(image.getSize());
  }
  // 浮出条自己也得能吃左键:它在 no-drag 的 portal 里,但 drag 区那一层的问题在这一步才见分晓
  const btn = (await wc.executeJavaScript(`(() => {
    const b = document.querySelectorAll('.win-menu-btn')[1];
    if (!b) return null;
    const r = b.getBoundingClientRect();
    return { x: Math.round(r.x + r.width / 2), y: Math.round(r.y + r.height / 2), n: document.querySelectorAll('.win-menu-btn').length };
  })()`)) as { x: number; y: number; n: number } | null;
  out.maxBtn = btn;
  if (btn) {
    out.maxBefore = win.isMaximized();
    wc.sendInputEvent({ type: 'mouseMove', x: btn.x, y: btn.y });
    wc.sendInputEvent({ type: 'mouseDown', x: btn.x, y: btn.y, button: 'left', clickCount: 1 });
    wc.sendInputEvent({ type: 'mouseUp', x: btn.x, y: btn.y, button: 'left', clickCount: 1 });
    await sleep(600);
    out.maxAfter = win.isMaximized();
    out.menuAfterBtnClick = await read();
  }
  console.log('winmenu ' + JSON.stringify(out));
}


/**
 * 「回到最新消息」判据的自证:出现条件是"末条消息底边越过输入区顶边",
 * 而用户报的第二件事是"拉高窗口后钮一直不走"——那一幕往往一个 scroll 事件都不派发
 * (没有可滚的量了),所以判据必须挂在尺寸上重算。这里就把窗口压矮→上划→再拉高,
 * 每一步同时读"几何真值 covered"和"DOM 里到底有没有那颗钮 dom",两者必须一致。
 */
const JUMP_STATE = `(() => {
  const list = document.querySelector('.message-list');
  const last = list.lastElementChild;
  const bar = document.querySelector('.composer-wrap');
  const lb = last.getBoundingClientRect().bottom;
  const bt = bar.getBoundingClientRect().top;
  return { cardH: Math.round(document.querySelector('.chat-area').getBoundingClientRect().height),
           top: Math.round(list.scrollTop), max: Math.round(list.scrollHeight - list.clientHeight),
           gap: Math.round(bt - lb), covered: lb > bt, dom: !!document.querySelector('.jump-bottom') };
})()`;

async function runResizeProbe(win: BrowserWindow): Promise<void> {
  const wc = win.webContents;
  const read = () => wc.executeJavaScript(JUMP_STATE) as Promise<Record<string, unknown>>;
  const out: Record<string, unknown> = {};
  out.initial = await read();
  win.setContentSize(1280, 600);
  await sleep(600);
  out.afterShrink = await read();
  await wc.executeJavaScript(`(() => { document.querySelector('.message-list').scrollTop = 0; return 1; })()`);
  await sleep(300);
  out.scrolledUp = await read();
  // 先趁它还可见时点掉(滚到底 + 自我摘除),再拉高窗口看第二件事
  await wc.executeJavaScript(`(() => { const b = document.querySelector('.jump-bottom'); if (b) b.click(); return 1; })()`);
  await sleep(300);
  out.afterClick = await read();
  win.setContentSize(1280, 800);
  await sleep(600);
  out.afterGrow = await read();
  // 再滚上去一次,拉高时它正该在屏上 —— 这才是"调高页面高度后按钮一直存在"那一幕
  out.shrunkAgain = await (async () => {
    win.setContentSize(1280, 600);
    await sleep(600);
    await wc.executeJavaScript(`(() => { document.querySelector('.message-list').scrollTop = 0; return 1; })()`);
    await sleep(300);
    return read();
  })();
  win.setContentSize(1280, 800);
  await sleep(600);
  out.grownWhileShown = await read();
  console.log('resize ' + JSON.stringify(out));
}

/**
 * 快捷短语三件事的自证:折叠 / 点击追加 / 按住 1s 发送 / 右键改名 / 40% 阈值。
 * 按住这条必须走真实指针输入 —— 组件的 onTap(<250ms)、onHold(满 holdTime)、
 * 提前松手三条分支全靠按下与抬起之间的时间差判定,合成 .click() 一律测不出来。
 * 所以每一步都记"按下→抬起"的实测时长,判据才有对照意义。
 */
const PHRASE_STATE = `(() => {
  const head = document.querySelector('.section-head.collapsible');
  const chips = document.querySelector('.phrase-chips');
  const panel = document.querySelector('.right-panel').getBoundingClientRect();
  const sec = document.querySelector('.phrase-section').getBoundingClientRect();
  const hb = chips ? chips.querySelector('.hold-button') : null;
  const edit = document.querySelector('.phrase-input');
  return {
    expanded: head ? head.getAttribute('aria-expanded') : null,
    chevronTransform: head ? getComputedStyle(head.querySelector('.chevron')).transform : null,
    chipsShown: !!chips,
    // 只读 idle 那层:按钮的 textContent 会把 done 标签和 .hold-button__sr 提示语一起带进来
    labels: chips ? [...chips.querySelectorAll('.hold-button__idle')].map((b) => b.textContent.trim()) : [],
    chipH: hb ? Math.round(hb.getBoundingClientRect().height) : 0,
    chipPhase: hb ? hb.getAttribute('data-phase') : null,
    sectionPct: +(100 * sec.height / panel.height).toFixed(1),
    // 上限到底解析成多少、以及是谁在限它:cqh 没有尺寸容器时会退成 none
    cap: (() => { const cs = getComputedStyle(document.querySelector('.phrase-section')); return { maxHeight: cs.maxHeight, flex: cs.flex, h: Math.round(document.querySelector('.phrase-section').getBoundingClientRect().height) }; })(),
    chipsScroll: chips ? { client: chips.clientHeight, content: chips.scrollHeight } : null,
    draft: document.querySelector('.draft-box').textContent,
    msgs: document.querySelectorAll('.msg-row').length,
    // 量整行而不是内层:气泡里可能是 .text,也可能是种子那种 .bigemoji,读数不该跟着类名跑
    lastMsg: document.querySelector('.msg-row:last-child')?.textContent ?? null,
    gridH: Math.round(document.querySelector('.emoji-grid').getBoundingClientRect().height),
    editing: !!edit,
    editValue: edit ? edit.value : null
  };
})()`;

const CENTER = (sel: string) =>
  `(() => { const el = document.querySelector('${sel}'); const r = el.getBoundingClientRect();
     return { x: Math.round(r.x + r.width / 2), y: Math.round(r.y + r.height / 2),
              hit: (() => { const h = document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2); return h ? (h.className || h.tagName).toString().slice(0, 40) : null; })() }; })()`;

async function runPhraseProbe(win: BrowserWindow): Promise<void> {
  const wc = win.webContents;
  const read = () => wc.executeJavaScript(PHRASE_STATE) as Promise<Record<string, unknown>>;
  const out: Record<string, unknown> = {};

  /** 每次按下前重新量中心:折叠会让上方网格长高、整节位置会漂,拿旧坐标会点到别处 */
  const press = async (sel: string, holdMs: number) => {
    const c = (await wc.executeJavaScript(CENTER(sel))) as { x: number; y: number; hit: string };
    const t0 = Date.now();
    wc.sendInputEvent({ type: 'mouseMove', x: c.x, y: c.y });
    wc.sendInputEvent({ type: 'mouseDown', x: c.x, y: c.y, button: 'left', clickCount: 1 });
    await sleep(holdMs);
    wc.sendInputEvent({ type: 'mouseUp', x: c.x, y: c.y, button: 'left', clickCount: 1 });
    await sleep(120);
    return { at: [c.x, c.y], hit: c.hit, heldMs: Date.now() - t0 };
  };
  const rightClick = async (sel: string) => {
    const c = (await wc.executeJavaScript(CENTER(sel))) as { x: number; y: number };
    wc.sendInputEvent({ type: 'mouseMove', x: c.x, y: c.y });
    wc.sendInputEvent({ type: 'mouseDown', x: c.x, y: c.y, button: 'right', clickCount: 1 });
    wc.sendInputEvent({ type: 'mouseUp', x: c.x, y: c.y, button: 'right', clickCount: 1 });
    await sleep(150);
    return [c.x, c.y];
  };

  out.before = await read();

  // ① 点击(短按 <250ms)→ 追加进输入框
  const CHIP = '.phrase-chips .hold-button';
  out.tap = await press(CHIP, 60);
  out.afterTap = await read();

  // ② 按住 1s → 直接发送
  out.hold = await press(CHIP, 1300);
  out.afterHold = await read();

  // ③ 提前松手(400ms,既不算点击也没满 1s)→ 两件事都不该发生
  const msgsBefore = (out.afterHold as { msgs: number }).msgs;
  const draftBefore = (out.afterHold as { draft: string }).draft;
  out.early = await press(CHIP, 400);
  const afterEarly = await read();
  out.earlyRelease = {
    msgsSame: afterEarly.msgs === msgsBefore,
    draftSame: afterEarly.draft === draftBefore,
    phase: afterEarly.chipPhase
  };

  // ④ 右键 → 变输入框;改文本 + Enter → 短语改名
  out.rightClickAt = await rightClick(CHIP);
  out.afterRightClick = await read();
  await wc.executeJavaScript(`(() => {
    const el = document.querySelector('.phrase-input');
    const set = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set;
    set.call(el, '666 顶');
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    return 1;
  })()`);
  await sleep(200);
  out.afterRename = await read();

  // ⑤ 折叠 / 展开
  const HEAD = '.section-head.collapsible';
  out.collapse = await press(HEAD, 60);
  out.afterCollapse = await read();
  out.expand = await press(HEAD, 60);
  out.afterExpand = await read();

  // ⑥ 40% 阈值:走真实"添加"路径灌 40 条,再看节的高度占比与列表是否内滚
  await wc.executeJavaScript(
    `(async () => {
      const set = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set;
      const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
      for (let i = 0; i < 40; i++) {
        const add = document.querySelector('.chip.add');
        if (!add) return 'no .chip.add at i=' + i;
        add.click();
        await sleep(30);
        const el = document.querySelector('.phrase-input');
        if (!el) return 'no input at i=' + i;
        set.call(el, '短语' + (i + 1));
        el.dispatchEvent(new Event('input', { bubbles: true }));
        el.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
        await sleep(30);
      }
      return 'ok';
    })()`
  ).then((r) => {
    out.addN = r;
  });
  await sleep(400);
  out.afterFill = await read();
  out.panelH = Math.round((await wc.executeJavaScript(
    `Math.round(document.querySelector('.right-panel').getBoundingClientRect().height)`
  )) as number);
  await wc.capturePage().then((image) => {
    writeFileSync('.cache/phrase-40.png', image.toPNG());
  });
  console.log('phrase ' + JSON.stringify(out));
}

/**
 * 原型图量尺:nativeImage 直接解 JPEG(BGRA),对左卡片做行剖面。
 * 为什么要量原图而不是照记忆改数值——"间距"是相对量,原型里头像带之间的空档
 * 才是判据来源;上一版 64/0 的口径就是当初量出来的,但当时只记了项高、没记带间距。
 */
function runProtoProbe(): void {
  const img = nativeImage.createFromPath('ChatAPP · 原型图.jpg');
  const { width: W, height: H } = img.getSize();
  const bmp = img.getBitmap() as unknown as Buffer; // 44.x 的 typings 把它标成 void,运行时是 BGRA
  const at = (x: number, y: number, k: number) => bmp[(y * W + x) * 4 + k];
  const sat = (x: number, y: number) => {
    const b = at(x, y, 0), g = at(x, y, 1), r = at(x, y, 2);
    return Math.max(r, g, b) - Math.min(r, g, b);
  };
  const lum = (x: number, y: number) => 0.299 * at(x, y, 2) + 0.587 * at(x, y, 1) + 0.114 * at(x, y, 0);
  if (PROTO_BOX) {
    // 原型图是一张 ~1:1 的桌面截图(里面有两个窗口实例),先按给定框量,不猜全局坐标
    const [BX0, BY0, BX1, BY1] = PROTO_BOX;
    const prof: string[] = [];
    for (let y = BY0; y <= BY1; y++) {
      // 原型里的头像是"暗色照片圆盘 + 浅底卡片",不是纯色圆 → 判"非背景"(暗或饱和)而不是只判饱和
      let n = 0;
      for (let x = BX0 + 20; x <= BX0 + 80; x++) if (lum(x, y) < 200 || sat(x, y) > 70) n++;
      prof.push(`${y}:${n}`);
    }
    const bands: { y0: number; y1: number }[] = [];
    let run = -1;
    prof.forEach((s, i) => {
      const n = +s.split(':')[1];
      if (n >= 20 && run < 0) run = BY0 + i;
      if (n < 20 && run >= 0) { bands.push({ y0: run, y1: BY0 + i - 1 }); run = -1; }
    });
    if (run >= 0) bands.push({ y0: run, y1: BY1 });
    const kept = bands.filter((b) => b.y1 - b.y0 > 12);
    const c = kept.map((b) => (b.y0 + b.y1) / 2);
    console.log('proto ' + JSON.stringify({
      box: [BX0, BY0, BX1, BY1],
      // 每行一个字符的粗略柱状图(0..9 = 该行"非背景"像素数 /7 封顶),用来肉眼核对节奏而不是信阈值
      profile: prof.map((s) => Math.min(9, Math.floor(+s.split(':')[1] / 7))).join('').replace(/(.{50})/g, '$1|'),
      bands: kept.map((b) => ({ y0: b.y0, y1: b.y1, h: b.y1 - b.y0 + 1 })),
      pitch: c.slice(1).map((v, i) => +(v - c[i]).toFixed(1)),
      edgeRow: Array.from({ length: Math.round((BX1 - BX0) / 10) }, (_, i) => Math.round(lum(BX0 + i * 10, Math.round((BY0 + BY1) / 2)))),
      edgeCol: Array.from({ length: Math.round((BY1 - BY0) / 10) }, (_, i) => Math.round(lum(Math.round((BX0 + BX1) / 2), BY0 + i * 10)))
    }));
    return;
  }
  // ① 自动定位深紫卡片:整幅图先按"暗像素占比"找列与行,别把截图里的桌面留白当成窗口
  const darkCols: number[] = [];
  for (let x = 0; x < W; x++) {
    let n = 0;
    for (let y = 0; y < H; y += 4) if (lum(x, y) < 110) n++;
    if (n > H / 4 / 3) darkCols.push(x);
  }
  const CL = darkCols[0] ?? 0;
  const CR = darkCols[darkCols.length - 1] ?? 0;
  const darkRows: number[] = [];
  for (let y = 0; y < H; y++) {
    let n = 0;
    for (let x = CL + 10; x < CR - 10; x += 4) if (lum(x, y) < 110) n++;
    if (n > (CR - CL) / 4 / 3) darkRows.push(y);
  }
  const CT = darkRows[0] ?? 0;
  const CB = darkRows[darkRows.length - 1] ?? 0;
  const scale = (CR - CL + 1) / 300; // 卡片在设计里是 300 宽,一切按它归一
  // ② 头像带:卡片左半 x∈[CL+16, CL+110](归一后)逐行数饱和像素
  const x0 = Math.round(CL + 16 * scale), x1 = Math.round(CL + 110 * scale);
  const bands: { y0: number; y1: number; h: number }[] = [];
  let run = -1;
  for (let y = CT; y <= CB; y++) {
    let n = 0;
    for (let x = x0; x <= x1; x++) if (sat(x, y) > 70) n++;
    if (n >= 3 && run < 0) run = y;
    if (n < 3 && run >= 0) { bands.push({ y0: run, y1: y - 1, h: y - run }); run = -1; }
  }
  if (run >= 0) bands.push({ y0: run, y1: CB, h: CB - run });
  const kept = bands.filter((b) => b.h > 10 * scale);
  const centers = kept.map((b) => (b.y0 + b.y1) / 2);
  console.log('proto ' + JSON.stringify({
    W, H, card: { CL, CR, CT, CB, w: CR - CL + 1, h: CB - CT + 1 }, scale: +scale.toFixed(3),
    avatarScanX: [x0, x1],
    bands: kept.map((b) => ({ y0: b.y0, y1: b.y1, h: +(b.h / scale).toFixed(1) })),
    pitch: centers.slice(1).map((c, i) => +((c - centers[i]) / scale).toFixed(1))
  }));
}

/**
 * 中间卡"角"的各方案单独出图(全部在真实界面上:不隐子级、不改内容,只注入那一条声明)。
 * 每项是**完整 CSS 文本**(不是 `.chat-area{}` 的体内片段)——要覆盖 `--chat-veil` 必须写在
 * `.chat-area` 元素上,走 `:root{…}` 会被 `:root[data-chat-theme=…]`(特异度 0,2,0 > 0,1,0)吃掉,
 * 那正是上一轮那档假对照的成因。
 */
const CT: [string, string][] = [
  ['① 现状(veil 换深一档 + 已撤 fade 模糊)', ''],
  ['② 改前(veil 旧值)', '.chat-area{--chat-veil:rgba(255,240,214,.82)}'],
  ['③ 白色 1px 内环(inset 影)', '.chat-area{box-shadow:inset 0 0 0 1px rgba(255,255,255,.55)}'],
  ['④ 白色 1.5px 内环', '.chat-area{box-shadow:inset 0 0 0 1.5px rgba(255,255,255,.7)}'],
  ['⑤ 白色 1px 描边(border)', '.chat-area{border:1px solid rgba(255,255,255,.55)}'],
  ['⑥ 深色 1px 描边(border)', '.chat-area{border:1px solid rgba(30,20,50,.12)}'],
  ['⑦ 三卡同款外浮影', '.chat-area{box-shadow:var(--shadow-float)}'],
  ['⑧ 外浮影 + 白 1px 内环', '.chat-area{box-shadow:var(--shadow-float),inset 0 0 0 1px rgba(255,255,255,.55)}'],
  ['⑨ 四周一圈模糊光晕(无偏移影)', '.chat-area{box-shadow:0 0 24px rgba(30,20,50,.28)}'],
  ['⑩ 深色描边 + 外浮影', '.chat-area{border:1px solid rgba(30,20,50,.12);box-shadow:var(--shadow-float)}'],
  ['⑪ 改前:fade 带 blur(10px) —— 弧外那块糊方角', '.chat-fade{backdrop-filter:blur(10px) !important}'],
  ['⑫ 保留模糊的替代修法:blur + fade 挂 inherit 圆角/曲率', '.chat-fade{backdrop-filter:blur(10px) !important;border-radius:inherit;corner-shape:inherit}'],
];

async function runCornerShot(win: BrowserWindow): Promise<void> {
  const wc = win.webContents;
  const RECT = { x: 302, y: 690, width: 96, height: 104 }; // 收紧到左下角上:弧半径 32,放大才看得出差别
  const ZOOM = 3;
  const picks = CS_PICK ?? CT.map((_, i) => i);
  const url = (f: string) => 'file:///' + path.resolve(f).replace(/\\/g, '/');
  const rows: { label: string; corner: string; wide: string }[] = [];
  for (const i of picks) {
    const [label, css] = CT[i];
    await wc.executeJavaScript(`(() => { ${SHEET_FN} SHEET().textContent = ${JSON.stringify(css)}; return 1; })()`);
    await sleep(320); // 等合成器出帧,否则拍到的是上一条声明还在屏上的那一帧
    const corner = `.cache/ct-${i + 1}-corner.png`;
    const wide = `.cache/ct-${i + 1}-win.png`;
    // 连拍第三张会撞 UNKNOWN: open(Windows 上像是刚写过的文件被扫毒/索引短暂占用),
    // 所以带退避重试,而不是让整条探针在最后一档上断掉。
    const writePng = async (file: string, img: Electron.NativeImage) => {
      for (let k = 0; ; k++) {
        try {
          writeFileSync(file, img.toPNG());
          return;
        } catch (e) {
          if (k >= 4) throw e;
          await sleep(200 * (k + 1));
        }
      }
    };
    await writePng(corner, await wc.capturePage(RECT));
    await writePng(wide, await wc.capturePage());
    rows.push({ label, corner: url(corner), wide: url(wide) });
    console.log(`cornershot-shot ${i + 1} ${JSON.stringify({ corner, wide })}`);
  }
  await wc.executeJavaScript(
    `(() => { document.getElementById('corner-probe')?.remove(); document.getElementById('ct-sheet')?.remove();
       const d = document.createElement('div'); d.id = 'ct-sheet';
       d.style.cssText = 'position:fixed;inset:0;z-index:99999;background:#161022;padding:12px;overflow:hidden;'
         + 'display:grid;grid-auto-flow:column;grid-auto-columns:${RECT.width * ZOOM + 16}px;gap:10px;align-content:start';
       d.innerHTML = ${JSON.stringify(rows)}.map((r) => '<div style="display:flex;flex-direction:column;gap:6px">'
         + '<span style="color:#fff;font:12.5px/1.35 system-ui">' + r.label + '</span>'
         + '<img src="' + r.corner + '" style="width:${RECT.width * ZOOM}px;height:${RECT.height * ZOOM}px;image-rendering:pixelated;border:1px solid #3a3050">'
         + '<img src="' + r.wide + '" style="width:${RECT.width * ZOOM}px;height:${Math.round((RECT.width * ZOOM * 800) / 1280)}px;border:1px solid #3a3050">'
         + '</div>').join('')
         + '<div style="color:#ffd9a0;font:11.5px/1.4 system-ui;grid-column:1/-1">'
         + '每列:上 = 中间卡左下角 96×104 的 1:1 裁图放大 3 倍(角点 324,790、弧半径 32);下 = 整窗 1280×800 缩到 ${RECT.width * ZOOM}px</div>';
       document.body.appendChild(d);
       return { imgs: d.querySelectorAll('img').length }; })()`
  );
  await sleep(700);
  // 断言要放在等待之后:插入瞬间 img.complete 本来就是 false,那不算失败也不算通过
  const ready = await wc.executeJavaScript(
    `(() => { const a = [...document.querySelectorAll('#ct-sheet img')];
       return { imgs: a.length, allLoaded: a.every((im) => im.complete && im.naturalWidth > 0),
                bad: a.filter((im) => !(im.complete && im.naturalWidth > 0)).map((im) => im.src.slice(-18)) }; })()`
  );
  const sheet = await wc.capturePage();
  writeFileSync('.cache/corner-sheet.png', sheet.toPNG());
  console.log('cornershot ' + JSON.stringify({ rect: RECT, zoom: ZOOM, picks: picks.map((p) => p + 1), ready, sheet: '.cache/corner-sheet.png' }));
}

/**
 * 「内侧圆角、外侧糊直角」归因实验:一次只关一样东西,拍同一块 96×104 角部裁图。
 * 判据不是"哪张好看",而是"哪一样关掉之后那块方角消失"——只有差分能分清是
 * fade 的 backdrop-filter、fade 自己的洗色、还是负 margin 溢出的列表。
 */
const LT: [string, string][] = [
  ['00 现状', ''],
  ['01 fade 关 backdrop-filter', '.chat-fade{backdrop-filter:none !important}'],
  ['02 fade 整个不显示', '.chat-fade{display:none !important}'],
  ['03 fade 挂 inherit 圆角', '.chat-fade{border-radius:inherit !important}'],
  ['04 fade inherit 圆角 + inherit corner-shape', '.chat-fade{border-radius:inherit !important;corner-shape:inherit !important}'],
  ['05 fade 加 clip-path 圆角', '.chat-fade{clip-path:inset(0 round 32px) !important}'],
  ['06 fade mask 叠一层圆角(与渐隐求交)', '.chat-fade{mask-composite:intersect;-webkit-mask-composite:source-in;border-radius:32px}'],
  ['07 只圆外沿两角 + corner-shape', '.chat-fade--top{border-radius:var(--r-window) var(--r-window) 0 0 !important;corner-shape:inherit !important}.chat-fade--bottom{border-radius:0 0 var(--r-window) var(--r-window) !important;corner-shape:inherit !important}'],
  // 真底参照:楔形本来就该长这样。BL 的"卡外点"落在侧栏投影里(实测 246.6 vs 纯底 254),
  // 拿它当基准会把干净态误判成有漏 —— 所以判据必须是"和把卡藏掉的那张比"。
  ['08 卡片不显示(真底参照)', '.chat-area{visibility:hidden !important}'],
  ['09 现状复采(稳定性对照)', ''],
];
// 两只角都量:上带贴的是卡片顶边,下带贴的是底边,漏的方式可能不同
const RECTS = [
  { name: 'BL', x: 302, y: 690, width: 96, height: 104 },
  { name: 'TL', x: 302, y: 4, width: 96, height: 104 },
];

async function runLeakProbe(win: BrowserWindow): Promise<void> {
  const wc = win.webContents;
  const files: string[] = [];
  for (const [i, [label, css]] of LT.entries()) {
    const n = String(i).padStart(2, '0');
    await wc.executeJavaScript(`(() => { ${SHEET_FN} SHEET().textContent = ${JSON.stringify(css)}; return 1; })()`);
    await sleep(320);
    for (const r of RECTS) {
      const file = `.cache/lk-${n}-${r.name}.png`;
      writeFileSync(file, (await wc.capturePage(r)).toPNG());
      files.push(file);
    }
    console.log(`leak-shot ${n} ${label}`);
  }
  await wc.executeJavaScript(`(() => { document.getElementById('corner-probe')?.remove(); return 1; })()`);
  console.log('leak ' + JSON.stringify({ rects: RECTS, files }));
}

/**
 * 窄窗时三栏的盒关系。用户口径的三条不变量:
 * ① 中间卡与右卡不得重叠(重叠 = 两张卡"合并"的观感);
 * ② 左右两卡等宽;③ 中间区最小宽 = 侧卡宽的 2 倍。
 * 判据打在**盒与盒之间**而不是各自宽度:右栏转浮层后 `left` 会跑到中间卡右边之内,
 * 只看 width 三档全"正常"。overlap>0 就是缺陷本身;windowOverflow>0 是另一条失败路
 * (内联态挤不动了,整行溢出被 .window 的 overflow:hidden 裁掉)。
 */
const COLS_STATE = `(() => {
  const w = document.querySelector('.window');
  const ca = document.querySelector('.chat-area');
  const sb = document.querySelector('.sidebar');
  const rp = document.querySelector('.right-panel');
  const B = (el) => { const r = el.getBoundingClientRect(); return { left: +r.left.toFixed(1), right: +r.right.toFixed(1), w: +r.width.toFixed(1) }; };
  const wb = B(w), cb = B(ca), sbb = B(sb);
  const rpb = rp ? B(rp) : null;
  const cs = rp ? getComputedStyle(rp) : null;
  const f = (n) => (n === null || n === undefined ? null : +Number(n).toFixed(1));
  return {
    winW: f(wb.w),
    innerW: window.innerWidth,
    overflowX: f(w.scrollWidth - w.clientWidth),
    sideW: f(sbb.w),
    chatW: f(cb.w),
    rightW: rpb ? f(rpb.w) : 'unmounted',
    rightPos: cs ? cs.position : 'unmounted',
    gap: rpb ? f(rpb.left - cb.right) : null,
    overlap: rpb ? f(cb.right - rpb.left) : null,
    pastWinRight: rpb ? f(rpb.right - wb.right) : null,
    chatOverSide: rpb ? +(cb.w / sbb.w).toFixed(3) : null,
    chatMinCS: getComputedStyle(ca).minWidth,
    sideFlexCS: getComputedStyle(sb).flexShrink + '/' + getComputedStyle(sb).flexBasis,
    dataPanel: w.dataset.panel,
    // 用户口径②:左右等宽 —— 直接报差值,别让人拿两个读数自己减
    sideMinusRight: rpb ? +(sbb.w - rpb.w).toFixed(2) : null,
    // 侧栏跟着变窄后的连带面:过滤行会不会换行、消息/输入栏会不会横向溢出、表情格还剩多宽
    overflowSide: f(sb.scrollWidth - sbb.w),
    sideWidest: (() => {
      let best = null;
      for (const el of document.querySelectorAll('.sidebar *')) {
        const r = el.getBoundingClientRect();
        if (r.right > sbb.right + 0.5 && (!best || r.right > best.right)) {
          best = { cls: String(el.className).slice(0, 44), right: +r.right.toFixed(1), w: +r.width.toFixed(1) };
        }
      }
      return best;
    })(),
    overflowChat: f(ca.scrollWidth - cb.w),
    overflowRight: rp ? f(rp.scrollWidth - rpb.w) : null,
    tabs: (() => {
      const t = document.querySelector('.filter-tabs');
      const items = [...document.querySelectorAll('.gooey-nav__item')];
      const tops = new Set(items.map((e) => Math.round(e.getBoundingClientRect().top)));
      const iw = items.map((e) => +e.getBoundingClientRect().width.toFixed(1));
      const nav = document.querySelector('.filter-nav.gooey-nav') || document.querySelector('.gooey-nav');
      const last = items[items.length - 1];
      return {
        h: f(t.getBoundingClientRect().height),
        rows: tops.size,
        itemW: Math.min(...iw),
        // 第 9 轮的不变量:末格要贴住 nav 内沿(改前留 18.2px 空档)
        gapToNav: nav && last ? +(nav.getBoundingClientRect().right - last.getBoundingClientRect().right).toFixed(1) : null
      };
    })(),
    emojiCell: (() => { const c = document.querySelector('.emoji-cell'); return c ? +c.getBoundingClientRect().width.toFixed(1) : null; })(),
    // 表情格自己那一层:.scroll 只写了 overflow-y:auto,按规范 overflow-x 跟着变成 auto,
    // 横向滚动条就浮在这里,量 .right-panel 是量不到的。
    emojiClip: (() => {
      const g = document.querySelector('.emoji-grid');
      return g ? f(g.scrollWidth - g.clientWidth) : null;
    })(),
    composerClip: (() => { const c = document.querySelector('.composer-wrap'); return f(c.scrollWidth - c.clientWidth); })(),
    inputW: (() => { const i = document.querySelector('.draft-box'); return f(i.getBoundingClientRect().width); })(),
    msgClip: (() => { const l = document.querySelector('.message-list'); return f(l.scrollWidth - l.clientWidth); })()
  };
})()`;

async function runColsProbe(win: BrowserWindow): Promise<void> {
  const wc = win.webContents;
  const widths =
    colsWIndex >= 0
      ? process.argv[colsWIndex + 1].split(',').map((s) => parseInt(s, 10)).filter((n) => !Number.isNaN(n))
      : [1280, 1260, 1240, 1200, 1160, 1120, 1100, 1060, 1020, 980, 960];
  const rows: Record<string, unknown>[] = [];
  for (const w of widths) {
    win.setContentSize(w, 800);
    // 重排 + 容器查询求值要跨一帧;capturePage 取的是已呈现帧,不等就读到上一档的盒
    await sleep(420);
    const r = (await wc.executeJavaScript(COLS_STATE)) as Record<string, unknown>;
    rows.push({ wantW: w, ...r });
    console.log('cols ' + JSON.stringify(rows[rows.length - 1]));
    if (COLS_SHOT) {
      const img = await wc.capturePage();
      const file = `.cache/cols-${w}.png`;
      for (let k = 0; k < 4; k++) {
        try {
          writeFileSync(file, img.toPNG());
          break;
        } catch {
          await sleep(200 * (k + 1));
        }
      }
    }
  }
  win.setContentSize(1280, 800);
  console.log('cols-done ' + JSON.stringify({ n: rows.length, widths }));
}

/**
 * 自绘拖拽 + 三项窗口菜单的自证。分两层量:
 * ① 页面层(能全验):整棵树不许再有 -webkit-app-region: drag、两条带各出一个菜单、
 *    .no-drag 子元素上右键**不**出菜单(对照腿)、菜单三项、Escape/外点关闭;
 * ② 主进程层(接线):合成 pointerdown/move/up 要让 drag-start/move/end 各到一次,
 *    且真实光标没动 ⇒ 窗口位置必须一格不差(证明"点一下不会把窗口瞬移");
 *    双击要真的最大化/还原;最大化状态下**单击**标题带不许恢复(过阈值才算起拖)。
 * 拖动的"手感"与"拖到顶部最大化"量不到 —— 那要搬真实光标,探针不能碰用户手上的鼠标。
 */
const DRAG = process.argv.includes('--drag');
const MINANIM = process.argv.includes('--minanim');
const COPY = process.argv.includes('--copy');
// 对照腿:同一进程、同一 Electron,只换窗口样式,用来定位"最小化没有 shell 动画"到底卡在哪一条样式上
const PLAIN = process.argv.includes('--plain');

/**
 * 「面板格子里的表情能不能用真鼠标框住、复制出码字」这一挡。
 * 页面侧能量的都量过了(程序化选区 + 真 Ctrl+C 都拿到了码字),但那条路绕开了 user-select:
 * setBaseAndExtent 不受它约束,真实指针才受。上一轮就是靠程序化选区的 toString() 判绿,
 * 用户一句话就推翻 —— 所以这里只认 sendInputEvent 的按下→拖动→抬起。
 * 四腿读数:
 * ① 拖选后 getSelection() 是那颗码位 ⇒ 格子真能框住
 * ② 拖选后草稿不变 ⇒ 选字那一枪没被当成"点格子入框"(捕获层的 click 吞掉了)
 * ③ Ctrl+C 后主进程读回的剪贴板(先写哨兵,读回不是哨兵才证明是这次复制写的)
 * ④ 短按同一格仍入框 ⇒ ②的对照,防那个吞掉把正常点击一起吞了
 */
const GLYPH_BOX = (sel: string) => `(() => {
  const el = document.querySelector('${sel}');
  if (!el) return null;
  const r = el.getBoundingClientRect();
  return {
    txt: el.textContent,
    box: [Math.round(r.width * 10) / 10, Math.round(r.height * 10) / 10],
    x0: Math.round(r.left + 2), x1: Math.round(r.right - 2), y: Math.round(r.top + r.height / 2),
    sel: getComputedStyle(el).userSelect
  };
})()`;

const COPY_STATE = `(() => {
  const s = getSelection();
  return {
    str: s ? s.toString() : null,
    collapsed: s ? s.isCollapsed : null,
    draft: document.querySelector('.draft-box').textContent,
    msgs: document.querySelectorAll('.msg-row').length
  };
})()`;

async function runCopyProbe(win: BrowserWindow): Promise<void> {
  const wc = win.webContents;
  const out: Record<string, unknown> = {};
  const SENTINEL = 'CLIP-SENTINEL-UNCHANGED';
  const state = () =>
    wc.executeJavaScript(COPY_STATE) as Promise<{ str: string | null; collapsed: boolean | null; draft: string; msgs: number }>;

  const dragAcross = async (sel: string) => {
    const g = (await wc.executeJavaScript(GLYPH_BOX(sel))) as
      | { txt: string; box: number[]; x0: number; x1: number; y: number; sel: string }
      | null;
    if (!g) return { missing: sel };
    wc.sendInputEvent({ type: 'mouseMove', x: g.x0, y: g.y });
    wc.sendInputEvent({ type: 'mouseDown', x: g.x0, y: g.y, button: 'left', clickCount: 1 });
    for (let i = 1; i <= 5; i++) {
      wc.sendInputEvent({ type: 'mouseMove', x: Math.round(g.x0 + ((g.x1 - g.x0) * i) / 5), y: g.y });
      await sleep(16);
    }
    wc.sendInputEvent({ type: 'mouseUp', x: g.x1, y: g.y, button: 'left', clickCount: 1 });
    await sleep(140);
    return g;
  };

  /** Ctrl+C 两种发法都试:读回还是哨兵只能说明"这一挡没量到",不能报成"复制失败"。
   *  Electron 44 的 clipboard.readText/writeText 是异步的,少 await 就是拿 Promise 跟字符串比。 */
  const realCopy = async () => {
    await clipboard.writeText(SENTINEL);
    wc.sendInputEvent({ type: 'keyDown', keyCode: 'C', modifiers: ['control'] });
    wc.sendInputEvent({ type: 'keyUp', keyCode: 'C', modifiers: ['control'] });
    await sleep(160);
    const viaKeyDown = await clipboard.readText();
    if (viaKeyDown !== SENTINEL) return { viaKeyDown };
    wc.sendInputEvent({ type: 'rawKeyDown', keyCode: 'C', modifiers: ['control'] });
    wc.sendInputEvent({ type: 'char', keyCode: 'c', modifiers: ['control'] });
    wc.sendInputEvent({ type: 'keyUp', keyCode: 'C', modifiers: ['control'] });
    await sleep(160);
    return { viaKeyDown, viaRawChar: await clipboard.readText() };
  };

  const tap = async (sel: string, ms: number) => {
    const c = (await wc.executeJavaScript(CENTER(sel))) as { x: number; y: number; hit: string };
    wc.sendInputEvent({ type: 'mouseMove', x: c.x, y: c.y });
    wc.sendInputEvent({ type: 'mouseDown', x: c.x, y: c.y, button: 'left', clickCount: 1 });
    await sleep(ms);
    wc.sendInputEvent({ type: 'mouseUp', x: c.x, y: c.y, button: 'left', clickCount: 1 });
    await sleep(140);
    return c;
  };

  /* 在 document 捕获层记每一次打在格子上的 click:这一层在 React 根容器之前,所以
     "Chrome 到底派发了 click 没有"和"我们的 guard 有没有把它吞掉"是两个分开的读数。
     少了这一格,拖选后草稿不变只能证明"没插入",证不了"不吞就会插入"。 */
  await wc.executeJavaScript(`(() => {
    window.__cellClicks = [];
    document.addEventListener('click', (e) => {
      const c = e.target.closest && e.target.closest('.emoji-cell, [data-epr-part="emoji"]');
      if (!c) return;
      const s = getSelection();
      window.__cellClicks.push({ txt: c.textContent, inCell: !!s && !s.isCollapsed && c.contains(s.anchorNode) && c.contains(s.focusNode) });
    }, true);
    return 1;
  })()`);
  const clicks = () => wc.executeJavaScript(`window.__cellClicks`) as Promise<{ txt: string; inCell: boolean }[]>;

  out.tab = await wc.executeJavaScript(`document.querySelector('.panel-tab.active')?.textContent ?? null`);

  out.bigDrag = await dragAcross('.emoji-cell .ep-glyph');
  out.bigAfterDrag = await state();
  out.bigClip = await realCopy();
  out.bigDragClicks = await clicks();

  // ④ 对照腿分两枪:4a 先清掉选区再短按同一格(应当入框);4b 按另一格(选区不在它里面)
  await wc.executeJavaScript(`getSelection().removeAllRanges()`);
  const draftBeforeTap = (out.bigAfterDrag as { draft: string }).draft;
  out.tapClean = await tap('.emoji-cell', 60);
  out.afterTapClean = await state();
  out.tapCleanInserted = (out.afterTapClean as { draft: string }).draft !== draftBeforeTap;

  out.tapOther = await tap('.emoji-cell:nth-child(3)', 60);
  out.afterTapOther = await state();
  out.tapOtherInserted = (out.afterTapOther as { draft: string }).draft !== (out.afterTapClean as { draft: string }).draft;
  out.allClicks = await clicks();

  // 表情 tab(picker 自绘的 button)跑同一条选区+剪贴板腿
  await wc.executeJavaScript(
    `(() => { const t = [...document.querySelectorAll('.panel-tab')].find((x) => x.textContent === '表情'); t.click(); return t ? 1 : 0; })()`);
  await sleep(500);
  out.pickerDrag = await dragAcross('.epr-emoji .ep-glyph');
  out.pickerAfterDrag = await state();
  out.pickerClip = await realCopy();
  out.pickerDragClicks = await clicks();

  console.log('copy ' + JSON.stringify(out));
}

const DRAG_FN = `(() => {
  window.__bandPoint = (sel) => {
    const el = document.querySelector(sel);
    if (!el) return null;
    const r = el.getBoundingClientRect();
    const y = Math.round(r.top + r.height / 2);
    for (let x = Math.ceil(r.left + 4); x < r.right - 4; x += 4) {
      const hit = document.elementFromPoint(x, y);
      if (hit && hit.closest('.drag-region') && !hit.closest('.no-drag')) return { x, y };
    }
    return null;
  };
  window.__fire = (sel, type, button) => {
    const p = window.__bandPoint(sel);
    if (!p) return 'no-point';
    const el = document.elementFromPoint(p.x, p.y);
    const Ctor = type === 'dblclick' || type === 'contextmenu' ? MouseEvent : PointerEvent;
    el.dispatchEvent(new Ctor(type, {
      bubbles: true, cancelable: true, clientX: p.x, clientY: p.y,
      button: button === undefined ? (type === 'contextmenu' ? 2 : 0) : button,
      pointerId: 1, pointerType: 'mouse', isPrimary: true
    }));
    return p;
  };
  window.__menu = () => {
    const m = document.querySelector('.win-menu');
    if (!m) return null;
    const wrap = m.parentElement;
    const r = m.getBoundingClientRect();
    const cs = getComputedStyle(m);
    const wcs = getComputedStyle(wrap);
    return {
      x: Math.round(r.x), y: Math.round(r.y), w: Math.round(r.width), h: Math.round(r.height),
      btns: m.querySelectorAll('.win-menu-btn').length,
      labels: [...m.querySelectorAll('.win-menu-btn')].map((b) => b.getAttribute('aria-label')).join('/'),
      wrapCls: wrap.className, wrapPos: wcs.position, z: wcs.zIndex,
      bg: cs.backgroundColor, radius: cs.borderRadius, shape: cs.cornerShape,
      opacity: wcs.opacity, visibility: wcs.visibility, transform: wcs.transform
    };
  };
  window.__appRegion = () => {
    const bad = [];
    for (const el of document.querySelectorAll('*')) {
      const v = getComputedStyle(el).getPropertyValue('-webkit-app-region');
      if (v && v !== 'none') bad.push(String(el.className).slice(0, 28) + '=' + v);
    }
    return bad;
  };
  window.__bands = () => [...document.querySelectorAll('.drag-region')].map(
    (el) => el.className + '@' + Math.round(el.getBoundingClientRect().width) + 'x' + Math.round(el.getBoundingClientRect().height));
  return 'ok';
})()`;

async function runDragProbe(win: BrowserWindow): Promise<void> {
  const wc = win.webContents;
  const out: Record<string, unknown> = {};
  await wc.executeJavaScript(DRAG_FN);
  out.appRegion = await wc.executeJavaScript('__appRegion()');
  out.bands = await wc.executeJavaScript('__bands()');

  /* ---- 菜单:中间头部带 ---- */
  out.midCtx = await wc.executeJavaScript(`__fire('.chat-header','contextmenu')`);
  await sleep(300); // AnimatedContent 入场 160ms,读早了 transform/opacity 还是起始值
  out.menuMid = await wc.executeJavaScript('__menu()');
  out.menuMidEscape = await (wc.executeJavaScript(`(() => {
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    return 1;
  })()`) as Promise<unknown>).then(() => wc.executeJavaScript('__menu()'));

  /* ---- 菜单:左栏顶部带(用户截图那一枪的位置) ---- */
  out.sideCtx = await wc.executeJavaScript(`__fire('.sidebar-top','contextmenu')`);
  await sleep(300);
  out.menuSide = await wc.executeJavaScript('__menu()');

  /* ---- 对照腿:先关掉菜单,再右键打在 .no-drag 子元素上 ⇒ 不该出菜单 ---- */
  const closeMenu = () =>
    wc.executeJavaScript(`(() => { document.body.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerId: 1, clientX: 600, clientY: 400 })); return 1; })()`);
  await closeMenu();
  out.noDragControl = await (wc.executeJavaScript(`(() => {
    const el = document.querySelector('.header-pill');
    const r = el.getBoundingClientRect();
    el.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: Math.round(r.left + 6), clientY: Math.round(r.top + 6), button: 2 }));
    return 'fired';
  })()`) as Promise<unknown>).then(() => sleep(300)).then(() => wc.executeJavaScript('__menu()'));
  await closeMenu();

  /* ---- 拖拽接线:合成一遍,真实光标不动 ⇒ 窗口必须原地 ---- */
  const posBefore = win.getPosition();
  const logBefore = { ...dragLog };
  out.dragSeq = await wc.executeJavaScript(`(async () => {
    const a = __fire('.chat-header','pointerdown');
    await new Promise((r) => setTimeout(r, 60));
    const b = __fire('.chat-header','pointermove');
    __fire('.chat-header','pointermove');
    __fire('.chat-header','pointermove');
    const c = __fire('.chat-header','pointerup');
    return { down: a, move: b, up: c };
  })()`);
  await sleep(200);
  out.dragSeqLog = { start: dragLog.start - logBefore.start, move: dragLog.move - logBefore.move, end: dragLog.end - logBefore.end };
  const posAfter = win.getPosition();
  out.posAfterDrag = posAfter;
  // 只能当信息看:公式读的是**真实光标**,用户手一抖窗口就跟一段 ⇒ 它不是判据。
  // "光标不动 ⇒ 窗口不动"这条性质由下面 dragMath.still 定量验。
  out.posDeltaCursorDrift = [posAfter[0] - posBefore[0], posAfter[1] - posBefore[1]];

  /* ---- 位移公式本身:拿合成数字验(真实光标不能替用户搬,所以这一格只能这么量) ---- */
  const A: DragAnchor = { wx: 100, wy: 200, cx: 500, cy: 300, moved: false, hitTop: false, wasMax: false };
  out.dragMath = {
    still: dragTarget(A, 500, 300, 0),          // 光标没动 ⇒ 窗口一格不动
    right: dragTarget(A, 560, 340, 0),          // 光标 +60/+40 ⇒ 窗口 +60/+40
    topClamp: dragTarget(A, 500, 40, 20),       // 拖过工作区顶 ⇒ y 卡在 20 并报 hitTop
    leftUp: dragTarget(A, 400, 260, 0)          // 反向位移
  };

  /* ---- 真实输入通路:sendInputEvent 的右键(等价于用户那一枪),看命中链与菜单。
     坐标要先问页面要"带内的一点" —— 头部那颗 .header-pill 是 no-drag,
     写死 (700,36) 正好打在它上面,量到的是"不出菜单"这种正确答案,却不像在验功能。 */
  const logBefore3 = { ...dragLog };
  const band = (await wc.executeJavaScript(`__bandPoint('.chat-header')`)) as { x: number; y: number } | null;
  out.realBandPoint = band;
  let hits = 0;
  const onCtxReal = () => hits++;
  if (band) {
    wc.sendInputEvent({ type: 'mouseMove', x: band.x, y: band.y });
    await sleep(120);
    out.hitChain = await wc.executeJavaScript(
      `(() => { const chain = []; for (let el = document.elementFromPoint(${band.x}, ${band.y}); el && chain.length < 4; el = el.parentElement) chain.push(el.tagName + '.' + String(el.className).slice(0, 26)); return chain; })()`);
    wc.on('context-menu', onCtxReal);
    wc.sendInputEvent({ type: 'mouseDown', x: band.x, y: band.y, button: 'right', clickCount: 1 });
    wc.sendInputEvent({ type: 'mouseUp', x: band.x, y: band.y, button: 'right', clickCount: 1 });
    await sleep(320);
    out.menuReal = await wc.executeJavaScript('__menu()');
    out.nativeHitsReal = hits;
    wc.removeListener('context-menu', onCtxReal);
    await closeMenu();
    out.menuAfterCloseReal = await wc.executeJavaScript('__menu()');
  }
  out.dragRealLog = { start: dragLog.start - logBefore3.start, move: dragLog.move - logBefore3.move, end: dragLog.end - logBefore3.end };

  /* ---- 双击最大化 / 再双击还原 ---- */
  await wc.executeJavaScript(`__fire('.chat-header','dblclick')`);
  await sleep(500);
  out.maxAfterDbl = win.isMaximized();
  await wc.executeJavaScript(`__fire('.chat-header','dblclick')`);
  await sleep(500);
  out.unmaxAfterDbl2 = !win.isMaximized();

  /* ---- 最大化状态下"单击标题带"不许恢复(用户报的那条:双击最大化后单击就掉回原尺寸)。
     恢复只该发生在位移过 SM_CXDRAG 之后,而那一步要真实光标 ⇒ 阈值本身拿合成数字验。 ---- */
  win.maximize();
  await sleep(500);
  out.maxBeforeClick = win.isMaximized();
  const logBefore2 = { ...dragLog };
  await wc.executeJavaScript(`__fire('.chat-header','pointerdown')`);
  await sleep(60);
  out.stillMaxOnPointerdown = win.isMaximized();
  await wc.executeJavaScript(`__fire('.chat-header','pointerup')`);
  await sleep(60);
  out.stillMaxAfterClick = win.isMaximized();
  out.clickLog = { start: dragLog.start - logBefore2.start, move: dragLog.move - logBefore2.move, end: dragLog.end - logBefore2.end };
  const S: DragAnchor = { wx: 0, wy: 0, cx: 500, cy: 300, moved: false, hitTop: false, wasMax: true };
  out.slop = {
    jitter2: pastDragSlop(S, 502, 301),   // 手抖 2px ⇒ 仍算点击
    edge4: pastDragSlop(S, 504, 300),     // 到阈值 ⇒ 起拖(会还原)
    down6: pastDragSlop(S, 500, 306),     // 竖向同理
    negative: pastDragSlop(S, 494, 300)   // 反向同理
  };
  // 收尾:确实在最大化管理下退出,别让窗口停在最大化态影响后面的读数
  if (win.isMaximized()) win.unmaximize();
  win.setBounds({ x: 60, y: 60, width: 1280, height: 800 });
  await sleep(400);
  out.finalBounds = win.getBounds();

  console.log('drag ' + JSON.stringify({ ...out, log: dragLog }));
}

/**
 * 自绘最小化退场的自证。判据要打在"曲线确实在动"这一层:
 * 只看最终态(最小化成功/恢复成功)量不出动画有没有播、有没有掉帧。
 * 退场侧采一段 rAF:scale 要从 1 单调降到 0.6、opacity 从 1 到 0,最大帧距就是掉帧读数。
 * 恢复侧也采一段,但它现在**应当**全程停在 s=1/o=1 —— 恢复刻意不走过渡(实测 restore 后头
 * ~160ms 渲染器不出帧,那段入场有没有被呈现证明不了),所以这一腿是"没有多余动画"的对照。
 */
const MINANIM_SAMPLER = (bucket: string) => `(() => {
  const el = document.querySelector('.window');
  window.__${bucket} = [];
  const t0 = performance.now();
  let last = t0;
  const tick = () => {
    const cs = getComputedStyle(el);
    const now = performance.now();
    window.__${bucket}.push({
      dt: Math.round(now - t0),
      gap: Math.round(now - last),
      s: +new DOMMatrixReadOnly(cs.transform).a.toFixed(4),
      o: +parseFloat(cs.opacity).toFixed(3)
    });
    last = now;
    if (now - t0 < 200) requestAnimationFrame(tick);
  };
  requestAnimationFrame(tick);
  return el.className + '|' + getComputedStyle(el).transitionDuration;
})()`;

async function runMinAnimProbe(win: BrowserWindow): Promise<void> {
  const wc = win.webContents;
  const out: Record<string, unknown> = {};
  const el = () => wc.executeJavaScript(`(() => {
    const e = document.querySelector('.window'); const cs = getComputedStyle(e);
    return { cls: e.className, transform: cs.transform, opacity: cs.opacity, dur: cs.transitionDuration, origin: cs.transformOrigin };
  })()`);
  out.before = await el();

  await wc.executeJavaScript(DRAG_FN);
  await wc.executeJavaScript(
    `(() => { const s = document.createElement('style'); s.id = 'minanim-ab'; document.head.appendChild(s); return 1; })()`);

  /**
   * 一腿 = 注入对照样式 → 开控制条 → 起 rAF 采样 → 点最小化,同时用 capturePage 抓一小块
   * 反复取图、按位图哈希数"真的换了内容的帧"。rAF 那条只反映主线程回调节奏(getComputedStyle
   * 的插值值是按时钟算的,合成器漏帧它照样报新值 —— 上一轮我把这个数当成帧距报出去了,是错的),
   * presented 那条才是呈现面。两条腿共用同一个仪表,所以绝对值即便被 capturePage 自己的带宽
   * 压住,腿与腿之间仍然可比。
   */
  const leg = async (bucket: string, abCss: string) => {
    await wc.executeJavaScript(`document.getElementById('minanim-ab').textContent = ${JSON.stringify(abCss)};`);
    await wc.executeJavaScript(`__fire('.chat-header','contextmenu')`);
    await sleep(340);
    const btns = await wc.executeJavaScript(`document.querySelectorAll('.win-menu-btn').length`);
    await wc.executeJavaScript(MINANIM_SAMPLER(bucket));
    await wc.executeJavaScript(`document.querySelector('.win-menu-btn').click()`);
    const origin = await wc.executeJavaScript(
      `getComputedStyle(document.querySelector('.window')).transformOrigin`);
    const seen = new Set<string>();
    let calls = 0;
    const t0 = Date.now();
    while (Date.now() - t0 < 240) {
      const img = await wc.capturePage({ x: 470, y: 300, width: 160, height: 160 });
      calls++;
      seen.add(createHash('sha1').update(img.toBitmap()).digest('hex'));
    }
    await sleep(400);
    const minimized = win.isMinimized();
    const samples = (await wc.executeJavaScript(`window.__${bucket} || []`)) as {
      dt: number; gap: number; s: number; o: number;
    }[];
    win.restore();
    await sleep(450);
    const gaps = samples.map((x) => x.gap).sort((a, b) => a - b);
    const pct = (p: number) => (gaps.length ? gaps[Math.min(gaps.length - 1, Math.floor((p / 100) * gaps.length))] : null);
    // 曲线读数:看位移在时间上铺得均不均(强减速会把大部分行程挤进头几十毫秒,看着像掉帧)
    const at = (ms: number) => {
      const s = samples.find((x) => x.dt >= ms);
      return s ? [s.dt, s.s, s.o] : null;
    };
    return {
      btns,
      origin,
      minimized,
      presented: { calls, distinct: seen.size },
      curve: [at(20), at(60), at(100), at(140), at(180)],
      raf: {
        n: samples.length,
        p50: pct(50),
        p95: pct(95),
        max: gaps.length ? gaps[gaps.length - 1] : null,
        sMin: samples.length ? Math.min(...samples.map((x) => x.s)) : null,
        oMin: samples.length ? Math.min(...samples.map((x) => x.o)) : null
      }
    };
  };

  // off = 把本轮两条优化撤掉;bd = 在出厂版之上再退掉退场期间的 28px backdrop blur(候选,量了才决定要不要留)
  out.off = await leg('off', '.window--minimizing{will-change:auto !important}.window{transition-timing-function:var(--ease) !important}');
  out.on = await leg('on', '');
  out.bd = await leg('bd', '.window--minimizing{backdrop-filter:none !important}');
  out.after = await el();
  console.log('minanim ' + JSON.stringify(out));
}

function createWindow(): BrowserWindow {
  const win = new BrowserWindow({
    title: APP_TITLE,
    width: 1280,
    height: 800,
    // 1248 = 三栏按原型定宽跑满的最小窗口宽:2*10 窗口内边距 + 2*14 栏间隙 + 2*300 侧栏 + 600 中间区
    // (= 2×300,用户口径"中间最小宽 = 侧栏的 2 倍")。再窄就要动栏宽比例,所以直接钉住下限。
    minWidth: 1248,
    minHeight: 600,
    frame: false,
    transparent: true,
    backgroundColor: '#00000000',
    show: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  });

  win.on('page-title-updated', (e) => e.preventDefault());
  // 自绘退场留下的类要在恢复之后才摘,反向的 transition 才是"从任务栏长回来"那段
  win.on('restore', () => win.webContents.send('window:restored'));
  win.once('ready-to-show', () => {
    // 内核版本要打进日志:G2 圆角用 corner-shape 还是 JS 生成 path(),取决于这个数
    console.log(`ready-to-show chromium=${process.versions.chrome} node=${process.versions.node}`);
    win.show();
    if (SHOT_PATH) {
      // Let the WebGL background paint a few frames before grabbing the surface.
      setTimeout(() => {
        void (async () => {
          const rect = CROP
            ? { x: CROP[0], y: CROP[1], width: CROP[2], height: CROP[3] }
            : undefined;
          let prep: unknown = null;
          if (SHOT_HEIGHT) {
            win.setContentSize(1280, SHOT_HEIGHT);
            await sleep(500); // 等重排,否则下面读到的盒坐标还是旧高度那一套
          }
          if (HOVER) {
            // 指针停在 x,y 不动,让 :hover 生效后再采(撤掉 BorderGlow 后判"只剩图标自己的悬浮底")
            win.webContents.sendInputEvent({ type: 'mouseMove', x: HOVER[0], y: HOVER[1] });
            await sleep(400);
          }
          if (!Number.isNaN(SCROLL_TOP)) {
            prep = await win.webContents.executeJavaScript(
              `(async () => {
                 const el = document.querySelector('.message-list');
                 el.scrollTop = ${SCROLL_TOP};
                 const comp = document.querySelector('.composer').getBoundingClientRect();
                 // 钮是 React 收到 scroll 事件后才挂的:轮询等它出现,拿到"刚出现那一帧"的盒
                 const jb = await new Promise((res) => {
                   const t0 = performance.now();
                   const poll = () => {
                     const n = document.querySelector('.jump-bottom');
                     if (n) return res(n);
                     if (performance.now() - t0 > 2000) return res(null);
                     requestAnimationFrame(poll);
                   };
                   poll();
                 });
                 const box = (n) => { if (!n) return null; const r = n.getBoundingClientRect(); return { x: +r.x.toFixed(1), y: +r.y.toFixed(1), w: +r.width.toFixed(1), h: +r.height.toFixed(1) }; };
                 const topBefore = el.scrollTop;
                 const maxBefore = el.scrollHeight - el.clientHeight;
                 const first = box(jb);
                 const anim = jb ? getComputedStyle(jb).animationName : null;
                 await new Promise((r) => setTimeout(r, 600));
                 const settled = box(jb);
                 const card = document.querySelector('.chat-area').getBoundingClientRect();
                 // 点它一下:必须真的滚到底,并且把自己摘掉(滚到底 = 离底 0 < 24)
                 let afterClick = null;
                 if (jb) {
                   jb.click();
                   await new Promise((r) => setTimeout(r, 300));
                   afterClick = { top: el.scrollTop, max: el.scrollHeight - el.clientHeight, jump: !!document.querySelector('.jump-bottom') };
                 }
                 return { want: ${SCROLL_TOP}, top: topBefore, max: maxBefore,
                          comp: { h: +comp.height.toFixed(1), w: +comp.width.toFixed(1), x: +comp.x.toFixed(1), y: +comp.y.toFixed(1) },
                          chatCenter: +((card.left + card.right) / 2).toFixed(1),
                          jump: !!jb, animName: anim, jumpFirst: first, jumpSettled: settled,
                          moved: first && settled ? +(settled.x - first.x).toFixed(1) : null,
                          afterClick };
               })()`
            );
            console.log('scroll ' + JSON.stringify(prep));
          }
          const image = await win.webContents.capturePage(rect);
          writeFileSync(SHOT_PATH, image.toPNG());
          const size = image.getSize();
          console.log(`shot-saved ${SHOT_PATH} ${size.width}x${size.height}`);
        })()
          .catch((err: unknown) => {
            console.log('shot-failed ' + String(err));
          })
          .finally(() => app.quit());
      }, 1600);
    }
    if (FOLD_ROW >= 0) {
      setTimeout(() => {
        void win.webContents
          .executeJavaScript(foldProbe(FOLD_ROW))
          .then((r) => console.log('fold ' + JSON.stringify(r)))
          .catch((err: unknown) => console.log('fold-failed ' + String(err)))
          .finally(() => app.quit());
      }, 1200);
    }
    if (PHRASE) {
      setTimeout(() => {
        void runPhraseProbe(win)
          .catch((err: unknown) => console.log('phrase-failed ' + String(err)))
          .finally(() => app.quit());
      }, 1500);
    }
    if (RESIZE) {
      setTimeout(() => {
        void runResizeProbe(win)
          .catch((err: unknown) => console.log('resize-failed ' + String(err)))
          .finally(() => app.quit());
      }, 1500);
    }
    if (LEAK) {
      setTimeout(() => {
        void runLeakProbe(win)
          .catch((err: unknown) => console.log('leak-failed ' + String(err)))
          .finally(() => app.quit());
      }, 1500);
    }
    if (COLS) {
      setTimeout(() => {
        void runColsProbe(win)
          .catch((err: unknown) => console.log('cols-failed ' + String(err)))
          .finally(() => app.quit());
      }, 1500);
    }
    if (DRAG) {
      setTimeout(() => {
        void runDragProbe(win)
          .catch((err: unknown) => console.log('drag-failed ' + String(err)))
          .finally(() => app.quit());
      }, 1500);
    }
    if (COPY) {
      setTimeout(() => {
        void runCopyProbe(win)
          .catch((err: unknown) => console.log('copy-failed ' + String(err)))
          .finally(() => app.quit());
      }, 1500);
    }
    if (MINANIM) {
      setTimeout(() => {
        void runMinAnimProbe(win)
          .catch((err: unknown) => console.log('minanim-failed ' + String(err)))
          .finally(() => app.quit());
      }, 1500);
    }
    if (CORNERSHOT) {
      setTimeout(() => {
        void runCornerShot(win)
          .catch((err: unknown) => console.log('cornershot-failed ' + String(err)))
          .finally(() => app.quit());
      }, 1500);
    }
    if (CORNER) {
      setTimeout(() => {
        void runCornerProbe(win)
          .catch((err: unknown) => console.log('corner-failed ' + String(err)))
          .finally(() => app.quit());
      }, 1500);
    }
    if (ROWS) {
      setTimeout(() => {
        void runRowsProbe(win)
          .catch((err: unknown) => console.log('rows-failed ' + String(err)))
          .finally(() => app.quit());
      }, 1500);
    }
    if (TABS) {
      setTimeout(() => {
        void runTabsProbe(win)
          .catch((err: unknown) => console.log('tabs-failed ' + String(err)))
          .finally(() => app.quit());
      }, 1500);
    }
    if (GN_ROW >= 0) {
      setTimeout(() => {
        void runGnProbe(win, GN_ROW, GN_BASE)
          .catch((err: unknown) => console.log('gn-failed ' + String(err)))
          .finally(() => app.quit());
      }, 1500);
    }
    if (WINMENU) {
      setTimeout(() => {
        void runWinMenuProbe(win)
          .catch((err: unknown) => console.log('winmenu-failed ' + String(err)))
          .finally(() => app.quit());
      }, 1200);
    }
    if (FRAMES_MS) {      // 等 SoftAurora 起来再采:头 1.2s 是 WebGL 上下文建立与首帧编译,不算稳态
      setTimeout(() => {
        void win.webContents
          .executeJavaScript(frameProbe(FRAMES_MS))
          .then((r) => console.log('frames ' + JSON.stringify(r)))
          .catch((err: unknown) => console.log('frames-failed ' + String(err)))
          .finally(() => app.quit());
      }, 1200);
    }
  });

  const devUrl = process.env.VITE_DEV_SERVER_URL;
  if (devUrl) {
    void win.loadURL(devUrl);
  } else {
    void win.loadFile(path.join(__dirname, '..', 'dist', 'index.html'));
  }

  if (PLAIN) {
    // 三条腿:①本应用(frame:false+transparent)②frame:false 但不透明 ③普通有边框窗口。
    // 只有把"没边框"和"透明"拆开,才能说清 shell 的 minimize 动画卡在哪一条上。
    const mk = (title: string, opts: Electron.BrowserWindowConstructorOptions) =>
      new BrowserWindow({ title, width: 420, height: 300, backgroundColor: '#ffffff', show: true, webPreferences: { sandbox: true }, ...opts });
    const c1 = mk('ChatAPP-NOFRAME-OPAQUE', { x: 120, y: 520, frame: false, transparent: false });
    const c2 = mk('ChatAPP-CONTROL', { x: 580, y: 520, frame: true, transparent: false });
    void c1.loadURL('data:text/html,<h1>noframe opaque</h1>');
    void c2.loadURL('data:text/html,<h1>framed control</h1>');
    console.log('plain-hwnd noframe=' + c1.getNativeWindowHandle().readBigUInt64LE() + ' control=' + c2.getNativeWindowHandle().readBigUInt64LE());
  }
  console.log('main-hwnd ' + win.getNativeWindowHandle().readBigUInt64LE().toString());
  return win;
}

/* ===== 零网络判据（离线 emoji-picker 的验收，来自 offline-emoji-picker skill Step 5）=====
   三条设计各防一种假绿：
   · 同一条钩子也数 file:// —— 钩子没挂上时 remote 同样是 0，本地计数 >0 才证明它在收事件；
   · 远程请求 cancel:true —— 只观测不阻断的话，CDN 真被请求到页面照样显示，判据就成了事后诸葛；
   · --netprobe 反向对照 —— 主动打一次库里那条 CDN 地址，看钩子接不接得住。
   只在 file:// 交付路径上装：开发模式走 http://localhost，会被这道钩子拦死。 */
const NETPROBE = process.argv.includes('--netprobe');
const remoteRequests: string[] = [];
const localFiles = new Map<string, number>();

function installNetworkGuard(): void {
  session.defaultSession.webRequest.onBeforeRequest({ urls: ['<all_urls>'] }, (details, callback) => {
    const scheme = details.url.slice(0, details.url.indexOf(':')).toLowerCase();
    if (scheme === 'file' || scheme === 'data' || scheme === 'blob') {
      if (scheme === 'file') {
        const name = decodeURIComponent(details.url.split('/').pop()!.split('?')[0]);
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
  const listed = [...localFiles.entries()].sort().map(([n, c]) => n + (c > 1 ? 'x' + c : ''));
  console.log(
    `network remote=${remoteRequests.length} local-files=${localFiles.size} [${listed.join(', ')}]` +
      (remoteRequests.length ? ' ' + JSON.stringify(remoteRequests.slice(0, 5)) : '')
  );
}

// 库里硬编码的那条 jsDelivr 模板，取一个真实码位当反向对照
const NETPROBE_JS =
  `fetch('https://cdn.jsdelivr.net/npm/emoji-datasource-apple@16.0.0/img/apple/64/1f600.png', {cache:'no-store'})` +
  ` .then(r => 'RESOLVED status=' + r.status) .catch(e => 'REJECTED ' + String(e && e.message).slice(0, 60))`;

/**
 * 表情面板自证：大表情 tab 每格有没有图 → 切到 表情 tab 量 picker → 用**原生 setter** 驱动搜索
 * （直接 input.value= 会被 React 的 value tracker 当成没变，搜索根本不跑，2026-10-12 踩过）
 * → 把 DOM 里出现过的 atlas 页各真解码一次（DOM 里有 .ep-glyph 只证明 React 挂上了）。
 */
const EMOJI_SELFTEST_JS = `(async () => {
  const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
  const qa = (s) => [...document.querySelectorAll(s)];
  const out = { bigTab: { cells: qa('.emoji-cell').length, sprite: qa('.emoji-cell .ep-glyph').length } };
  try {
    const tab = qa('.panel-tab').find((t) => t.textContent.trim() === '表情');
    if (!tab) throw new Error('找不到「表情」tab，面板里的 tab 是: ' + qa('.panel-tab').map((t) => t.textContent).join('/'));
    tab.click();
    await sleep(500);
    out.picker = {
      cells: qa('.ep-picker [data-epr-part="emoji"]').length,
      catIcons: qa('.ep-picker .ep-cat-icon').length,
      skinTones: qa('.ep-picker [data-epr-part="skin-tone-button"]').length,
      hasSearch: !!document.querySelector('.ep-picker input')
    };
    const input = document.querySelector('.ep-picker input');
    if (!input) throw new Error('picker 里没有搜索框');
    const setv = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
    setv.call(input, '笑脸');
    input.dispatchEvent(new Event('input', { bubbles: true }));
    await sleep(900);
    out.searchZh = {
      // 播报节点在库的 root 之外（aria-live 区），只在 .ep-picker 里找会读成 null
      announced: [...document.querySelectorAll('*')].map((e) => (e.childNodes.length === 1 && e.firstChild && e.firstChild.nodeType === 3 ? e.textContent.trim() : '')).filter((t) => /个结果|没有匹配/.test(t))[0] || null,
      cells: qa('.ep-picker [data-epr-part="emoji"]').length
    };
    // 从 background-image 里取 URL：不用正则 —— 这段是塞进模板字面量再注进页面的，
    // 正则里的 \\) 会被转义成未配对的 )，整条脚本编译期就废（2026-10-12 踩过）。
    const urls = [...new Set(qa('.ep-glyph').map((e) => {
      const bg = getComputedStyle(e).backgroundImage;
      return bg.startsWith('url("') ? bg.slice(5, -2) : bg.startsWith('url(') ? bg.slice(4, -1) : '';
    }).filter(Boolean))];
    out.decoded = [];
    for (const u of urls) {
      try { const im = new Image(); im.src = u; await im.decode(); out.decoded.push(u.split('/').pop() + '=' + im.naturalWidth + 'x' + im.naturalHeight); }
      catch (err) { out.decoded.push('FAIL ' + String(err).slice(0, 40)); }
    }
  } catch (err) {
    out.error = String(err && err.message ? err.message : err);
  }
  return out;
})()`;

ipcMain.handle('app:ping', (e) => {
  if (SELFTEST) {
    const wc = e.sender;
    void (async () => {
      await new Promise((r) => setTimeout(r, 900));
      try {
        console.log('emoji ' + JSON.stringify(await wc.executeJavaScript(EMOJI_SELFTEST_JS)));
      } catch (err) {
        console.log('emoji ERROR ' + String(err));
      }
      if (NETPROBE) {
        console.log('netprobe control-fetch=' + String(await wc.executeJavaScript(NETPROBE_JS)));
      }
      reportNetwork();
      console.log('selftest-ok');
      app.quit();
    })();
  }
  return 'pong';
});

ipcMain.handle('window:minimize', (e) => {
  BrowserWindow.fromWebContents(e.sender)?.minimize();
});
ipcMain.handle('window:maximize', (e) => {
  const win = BrowserWindow.fromWebContents(e.sender);
  if (win?.isMaximized()) win.unmaximize();
  else win?.maximize();
});
ipcMain.handle('window:close', (e) => {
  BrowserWindow.fromWebContents(e.sender)?.close();
});

/* ===== 自绘窗口拖拽 =====
   替代 -webkit-app-region: drag。那条属性会把区域变成 OS 的 HTCAPTION,右键就被
   DefWindowProc 接走弹系统菜单(深色、六项、应用侧改不了),所以整条撤掉。
   位移算法:起手时记住"窗口原点 + 光标原点",每帧用**主进程现读的实时光标**算增量
   ⇒ 渲染层只报"动了一下",不传坐标,所以既不累计误差,也不受某一帧丢事件影响。
   顶边贴住工作区顶 = Aero 那套"卡住再松手就最大化";松手时没动过(纯点击)不算。 */
type DragAnchor = {
  wx: number; wy: number; cx: number; cy: number;
  moved: boolean; hitTop: boolean;
  /** 起手时窗口是最大化的:要等位移超过起拖阈值才还原 —— 否则"点一下标题带"就等于"拽下来",
      用户看到的就是双击最大化之后单击即恢复(2026-10-11 报的这条)。 */
  wasMax: boolean;
};
let dragAnchor: DragAnchor | null = null;
// 自证用:探针按模块变量直接读,不给生产面加 IPC
const dragLog = { start: 0, move: 0, end: 0 };
/** Windows 的 SM_CXDRAG 档(100% 缩放下 4px):小于它算点击,不算拖拽。 */
const DRAG_SLOP = 4;
/** 还原后光标要落在标题带的哪条横线上(带高 68 的视觉中心附近)。 */
const DRAG_GRAB_Y = 24;

/** 位移公式单独抽出来,是为了让探针能拿合成数字验它本身 —— 真实光标不能替用户搬。 */
function dragTarget(a: DragAnchor, cx: number, cy: number, workTop: number) {
  let y = a.wy + (cy - a.cy);
  const hitTop = y < workTop;
  if (hitTop) y = workTop;
  return { x: Math.round(a.wx + (cx - a.cx)), y: Math.round(y), hitTop };
}

function pastDragSlop(a: DragAnchor, cx: number, cy: number) {
  return Math.abs(cx - a.cx) >= DRAG_SLOP || Math.abs(cy - a.cy) >= DRAG_SLOP;
}

ipcMain.on('window:drag-start', (e) => {
  const win = BrowserWindow.fromWebContents(e.sender);
  if (!win) return;
  dragLog.start++;
  const b = win.getBounds();
  const c = screen.getCursorScreenPoint();
  dragAnchor = { wx: b.x, wy: b.y, cx: c.x, cy: c.y, moved: false, hitTop: false, wasMax: win.isMaximized() };
});

ipcMain.on('window:drag-move', (e) => {
  const win = BrowserWindow.fromWebContents(e.sender);
  if (!win || !dragAnchor) return;
  dragLog.move++;
  const c = screen.getCursorScreenPoint();
  const a = dragAnchor;
  if (a.wasMax) {
    // 阈值之前什么都不做:最大化状态下"动一点点"仍然是点击(双击恢复靠的就是它)
    if (!pastDragSlop(a, c.x, c.y)) return;
    // 真拖起来了 ⇒ 还原,并让光标落在标题带同一相对位置,然后按新状态重新锚定
    const m = win.getBounds();
    const fx = m.width > 0 ? (c.x - m.x) / m.width : 0.5;
    win.unmaximize();
    const n = win.getBounds();
    win.setPosition(Math.round(c.x - n.width * fx), Math.round(c.y - DRAG_GRAB_Y));
    const c2 = screen.getCursorScreenPoint();
    const b2 = win.getBounds();
    a.wx = b2.x; a.wy = b2.y; a.cx = c2.x; a.cy = c2.y; a.wasMax = false; a.moved = true;
    return;
  }
  const wa = screen.getDisplayMatching(win.getBounds()).workArea;
  const t = dragTarget(a, c.x, c.y, wa.y);
  if (t.x !== a.wx || t.y !== a.wy) a.moved = true;
  if (t.hitTop) a.hitTop = true;
  win.setPosition(t.x, t.y);
});

ipcMain.on('window:drag-end', (e) => {
  const win = BrowserWindow.fromWebContents(e.sender);
  const a = dragAnchor;
  dragLog.end++;
  dragAnchor = null;
  if (win && a && a.moved && a.hitTop && !win.isMaximized()) win.maximize();
});


void app.whenReady().then(() => {
  if (PROTO) {
    runProtoProbe();
    app.quit();
    return;
  }
  // 零网络判据只在 file:// 交付路径上装（开发模式走 http://localhost 会被自己拦死）
  if (!process.env.VITE_DEV_SERVER_URL) installNetworkGuard();
  createWindow();
  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', () => {
  app.quit();
});
