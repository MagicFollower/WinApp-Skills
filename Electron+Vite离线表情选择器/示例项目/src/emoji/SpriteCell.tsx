import type { EmojiRenderProps } from 'emoji-picker-react/primitives';
import { spriteStyle } from './atlas';

const GLYPH_PX = 30;

/**
 * `managed` carries the library's own type/class/position style/tabIndex/
 * aria-label/data-epr-* — those are what selection, keyboard navigation,
 * virtualization and accessibility ride on, so they stay on the button.
 */
export function SpriteCell({ emoji, children, ...managed }: EmojiRenderProps) {
  const sprite = spriteStyle(emoji.unified, GLYPH_PX);
  return (
    <button {...managed}>
      {sprite ? <span className="ep-glyph" style={sprite} /> : children}
    </button>
  );
}

export { GLYPH_PX };
