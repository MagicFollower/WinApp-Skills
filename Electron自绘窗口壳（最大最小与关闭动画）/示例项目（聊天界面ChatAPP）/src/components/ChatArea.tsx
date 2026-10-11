import { useEffect, useRef, useState } from 'react';
import FoldText from './bits/FoldText/FoldText';
import ClickSpark from './bits/ClickSpark/ClickSpark';
import Magnet from './bits/Magnet/Magnet';
import { chatSubtitle, findChat, senderColor } from '../data/seed';
import { palette } from '../lib/palette';
import { reduceMotion } from '../lib/motionPref';
import { useAppStore } from '../stores/app';
import type { Message } from '../types';
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
        <div className="text">{msg.text}</div>
      </div>
    </div>
  );
}

export function ChatArea() {
  const { messages, send, togglePanel, draft, setDraft } = useAppStore();
  const activeChatId = useAppStore((s) => s.activeChatId);
  const active = findChat(activeChatId);
  const theme = active.themeId;
  const [lastCovered, setLastCovered] = useState(false);
  const listRef = useRef<HTMLDivElement>(null);
  const composerRef = useRef<HTMLDivElement>(null);

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

  const submit = () => {
    const t = draft.trim();
    if (!t) return;
    send(t);
    setDraft('');
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
        <input
          placeholder="消息"
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
              e.preventDefault();
              submit();
            }
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
