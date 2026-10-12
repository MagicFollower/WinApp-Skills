import type { CSSProperties } from 'react';
import GooeyNav from './bits/GooeyNav/GooeyNav';
import type { GooeyNavItem } from './bits/GooeyNav/GooeyNav';
import { SEED_CHATS } from '../data/seed';
import { palette } from '../lib/palette';
import { useAppStore } from '../stores/app';
import type { ChatFilter } from '../types';
import { IconGroup, IconMenu, IconPaw, IconPerson, IconSearch, IconSpace } from './Icons';

/** 4 格是硬上限:GooeyNav 的 shader 只有 MAX_PILLS = 4 个 pill 槽位,第 5 个画不出来。 */
const FILTERS: (GooeyNavItem & { key: ChatFilter })[] = [
  { key: '全部', label: '全部', icon: <IconPaw /> },
  { key: '个人', label: '个人', icon: <IconPerson /> },
  { key: '群聊', label: '群聊', icon: <IconGroup /> },
  { key: '空间', label: '空间', icon: <IconSpace /> },
];

/* 组件把 THEMES 写成 <nav> 的内联 style,CSS 类盖不住,只能走它自己的 style prop。 */
const NAV_VARS = {
  '--gn-ink': 'var(--sidebar-text-1)',
  '--gn-muted': 'var(--sidebar-text-2)',
} as CSSProperties;

const TYPE_BY_FILTER: Record<Exclude<ChatFilter, '全部'>, string> = {
  个人: 'personal',
  群聊: 'group',
  空间: 'space',
};

export function Sidebar() {
  const { filter, search, activeChatId, setFilter, setSearch, selectChat } = useAppStore();

  const chats = SEED_CHATS.filter((c) => {
    if (filter !== '全部' && c.type !== TYPE_BY_FILTER[filter as Exclude<ChatFilter, '全部'>]) return false;
    if (search && !c.name.toLowerCase().includes(search.toLowerCase())) return false;
    return true;
  });

  return (
    <aside className="sidebar">
      <div className="sidebar-top drag-region">
        <button className="menu-btn no-drag" aria-label="菜单">
          <IconMenu />
        </button>
        <span className="app-badge" />
        <div className="searchbox no-drag">
          <IconSearch size={14} />
          <input
            placeholder="搜索联系人…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
      </div>

      <div className="filter-tabs">
        {/* 不传 colors:组件在 colors 为空时让水珠沿用 pill 色,切换全程白色 */}
        <GooeyNav
          items={FILTERS}
          activeIndex={FILTERS.findIndex((f) => f.key === filter)}
          onChange={(index) => setFilter(FILTERS[index].key)}
          className="filter-nav"
          style={NAV_VARS}
          theme="dark"
          size="md"
          frame
          color={palette.navPill}
          activeTextColor={palette.navActiveInk}
          animationTime={450}
          particleCount={15}
          timeVariance={350}
          spread={0}
          gooeyness={0.5}
          wobble={0.5}
          hoverEffect
        />
      </div>

      <div className="chat-list scroll on-dark">
        {chats.map((c) => (
          <div
            key={c.id}
            className={`chat-item ${activeChatId === c.id ? 'active' : ''}`}
            onClick={() => selectChat(c.id)}
          >
            <span className="avatar" style={{ background: c.avatarBg }}>
              {c.avatar}
            </span>
            <div className="meta">
              <div className="name">{c.name}</div>
              <div className="sub">{c.members} 成员</div>
            </div>
          </div>
        ))}
      </div>
    </aside>
  );
}
