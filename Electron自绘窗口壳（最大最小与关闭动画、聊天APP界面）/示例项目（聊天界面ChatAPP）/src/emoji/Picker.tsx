import * as Picker from 'emoji-picker-react/primitives';
import type { EmojiClickData, EmojiData, CategoryIcons } from 'emoji-picker-react/primitives';
import pickerJson from './data/picker.json';
import { SpriteCell } from './SpriteCell';
import { spriteStyle } from './atlas';

// Module scope on purpose: the loader/data identity must stay stable or the
// dataset reloads on every render.
const emojiData = pickerJson as unknown as EmojiData;
const components = { Emoji: SpriteCell };

// The built-in category rail draws native text glyphs, which come out as grey
// outlines on Windows and tofu on a Linux box with no emoji font — the exact
// inconsistency the local atlas exists to remove.
const CATEGORY_ICON_PX = 18;
const categoryUnificodes: Record<string, string> = {
  [Picker.Categories.SUGGESTED]: '1f551',
  [Picker.Categories.SMILEYS_PEOPLE]: '1f600',
  [Picker.Categories.ANIMALS_NATURE]: '1f43a',
  [Picker.Categories.FOOD_DRINK]: '1f354',
  [Picker.Categories.TRAVEL_PLACES]: '1f697',
  [Picker.Categories.ACTIVITIES]: '1f3c6',
  [Picker.Categories.OBJECTS]: '1f4a1',
  [Picker.Categories.SYMBOLS]: '2764-fe0f',
  [Picker.Categories.FLAGS]: '1f3c1'
};
const categoryIcons = Object.fromEntries(
  Object.entries(categoryUnificodes).flatMap(([cat, u]) => {
    const style = spriteStyle(u, CATEGORY_ICON_PX);
    return style ? [[cat, <span key={cat} className="ep-glyph ep-cat-icon" style={style} />]] : [];
  })
) as CategoryIcons;
const labels = {
  searchPlaceholder: '搜索',
  searchLabel: '搜索表情',
  searchClear: '清除搜索',
  searchResultsNone: '没有匹配的表情',
  searchResultsOne: '1 个结果',
  searchResultsMany: '%n 个结果',
  categoryNavigation: '分类',
  loading: '加载中…',
  loadingError: '表情数据加载失败。',
  retryLoading: '重试'
};

export type { EmojiClickData };

export function EmojiPicker({ onPick, columns = 9 }: { onPick: (data: EmojiClickData) => void; columns?: number }) {
  return (
    <Picker.Root
      className="ep-picker"
      emojiData={emojiData}
      /* native keeps the library off its hard-coded jsDelivr sprite URLs; the
          visible glyphs come from SpriteCell reading the local atlas. */
      emojiStyle="native"
      columns={columns}
      suggestedEmojisMode="frequent"
      components={components}
      categoryIcons={categoryIcons}
      labels={labels}
      onEmojiClick={onPick}
      appearance="default"
      colorScheme="light"
    >
      <Picker.Search />
      <Picker.CategoryNav />
      <Picker.Viewport>
        <Picker.List />
        <Picker.Empty />
        <Picker.Loading />
        <Picker.LoadError />
      </Picker.Viewport>
      <Picker.SkinTone />
    </Picker.Root>
  );
}
