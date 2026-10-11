import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import AnimatedContent from './bits/AnimatedContent/AnimatedContent';
import { reduceMotion } from '../lib/motionPref';
import { IconClose, IconMax, IconMin } from './Icons';

const isElectron = typeof window !== 'undefined' && 'api' in window;

/* 控制条盒尺寸:5(内边距) + 34 + 4 + 34 + 4 + 34 + 5 = 120,高 5 + 34 + 5 = 44。
   clamp 用它,改按钮尺寸要同步改这里,否则窗口边缘附近右键会贴到屏幕外。 */
const WIN_MENU_W = 120;
const WIN_MENU_H = 44;

/* 拖拽带的契约:元素标 .drag-region = 可以从这里起手拖,其内的 .no-drag = 不起手
   (按钮、输入框、胶囊)。CSS 侧只剩标记作用,不再写 -webkit-app-region —— 那个属性
   会把区域变成 OS 的 HTCAPTION,右键随之被 OS 接走、弹深色系统菜单(条目应用改不了)。
   依据 手册 2.9 */
const inBand = (t: EventTarget | null) =>
  !!t && t instanceof Element && !!t.closest('.drag-region') && !t.closest('.no-drag');

/* 退场时长写在 CSS 的 --t-minimize 里,这里不复制数值:收尾靠 transitionend。
   两条兜底是真的要 —— reduced-motion 下 CSS 是 `transition: none`(事件永远不来),
   而事件被吞掉的话那颗按钮会变成"点了没反应",比少一段动画严重得多。 */
const MINIMIZE_FALLBACK_MS = 400;

function minimizeWithAnim(root: HTMLElement | null) {
  const api = window.api;
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
  const onEnd = (e: TransitionEvent) => {
    if (e.target === root && e.propertyName === 'opacity') go();
  };
  root.addEventListener('transitionend', onEnd);
  root.classList.add('window--minimizing');
  window.setTimeout(go, MINIMIZE_FALLBACK_MS);
}

export default function WindowChrome() {
  const [menuAt, setMenuAt] = useState<{ x: number; y: number } | null>(null);

  /* 右键:整窗一律不吃默认菜单;落在拖拽带上才浮出我们这三项。
     挂在 document 上而不是各条带自己身上,所以左栏顶带与中间头部走同一条路。 */
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
    if (!isElectron) return;
    let dragging = false;
    const api = window.api;
    if (!api) return;

    const onPointerDown = (e: PointerEvent) => {
      if (e.button !== 0 || dragging || !inBand(e.target)) return;
      dragging = true;
      try {
        (e.target as Element).setPointerCapture?.(e.pointerId);
      } catch {
        /* 合成 PointerEvent 没有真实 pointerId,捕获会抛 InvalidPointerId —— 探针就靠合成事件驱动 */
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
  }, []);

  /* 控制条只由 pointerdown / Escape / 失焦关闭:右键自己那一下发生在监听挂上之前,
     所以不会被刚打开的菜单立刻关掉。 */
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

  /* 恢复:把退场留下的类摘掉,但**不**反向播 —— 实测 restore 之后头 ~160ms 渲染器不出帧
     (rAF 采样只有 4 个点、中间一个 164ms 空档),那段"入场"到底有没有被呈现证明不了;
     而按计算值看它起始是 scale .64 / opacity .09,真播出来很可能是一下近乎透明的窗口。
     所以这里用 window--noanim 把 transition 关掉再摘类:恢复即时到位,与系统原本的行为一致。 */
  useEffect(() => {
    const api = window.api;
    if (!api?.onRestore) return;
    const root = document.querySelector<HTMLElement>('.window');
    api.onRestore(() => {
      if (!root) return;
      root.classList.add('window--noanim');
      root.classList.remove('window--minimizing');
      void root.offsetWidth; // 强制回流:不挡这一下,上面两条会被合并进同一帧,transition 照样播
      root.classList.remove('window--noanim');
    });
  }, []);

  if (!isElectron || !menuAt) return null;

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
          minimizeWithAnim(document.querySelector<HTMLElement>('.window'));
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
          window.api?.maximize();
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
          window.api?.close();
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

  return createPortal(
    reduceMotion ? (
      <div className="win-menu-wrap" style={pos}>
        {bar}
      </div>
    ) : (
      /* threshold=0 让 ScrollTrigger 的起点落在视口底边 ⇒ 一挂上就满足,不必真滚到那儿;
         默认 0.1 会让"贴底"位置上的菜单永远停在 visibility:hidden。 */
      <AnimatedContent
        className="win-menu-wrap"
        style={pos}
        threshold={0}
        distance={8}
        duration={0.16}
        ease="power2.out"
        scale={0.96}
      >
        {bar}
      </AnimatedContent>
    ),
    document.body
  );
}
