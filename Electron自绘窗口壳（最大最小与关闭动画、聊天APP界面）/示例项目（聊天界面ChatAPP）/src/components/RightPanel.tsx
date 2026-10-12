import { useState, type MouseEvent as ReactMouseEvent } from 'react';
import AnimatedContent from './bits/AnimatedContent/AnimatedContent';
import HoldButton from './bits/HoldButton/HoldButton';
import { BIG_EMOJI } from '../data/seed';
import { palette } from '../lib/palette';
import { reduceMotion } from '../lib/motionPref';
import { useAppStore } from '../stores/app';
import type { EmojiTab } from '../types';
import { EmojiPicker } from '../emoji/Picker';
import { EMOJI_PX, spriteStyle, unifiedOf } from '../emoji/atlas';
import { IconChevron, IconClose, IconExpand, IconGroup } from './Icons';

const TABS: EmojiTab[] = ['表情', '大表情'];

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
                  onHold={() => send([{ text: p, size: 'text' }])}
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
  const { emojiTab, setEmojiTab, togglePanel, appendDraft } = useAppStore();

  return (
    <>
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

      {emojiTab === '表情' ? (
        /* 搜索/分类栏/肤色都由 picker 自带，面板顶部那条从未接线的搜索框随之撤掉 */
        <div className="panel-picker">
          <EmojiPicker columns={7} onPick={(d) => appendDraft(d.emoji, 'sm')} />
        </div>
      ) : (
        <div className="emoji-grid scroll cols-6">
          {BIG_EMOJI.map((e, i) => {
            /* atlas 优先：与 picker 同一套 Apple 位图，跨平台一致；没有格子才回落到系统字形，
               否则那格是 0×0 的空栏（只看 DOM 数量看不出来）。 */
            const glyph = spriteStyle(unifiedOf(e), EMOJI_PX.cell);
            return (
              <span key={`${e}-${i}`} className="emoji-cell" onClick={() => appendDraft(e, 'lg')}>
                {glyph ? <span className="ep-glyph" style={glyph}>{e}</span> : e}
              </span>
            );
          })}
        </div>
      )}

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

/** 格子里现在带码位文本，拖选能复制——但按下/抬起落在同一格时浏览器照样派发 click，
    一次框选就会顺手把表情插进草稿。这一枪在捕获阶段吞掉。
    判据取"选区两端都在这一格内"，不取"当前有非空选区"：别处的残留选区不该挡住正常点击。 */
function swallowSelectPick(e: ReactMouseEvent) {
  const cell = (e.target as HTMLElement).closest<HTMLElement>('.emoji-cell, [data-epr-part="emoji"]');
  if (!cell) return;
  const sel = window.getSelection();
  if (!sel || sel.isCollapsed || !sel.toString()) return;
  if (cell.contains(sel.anchorNode) && cell.contains(sel.focusNode)) e.stopPropagation();
}

export function RightPanel() {
  const body = <PanelBody />;
  return (
    <aside className="right-panel" onClickCapture={swallowSelectPick}>
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
