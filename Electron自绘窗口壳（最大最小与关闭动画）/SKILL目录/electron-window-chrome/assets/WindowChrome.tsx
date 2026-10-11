import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';

/* ============================================================================
   自绘窗口壳:拖拽 + 右键三项控制条 + 最小化退场动画。零第三方依赖(只要 react/react-dom)。
   拷进目标工程的 src/components/ 后按下面三段契约接线。

   契约 1 — 渲染层 API:宿主 preload 必须暴露 window.api,方法名固定
            minimize / maximize / close / dragStart / dragMove / dragEnd / onRestore
            (见 preload-window-chrome.ts + window-api.d.ts)。
   契约 2 — DOM 标记:可以从这里起手拖窗的元素标 .drag-region,其内的按钮/输入框标
            .no-drag。CSS 侧只剩标记作用(见 window-chrome.css),不再写
            -webkit-app-region —— 那个属性会把区域变成 OS 的 HTCAPTION,右键随之被
            OS 接走、弹深色系统菜单(条目应用改不了)。依据 references/pitfalls.md ①。
   契约 3 — 窗口根元素:视觉上就是那张贴桌面的卡片的元素(本文件默认按 .window 找),
            退场动画挂它身上。类名不同就传 rootSelector。
   ========================================================================== */

/* 控制条盒尺寸:5(内边距) + 34 + 4 + 34 + 4 + 34 + 5 = 120,高 5 + 34 + 5 = 44。
   clamp 用它,改 window-chrome.css 里的按钮尺寸/内边距/间距必须同步改这里,
   否则窗口边缘附近右键会贴到屏幕外。scripts/check-wiring.mjs 会算这笔账。 */
const WIN_MENU_W = 120;
const WIN_MENU_H = 44;

/* 退场时长写在 CSS 的 --chrome-t 里,这里不复制数值:收尾靠 transitionend。
   下面那条兜底时间是真的要 —— reduced-motion 下 CSS 是 `transition: none`(事件永远
   不来),而事件被吞掉的话那颗按钮会变成"点了没反应",比少一段动画严重得多。 */
const MINIMIZE_FALLBACK_MS = 400;

const reduceMotion =
  typeof window !== 'undefined' &&
  window.matchMedia('(prefers-reduced-motion: reduce)').matches;

/* 落在拖拽带上、且不是带内控件 —— 起手/右键/双击三条判定共用这一个谓词,
   否则会出现"能拖但右键不吃"或反过来的分裂状态。 */
const inBand = (t: EventTarget | null) =>
  !!t && t instanceof Element && !!t.closest('.drag-region') && !t.closest('.no-drag');

function minimizeWithAnim(api: WindowChromeApi | undefined, root: HTMLElement | null) {
  if (!api) return;
  if (!root || reduceMotion) {
    void api.minimize();
    return;
  }
  let done = false;
  const go = () => {
    if (done) return;
    done = true;
    void api.minimize();
  };
  /* 只认根元素自己的 opacity 结束:子树里任何一条 transition 结束都会冒泡到这里,
     不筛 target 就会在动画播到一半时提前 minimize。 */
  const onEnd = (e: TransitionEvent) => {
    if (e.target === root && e.propertyName === 'opacity') go();
  };
  root.addEventListener('transitionend', onEnd);
  root.classList.add('window--minimizing');
  window.setTimeout(go, MINIMIZE_FALLBACK_MS);
}

export default function WindowChrome({ rootSelector = '.window' }: { rootSelector?: string }) {
  const api = typeof window === 'undefined' ? undefined : window.api;
  const [menuAt, setMenuAt] = useState<{ x: number; y: number } | null>(null);

  /* 右键:整窗一律不吃默认菜单;落在拖拽带上才浮出我们这三项。
     挂在 document 上而不是各条带自己身上,所以任意数量的拖拽带走同一条路。 */
  useEffect(() => {
    const onContextMenu = (e: MouseEvent) => {
      if (!inBand(e.target)) return;
      e.preventDefault();
      setMenuAt({ x: e.clientX, y: e.clientY });
    };
    document.addEventListener('contextmenu', onContextMenu);
    return () => document.removeEventListener('contextmenu', onContextMenu);
  }, []);

  /* 拖拽:起手时告诉主进程"开始",之后每次 pointermove 只报"动了一下",
     真实位移由主进程现读光标算 ⇒ 不累计误差,漏帧也只是下一帧补上。
     setPointerCapture 是关键:窗口跟着光标跑,光标很容易甩到窗口外,
     不捕获就收不到后续 pointermove,窗口会停在半路。 */
  useEffect(() => {
    if (!api) return;
    let dragging = false;

    const onPointerDown = (e: PointerEvent) => {
      if (e.button !== 0 || dragging || !inBand(e.target)) return;
      dragging = true;
      try {
        (e.target as Element).setPointerCapture?.(e.pointerId);
      } catch {
        /* 合成 PointerEvent 没有真实 pointerId,捕获会抛 InvalidPointerId —— 验收探针就靠合成事件驱动 */
      }
      api.dragStart();
    };
    const onPointerMove = () => {
      if (dragging) api.dragMove();
    };
    const endDrag = () => {
      if (!dragging) return;
      dragging = false;
      api.dragEnd();
    };
    const onDblClick = (e: MouseEvent) => {
      if (inBand(e.target)) api.maximize();
    };

    window.addEventListener('pointerdown', onPointerDown, true);
    window.addEventListener('pointermove', onPointerMove, true);
    window.addEventListener('pointerup', endDrag, true);
    window.addEventListener('pointercancel', endDrag, true);
    window.addEventListener('blur', endDrag);
    window.addEventListener('dblclick', onDblClick, true);
    return () => {
      window.removeEventListener('pointerdown', onPointerDown, true);
      window.removeEventListener('pointermove', onPointerMove, true);
      window.removeEventListener('pointerup', endDrag, true);
      window.removeEventListener('pointercancel', endDrag, true);
      window.removeEventListener('blur', endDrag);
      window.removeEventListener('dblclick', onDblClick, true);
    };
  }, [api]);

  /* 控制条只由 pointerdown / Escape / 失焦关闭:右键自己那一下发生在监听挂上之前,
     所以不会被刚打开的菜单立刻关掉 —— 别改成监听 click 来"修",那会真的立刻关掉。 */
  useEffect(() => {
    if (!menuAt) return;
    const close = () => setMenuAt(null);
    const onPointerDown = (e: PointerEvent) => {
      if (!(e.target as Element).closest('.win-menu')) close();
    };
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') close();
    };
    window.addEventListener('pointerdown', onPointerDown, true);
    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('blur', close);
    return () => {
      window.removeEventListener('pointerdown', onPointerDown, true);
      window.removeEventListener('keydown', onKeyDown);
      window.removeEventListener('blur', close);
    };
  }, [menuAt]);

  /* 恢复:把退场留下的类摘掉,但**不**反向播。
     实测 restore 之后头 ~160ms 渲染器不出帧(rAF 采样只有 4 个点、中间一个 164ms 空档),
     那段"入场"到底有没有被呈现证明不了;而按计算值看它起始是 scale .64 / opacity .09,
     真播出来很可能是一下近乎透明的窗口。所以这里用 window--noanim 把 transition 关掉
     再摘类:恢复即时到位,与系统原本的行为一致。 */
  useEffect(() => {
    if (!api?.onRestore) return;
    const root = document.querySelector<HTMLElement>(rootSelector);
    api.onRestore(() => {
      if (!root) return;
      root.classList.add('window--noanim');
      root.classList.remove('window--minimizing');
      void root.offsetWidth; // 强制回流:不挡这一下,上面两条会被合并进同一帧,transition 照样播
      root.classList.remove('window--noanim');
    });
  }, [api, rootSelector]);

  /* 纯浏览器里跑 Vite(没有 preload)时整块不渲染,页面其余部分照常工作。 */
  if (!api || !menuAt) return null;

  const bar = (
    <div
      className="win-menu no-drag"
      role="menu"
      aria-label="窗口控制"
      onContextMenu={(e) => e.preventDefault()}
    >
      <button
        className="win-menu-btn"
        role="menuitem"
        aria-label="最小化"
        onClick={() => {
          setMenuAt(null);
          minimizeWithAnim(api, document.querySelector<HTMLElement>(rootSelector));
        }}
      >
        <IconMin />
      </button>
      <button
        className="win-menu-btn"
        role="menuitem"
        aria-label="最大化"
        onClick={() => {
          setMenuAt(null);
          void api.maximize();
        }}
      >
        <IconMax />
      </button>
      <button
        className="win-menu-btn"
        role="menuitem"
        aria-label="关闭"
        onClick={() => {
          setMenuAt(null);
          void api.close();
        }}
      >
        <IconClose />
      </button>
    </div>
  );

  const pos = {
    left: Math.max(8, Math.min(menuAt.x, window.innerWidth - WIN_MENU_W - 8)),
    top: Math.max(8, Math.min(menuAt.y, window.innerHeight - WIN_MENU_H - 8)),
  };

  /* portal 到 body + 视口坐标定位:菜单不能活在拖拽带的层叠上下文里,
     否则祖先的 transform/backdrop-filter 会把它裁掉或压住。
     fixed 挂在包裹层而不是 .win-menu 上 —— 入场动效在自己的元素上做 translate/scale,
     一旦祖先带 transform 就成了 fixed 的包含块,位置会跟着位移抖 8px。 */
  return createPortal(
    <div className="win-menu-wrap" style={pos}>
      {bar}
    </div>,
    document.body
  );
}

/* 三颗图标就地内联,免得这份资产再拖一个 Icons.tsx 进目标工程。
   14px 线宽 1.4 是配 34px 按钮量的;换尺寸改 svg 的 width/height 就够。 */
function IconMin() {
  return (
    <svg width="14" height="14" viewBox="0 0 14 14" aria-hidden="true">
      <path d="M3 7h8" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" fill="none" />
    </svg>
  );
}
function IconMax() {
  return (
    <svg width="14" height="14" viewBox="0 0 14 14" aria-hidden="true">
      <rect x="3.3" y="3.3" width="7.4" height="7.4" rx="1.6" fill="none" stroke="currentColor" strokeWidth="1.4" />
    </svg>
  );
}
function IconClose() {
  return (
    <svg width="14" height="14" viewBox="0 0 14 14" aria-hidden="true">
      <path d="M4 4l6 6M10 4l-6 6" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" fill="none" />
    </svg>
  );
}
