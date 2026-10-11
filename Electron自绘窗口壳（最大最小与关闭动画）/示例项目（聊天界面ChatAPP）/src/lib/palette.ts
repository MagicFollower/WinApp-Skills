const cached = new Map<string, string>();

/** 读 :root 上的 CSS 变量实际值,让 tokens.css 保持单一真源。 */
export function token(name: string, fallback: string): string {
  const hit = cached.get(name);
  if (hit) return hit;
  const raw = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  const value = raw || fallback;
  cached.set(name, value);
  return value;
}

export const palette = {
  aurora1: token('--aurora-1', '#ffd9a0'),
  aurora2: token('--aurora-2', '#b79ce8'),
  glow: [token('--glow-1', '#2bb673'), token('--glow-2', '#1fb6a6'), token('--glow-3', '#3b9df8')],
  spark: token('--spark', '#ffffff'),
  // GooeyNav 的 color/activeTextColor/colors 会被组件内 parse() 解析,只吃 hex 与 rgb(),
  // 所以必须传实际色值而不是 var(...)。
  navPill: token('--sidebar-text-1', '#ffffff'),
  navActiveInk: token('--text-1', '#1e2430'),
  // HoldButton 的四个颜色只进内联 --hb-* 自定义属性(不经 JS 解析),传 var() 也安全,
  // 但 CSS 类盖不住内联值,所以只能在这里把令牌实际值取出来喂给 props。
  chipBg: token('--chip-bg', '#ffffff'),
  chipInk: token('--text-1', '#1e2430'),
  accent: token('--accent', '#2bb673'),
};
