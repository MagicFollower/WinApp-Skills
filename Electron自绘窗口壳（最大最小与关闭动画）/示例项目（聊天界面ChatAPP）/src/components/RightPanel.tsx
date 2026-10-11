import { useState } from 'react';
import AnimatedContent from './bits/AnimatedContent/AnimatedContent';
import HoldButton from './bits/HoldButton/HoldButton';
import { EMOJI_SETS } from '../data/seed';
import { palette } from '../lib/palette';
import { reduceMotion } from '../lib/motionPref';
import { useAppStore } from '../stores/app';
import type { EmojiTab } from '../types';
import { IconChevron, IconClose, IconExpand, IconGroup, IconSearch } from './Icons';

const TABS: EmojiTab[] = ['全部', '定制', '动态', '贴纸', '手势'];
const WIDE_GRID_TABS: EmojiTab[] = ['定制'];

/** 按住多久直接发送(用户口径 1s)。组件默认 2000,这里显式传。 */
const HOLD_MS = 1000;

function PhraseSection() {
  const { phrases, phrasesOpen, togglePhrases, appendDraft, send, addPhrase, updatePhrase } = useAppStore();
  const [editing, setEditing] = useState<string | null>(null);
  const [text, setText] = useState('');
  const [adding, setAdding] = useState(false);

  const startEdit = (p: string) => {
    setAdding(false);
    setEditing(p);
    setText(p);
  };
  const commitEdit = () => {
    if (editing !== null) updatePhrase(editing, text);
    setEditing(null);
  };
  const commitAdd = () => {
    addPhrase(text);
    setAdding(false);
    setText('');
  };

  return (
    <section className="panel-section phrase-section">
      <button
        type="button"
        className="section-head collapsible"
        aria-expanded={phrasesOpen}
        onClick={togglePhrases}
      >
        <span className="head-label">
          {/* IconChevron 只接 size,className 会被静默丢掉,所以旋转挂在包裹层上 */}
          <span className="chevron">
            <IconChevron />
          </span>
          快捷短语
        </span>
        <span className="count">{phrases.length}</span>
      </button>

      {phrasesOpen && (
        <div className="phrase-chips scroll">
          {phrases.map((p) => (
            <span
              key={p}
              className="phrase-slot"
              onContextMenu={(e) => {
                e.preventDefault();
                startEdit(p);
              }}
            >
              {editing === p ? (
                <input
                  className="phrase-input"
                  autoFocus
                  value={text}
                  onChange={(e) => setText(e.target.value)}
                  onBlur={commitEdit}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') commitEdit();
                    if (e.key === 'Escape') setEditing(null);
                  }}
                />
              ) : (
                <HoldButton
                  size="sm"
                  radius={12}
                  holdTime={HOLD_MS}
                  resetAfter={900}
                  doneLabel="已发送"
                  backgroundColor={palette.chipBg}
                  fillColor={palette.accent}
                  textColor={palette.chipInk}
                  fillTextColor="#ffffff"
                  onTap={() => appendDraft(p)}
                  onHold={() => send(p)}
                >
                  {p}
                </HoldButton>
              )}
            </span>
          ))}

          {adding ? (
            <input
              className="phrase-input"
              autoFocus
              placeholder="新短语"
              value={text}
              onChange={(e) => setText(e.target.value)}
              onBlur={commitAdd}
              onKeyDown={(e) => {
                if (e.key === 'Enter') commitAdd();
                if (e.key === 'Escape') {
                  setAdding(false);
                  setText('');
                }
              }}
            />
          ) : (
            <span
              className="chip add"
              role="button"
              tabIndex={0}
              onClick={() => {
                setEditing(null);
                setAdding(true);
                setText('');
              }}
            >
              + 添加
            </span>
          )}
        </div>
      )}
    </section>
  );
}

function PanelBody() {
  const { emojiTab, setEmojiTab, togglePanel, send } = useAppStore();
  const cells = EMOJI_SETS[emojiTab] ?? [];
  const wide = WIDE_GRID_TABS.includes(emojiTab);

  return (
    <>
      <div className="searchbox on-light">
        <IconSearch size={14} />
        <input placeholder="搜索…" />
      </div>

      <nav className="panel-tabs">
        {TABS.map((t) => (
          <button
            key={t}
            className={`panel-tab ${emojiTab === t ? 'active' : ''}`}
            onClick={() => setEmojiTab(t)}
          >
            {t}
          </button>
        ))}
      </nav>

      <div className={`emoji-grid scroll ${wide ? 'cols-9' : 'cols-6'}`}>
        {cells.map((e, i) => (
          <span key={`${e}-${i}`} className="emoji-cell" onClick={() => send(e)}>
            {e}
          </span>
        ))}
      </div>

      <section className="panel-section">
        <div className="section-head">
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
            <IconGroup />
            群成员
          </span>
          <span className="ops">
            <IconExpand />
            <span onClick={togglePanel} style={{ cursor: 'pointer', display: 'inline-flex' }}>
              <IconClose />
            </span>
          </span>
        </div>
      </section>

      <PhraseSection />
    </>
  );
}

export function RightPanel() {
  const body = <PanelBody />;
  return (
    <aside className="right-panel">
      {reduceMotion ? (
        body
      ) : (
        <AnimatedContent
          className="panel-anim"
          distance={28}
          direction="vertical"
          duration={0.22}
          ease="power3.out"
          threshold={0}
        >
          {body}
        </AnimatedContent>
      )}
    </aside>
  );
}
