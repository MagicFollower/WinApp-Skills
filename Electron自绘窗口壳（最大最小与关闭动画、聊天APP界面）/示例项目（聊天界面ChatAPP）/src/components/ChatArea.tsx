import { useEffect, useRef, useState } from 'react';
import FoldText from './bits/FoldText/FoldText';
import ClickSpark from './bits/ClickSpark/ClickSpark';
import Magnet from './bits/Magnet/Magnet';
import { chatSubtitle, findChat, senderColor } from '../data/seed';
import { palette } from '../lib/palette';
import { reduceMotion } from '../lib/motionPref';
import { useAppStore } from '../stores/app';
import { emojiGlyph } from '../emoji/atlas';
import { EmojiGlyph } from '../emoji/SpriteCell';
import type { GlyphSize, Message, MessagePart } from '../types';
import {
  IconBell,
  IconDots,
  IconDown,
  IconFile,
  IconImage,
  IconMic,
  IconSend,
  IconSmile,
} from './Icons';

/** 把草稿 DOM 读成分段:文本节点是 text 档,token 按自己的 data-size 成段,相邻同档合并。 */
function draftParts(box: HTMLElement): MessagePart[] {
  const parts: MessagePart[] = [];
  for (const node of Array.from(box.childNodes)) {
    const text = node.textContent ?? '';
    if (!text) continue;
    const size = node instanceof HTMLElement ? ((node.dataset.size as GlyphSize) ?? 'text') : 'text';
    const last = parts[parts.length - 1];
    /* 只并相邻的文本段。表情 token 一格一段,不能并:atlas 的键是按单格码位切的,
       两格并成 "1f62e-200d-1f4a8-1f602" 就查不到贴图,发出去会掉回系统字形(实测)。 */
    if (size === 'text' && last && last.size === 'text') last.text += text;
    else parts.push({ text, size });
  }
  return parts;
}

/** 带分段的气泡逐段渲染:表情段走与面板同一张 atlas 的贴图,其余跟着气泡正文 14.5px。 */
function bubbleText(msg: Message) {
  if (!msg.parts) return msg.text;
  return msg.parts.map((p, i) =>
    p.size === 'text' ? p.text : <EmojiGlyph key={i} text={p.text} tier={p.size} />
  );
}

function Bubble({ msg }: { msg: Message }) {
  if (msg.kind === 'bigemoji') {
    return (
      <div className={`msg-row ${msg.own ? 'own' : ''}`}>
        <span className="bigemoji">{msg.text}</span>
      </div>
    );
  }
  if (msg.kind === 'quote') {
    return (
      <div className={`msg-row ${msg.own ? 'own' : ''}`}>
        <div className="quote-card">
          <span className="qicon" style={{ color: 'var(--quote-accent)' }}>
            <IconMic />
          </span>
          <div>
            <div className="qsender">{msg.quoteSender}</div>
            <div className="qtext">{msg.text}</div>
          </div>
        </div>
      </div>
    );
  }
  return (
    <div className={`msg-row ${msg.own ? 'own' : ''}`}>
      <div className={`bubble ${msg.kind === 'image' ? 'has-thumb' : ''}`}>
        {!msg.own && (
          <div className="sender" style={{ color: senderColor(msg.sender) }}>
            {msg.sender}
          </div>
        )}
        {msg.kind === 'image' && (
          <span
            className="thumb"
            style={{
              background:
                'linear-gradient(135deg,#8a5cf6 0%,#3b9df8 45%,#1fb6a6 100%)',
            }}
          />
        )}
        <div className="text">{bubbleText(msg)}</div>
      </div>
    </div>
  );
}

/* 表情做成不可编辑的整块:一次退格删掉整个码位串,而不是像 <input> 那样逐 UTF-16
   单元删、删进 ZWJ/变体选择器中间留下孤立代理对。外观与面板同一张 atlas(emojiGlyph),
   data-size 是 draftParts 读回档位的唯一依据。 */
function glyphToken(text: string, size: 'sm' | 'lg') {
  const g = emojiGlyph(text, size);
  const span = document.createElement('span');
  span.className = g.className;
  if (g.style) Object.assign(span.style, g.style);
  span.dataset.size = size;
  span.contentEditable = 'false';
  span.textContent = text;
  return span;
}

export function ChatArea() {
  const { messages, send, togglePanel } = useAppStore();
  const draftQueue = useAppStore((s) => s.draftQueue);
  const ackDraft = useAppStore((s) => s.ackDraft);
  const activeChatId = useAppStore((s) => s.activeChatId);
  const active = findChat(activeChatId);
  const theme = active.themeId;
  const [lastCovered, setLastCovered] = useState(false);
  const listRef = useRef<HTMLDivElement>(null);
  const composerRef = useRef<HTMLDivElement>(null);
  const boxRef = useRef<HTMLDivElement>(null);
  const appliedInsert = useRef<WeakSet<object>>(new WeakSet());

  useEffect(() => {
    document.documentElement.dataset.chatTheme = theme;
  }, [theme]);

  /* 出现判据 = 末条消息的底边越过输入区顶边(即真的被输入栏盖住),不是"离底 N px"。
     读的是两个元素的实际盒,所以列表末尾那 76px padding、消息高度变化、右栏避让
     改掉的可用宽度都自动算进去,不需要跟着调常数。 */
  const syncJump = () => {
    const list = listRef.current;
    const bar = composerRef.current;
    const last = list?.lastElementChild;
    if (!list || !bar || !last) {
      setLastCovered(false);
      return;
    }
    setLastCovered(last.getBoundingClientRect().bottom > bar.getBoundingClientRect().top);
  };

  useEffect(() => {
    const el = listRef.current;
    if (el) {
      el.scrollTop = el.scrollHeight;
      syncJump();
    }
  }, [messages]);

  /* 拉高窗口后内容不再溢出,但这时往往一个 scroll 事件都不派发(没有可滚的量了)——
     不重算就会让钮赖在屏上。观察列表盒:窗口尺寸、右栏浮层、面板开合都走这条路。 */
  useEffect(() => {
    const el = listRef.current;
    if (!el) return;
    const ro = new ResizeObserver(syncJump);
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const jumpToBottom = () => {
    const el = listRef.current;
    if (!el) return;
    // 不用 behavior:'smooth':平滑滚动途中会派发 scroll 事件,那时末条还被盖着,
    // 会把刚点掉的钮又挂回来(闪一下),落定才消失。直跳只有一次事件。
    el.scrollTop = el.scrollHeight;
    syncJump();
  };

  /* 面板那一格 → 按队列顺序落到框尾(和原来 appendDraft 的字符串拼接同位置:点格子会让输入框
     失焦,光标本来就不在了)。去重按命令对象本身而不是"应用到第几号 id":id 计数器在 store 模块里、
     这个游标在组件里,store 被热更替后 id 从 1 重来,游标还停在高值,会把新命令整批滤掉(实测点格子没反应)。 */
  useEffect(() => {
    const box = boxRef.current;
    if (!box) return;
    const pending = draftQueue.filter((d) => !appliedInsert.current.has(d));
    if (!pending.length) return;
    for (const d of pending) {
      appliedInsert.current.add(d);
      box.appendChild(
        d.size === 'text' ? document.createTextNode(d.text) : glyphToken(d.text, d.size)
      );
    }
    ackDraft(pending[pending.length - 1].id);
  }, [draftQueue, ackDraft]);

  const submit = () => {
    const box = boxRef.current;
    if (!box) return;
    const parts = draftParts(box);
    if (!parts.map((p) => p.text).join('').trim()) return;
    send(parts);
    box.textContent = '';
  };

  const composerPill = (
    <div className="composer">
      <button className="icon-btn" aria-label="文件">
        <IconFile />
      </button>
      <button className="icon-btn" aria-label="图片">
        <IconImage />
      </button>
      <div className="composer-input">
        {/* 单字号的 <input> 连"这一格是表情还是打字"都记不住,所以这里是 contenteditable:
            打字/粘贴是文本节点,两档表情各是一个 token(.draft-sm 在框里与正文同字号,
            但气泡要靠它才知道该放大谁)。内容不进 React 树 —— 框就是草稿的真源。 */}
        <div
          ref={boxRef}
          className="draft-box"
          contentEditable
          suppressContentEditableWarning
          role="textbox"
          aria-label="消息"
          data-placeholder="消息"
          spellCheck={false}
          onKeyDown={(e) => {
            if (e.key !== 'Enter' || e.nativeEvent.isComposing) return;
            e.preventDefault();
            submit();
          }}
          onPaste={(e) => {
            e.preventDefault();
            // 走 insertText 而不是自己拼 DOM:落点仍是光标位置,也留在撤销栈里。
            const t = (e.clipboardData?.getData('text/plain') ?? '').replace(/[\r\n]+/g, ' ');
            if (t) document.execCommand('insertText', false, t);
          }}
          onInput={() => {
            const box = boxRef.current;
            // 删空后 Chrome 留一个 <br> 撑场:：empty 失效,占位文案不会再出现。
            if (box && !box.textContent && box.childNodes.length) box.textContent = '';
          }}
        />
        <button className="icon-btn" aria-label="表情" onClick={togglePanel}>
          <IconSmile />
        </button>
      </div>
    </div>
  );

  const sendButton = (
    <button className="send-btn" aria-label="发送" onClick={submit}>
      <IconSend />
    </button>
  );

  return (
    <main className="chat-area">
      <header className="chat-header drag-region">
        <div className="header-pill no-drag">
          <span className="avatar" style={{ background: active.avatarBg }}>
            {active.avatar}
          </span>
          <div>
            <FoldText
              className="title"
              text={active.name}
              splitBy="char"
              hinge="left"
              trigger="mount"
              duration={0.42}
              stagger={0.03}
              perspective={520}
              creaseShading={0.35}
              fontSize={15}
              fontWeight={600}
              color="var(--text-1)"
            />
            <div className="subtitle">{chatSubtitle(active)}</div>
          </div>
        </div>
        <div className="header-actions no-drag">
          <button className="action-pill" aria-label="通知">
            <IconBell />
          </button>
          <button className="action-pill" aria-label="更多">
            <IconDots />
          </button>
        </div>
      </header>

      <div className="message-list scroll" ref={listRef} onScroll={syncJump}>
        {messages.map((m) => (
          <Bubble key={m.id} msg={m} />
        ))}
      </div>

      {/* 消息流穿过头部/输入栏底下时,先被这两条模糊再淡出 */}
      <span className="chat-fade chat-fade--top" aria-hidden="true" />
      <span className="chat-fade chat-fade--bottom" aria-hidden="true" />

      {lastCovered && (
        <button className="jump-bottom no-drag" aria-label="回到最新消息" onClick={jumpToBottom}>
          <IconDown />
        </button>
      )}

      <div className="composer-wrap" ref={composerRef}>
        {composerPill}
        {reduceMotion ? (
          sendButton
        ) : (
          <Magnet wrapperClassName="send-magnet" padding={40} magnetStrength={6}>
            <ClickSpark
              sparkColor={palette.spark}
              sparkCount={6}
              sparkSize={9}
              sparkRadius={13}
              duration={420}
              easing="ease-out"
            >
              {sendButton}
            </ClickSpark>
          </Magnet>
        )}
      </div>
    </main>
  );
}
