## Integrate the <HoldButton /> component from React Bits

You are helping integrate an open-source React component into an existing application.

### Component: HoldButton
### Variant: JavaScript + CSS


---

### Usage Example
```jsx
import HoldButton from './HoldButton';

<HoldButton
  doneLabel="Deleted"
  backgroundColor="#27272a"
  fillColor="#5227FF"
  textColor="#f5f5f5"
  fillTextColor="#ffffff"
  size="md"
  radius={14}
  fillDirection="right"
  holdTime={1000}
  releaseTime={230}
  pressScale={1}
  wave
  waveAmplitude={9}
  glow
  resetAfter={1000}
  onHold={() => console.log('confirmed')}
>
  Hold to delete
</HoldButton>
```

### Props
| Prop            | Type       | Default          | Description                                                  |
| --------------- | ---------- | ---------------- | ------------------------------------------------------------ |
| children        | ReactNode  | "Hold to delete" | Label shown while idle and holding.                          |
| doneLabel       | ReactNode  | "Deleted"        | Label that blurs in when the hold completes.                 |
| icon            | ReactNode  | null             | Optional icon rendered before the idle label.                |
| doneIcon        | ReactNode  | null             | Optional icon rendered before the done label.                |
| backgroundColor | string     | "#27272a"        | Button body colour.                                          |
| fillColor       | string     | "#5227FF"        | Colour of the liquid fill, its wave, the glow and the focus ring. |
| textColor       | string     | "#f5f5f5"        | Label colour outside the fill.                               |
| fillTextColor   | string     | "#ffffff"        | Label colour inside the fill; the ink inverts as the edge crosses it. |
| size            | "sm"       | "md"             | "lg"                                                         |
| radius          | number     | 14               | Corner radius in pixels of the body, the fill and the focus ring. |
| fillDirection   | "right"    | "up"             | "right"                                                      |
| holdTime        | number     | 2000             | How long the press must last, in milliseconds. The fill moves at constant speed. |
| releaseTime     | number     | 200              | How fast the fill snaps back on an early release or a reset, in milliseconds. |
| pressScale      | number     | 0.97             | Squash of the button while a pointer holds it. 1 disables it. |
| wave            | boolean    | true             | Scrolling meniscus on the leading edge of the fill.          |
| waveAmplitude   | number     | 6                | Height of the wave crests in pixels.                         |
| glow            | boolean    | true             | Glow that charges with the hold and pulses once on completion. |
| resetAfter      | number     | 1200             | Milliseconds the done state stays before the button resets. 0 keeps it done. |
| disabled        | boolean    | false            | Dims the button and ignores input.                           |
| onHold          | () => void | -                | Called once, on the frame the fill completes.                |
| onTap           | () => void | -                | Called on a release shorter than 250ms that did not drift away. |
| className       | string     | ""               | Extra classes for the button.                                |

### Full Component Source
```jsx
'use client';

import { useEffect, useId, useLayoutEffect, useRef, useState } from 'react';

import './HoldButton.css';

const TAP_MS = 250;
const HIT_PAD = 10;
const LINEAR = t => t;
const EASE_OUT = t => 1 - Math.pow(1 - t, 3);

export default function HoldButton({
  children = 'Hold to delete',
  doneLabel = 'Deleted',
  icon = null,
  doneIcon = null,
  backgroundColor = '#27272a',
  fillColor = '#5227FF',
  textColor = '#f5f5f5',
  fillTextColor = '#ffffff',
  size = 'md',
  radius = 14,
  fillDirection = 'right',
  holdTime = 2000,
  releaseTime = 200,
  pressScale = 0.97,
  wave = true,
  waveAmplitude = 6,
  glow = true,
  resetAfter = 1200,
  disabled = false,
  onHold,
  onTap,
  className = ''
}) {
  const [phase, setPhase] = useState('idle');
  const [input, setInput] = useState(null);
  const phaseRef = useRef('idle');
  const inputRef = useRef(null);
  const buttonRef = useRef(null);
  const gesture = useRef({ pointerId: null, start: 0, rect: null });
  const timers = useRef({ complete: 0, reset: 0 });
  const hintId = useId();

  const go = (next, kind = null) => {
    phaseRef.current = next;
    inputRef.current = kind;
    setPhase(next);
    setInput(kind);
  };

  const clearTimers = () => {
    clearTimeout(timers.current.complete);
    clearTimeout(timers.current.reset);
  };

  const motion = useRef({ raf: 0, p: 0, from: 0, to: 0, start: 0 });
  const drive = (to, duration, ease) => {
    const m = motion.current;
    cancelAnimationFrame(m.raf);
    m.from = m.p;
    m.to = to;
    m.start = performance.now();
    const step = now => {
      const t = duration > 0 ? Math.min(1, (now - m.start) / duration) : 1;
      m.p = m.from + (m.to - m.from) * ease(t);
      buttonRef.current?.style.setProperty('--hb-p', m.p.toFixed(4));
      if (t < 1) {
        m.raf = requestAnimationFrame(step);
        return;
      }
      m.raf = 0;
      if (m.to === 1) complete();
    };
    m.raf = requestAnimationFrame(step);
  };

  const complete = () => {
    if (phaseRef.current !== 'holding') return;
    if (performance.now() - gesture.current.start < holdTime - 50) return;
    clearTimers();
    go('done', inputRef.current);
    onHold?.();
    if (resetAfter > 0) {
      timers.current.reset = setTimeout(() => {
        go('idle');
        drive(0, releaseTime, EASE_OUT);
      }, resetAfter);
    }
  };

  const begin = kind => {
    if (disabled || phaseRef.current !== 'idle') return false;
    const button = buttonRef.current;
    if (!button) return false;
    gesture.current.start = performance.now();
    gesture.current.rect = button.getBoundingClientRect();
    go('holding', kind);
    drive(1, holdTime, LINEAR);
    timers.current.complete = setTimeout(complete, holdTime + 100);
    return true;
  };

  const release = ({ drifted = false } = {}) => {
    if (phaseRef.current !== 'holding') return;
    clearTimers();
    const held = performance.now() - gesture.current.start;
    go('idle');
    drive(0, releaseTime, EASE_OUT);
    if (!drifted && held < TAP_MS) onTap?.();
  };
  const releaseRef = useRef(release);
  releaseRef.current = release;

  const handlePointerDown = e => {
    if (e.button !== 0 || !e.isPrimary || gesture.current.pointerId !== null) return;
    if (!begin('pointer')) return;
    gesture.current.pointerId = e.pointerId;
    try {
      e.currentTarget.setPointerCapture(e.pointerId);
    } catch {}
  };

  const endPointer = (e, options) => {
    if (e.pointerId !== gesture.current.pointerId) return;
    gesture.current.pointerId = null;
    try {
      if (e.currentTarget.hasPointerCapture(e.pointerId)) e.currentTarget.releasePointerCapture(e.pointerId);
    } catch {}
    release(options);
  };

  const handlePointerMove = e => {
    if (e.pointerId !== gesture.current.pointerId) return;
    const r = gesture.current.rect;
    if (!r) return;
    const out =
      e.clientX < r.left - HIT_PAD ||
      e.clientX > r.right + HIT_PAD ||
      e.clientY < r.top - HIT_PAD ||
      e.clientY > r.bottom + HIT_PAD;
    if (out) endPointer(e, { drifted: true });
  };

  const handlePointerLeave = e => {
    if (e.pointerType !== 'touch') endPointer(e, { drifted: true });
  };

  const handleKeyDown = e => {
    if (e.key === 'Escape') {
      if (inputRef.current === 'key') release({ drifted: true });
      return;
    }
    if (e.key === ' ' || e.key === 'Enter') {
      e.preventDefault();
      if (!e.repeat) begin('key');
    }
  };

  const handleKeyUp = e => {
    if (e.key === ' ' || e.key === 'Enter') {
      e.preventDefault();
      if (inputRef.current === 'key') release();
    }
  };

  useLayoutEffect(() => {
    const button = buttonRef.current;
    if (!button) return undefined;
    const measure = () => {
      button.style.setProperty('--hb-w', `${button.offsetWidth}px`);
      button.style.setProperty('--hb-h', `${button.offsetHeight}px`);
    };
    measure();
    const ro = new ResizeObserver(measure);
    ro.observe(button);
    return () => ro.disconnect();
  }, []);

  useEffect(() => {
    if (phase !== 'holding') return undefined;
    const cancel = () => releaseRef.current({ drifted: true });
    const onVisibility = () => {
      if (document.hidden) cancel();
    };
    window.addEventListener('blur', cancel);
    document.addEventListener('visibilitychange', onVisibility);
    return () => {
      window.removeEventListener('blur', cancel);
      document.removeEventListener('visibilitychange', onVisibility);
    };
  }, [phase]);

  useEffect(() => {
    const t = timers.current;
    const m = motion.current;
    return () => {
      clearTimeout(t.complete);
      clearTimeout(t.reset);
      cancelAnimationFrame(m.raf);
    };
  }, []);

  const direction = fillDirection === 'up' ? 'up' : 'right';
  const labels = (
    <>
      <span className="hold-button__idle" aria-hidden={phase === 'done'}>
        {icon ? <span className="hold-button__icon">{icon}</span> : null}
        {children}
      </span>
      <span className="hold-button__done" aria-hidden={phase !== 'done'}>
        {doneIcon ? <span className="hold-button__icon">{doneIcon}</span> : null}
        {doneLabel}
      </span>
    </>
  );

  return (
    <button
      ref={buttonRef}
      type="button"
      disabled={disabled}
      className={`hold-button hold-button--${size}${className ? ` ${className}` : ''}`}
      data-phase={phase}
      data-input={input ?? undefined}
      data-direction={direction}
      data-glow={glow ? 'true' : undefined}
      aria-describedby={hintId}
      style={{
        '--hb-radius': `${radius}px`,
        '--hb-bg': backgroundColor,
        '--hb-fill': fillColor,
        '--hb-text': textColor,
        '--hb-fill-text': fillTextColor,
        '--hb-hold': `${holdTime}ms`,
        '--hb-cycles': holdTime / 1100,
        '--hb-release': `${releaseTime}ms`,
        '--hb-press': pressScale,
        '--hb-wave': `${wave ? waveAmplitude : 0}px`
      }}
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={e => endPointer(e)}
      onPointerCancel={e => endPointer(e, { drifted: true })}
      onLostPointerCapture={e => endPointer(e, { drifted: true })}
      onPointerLeave={handlePointerLeave}
      onKeyDown={handleKeyDown}
      onKeyUp={handleKeyUp}
      onContextMenu={e => e.preventDefault()}
    >
      <span className="hold-button__pulse" aria-hidden="true" />
      <span className="hold-button__label">{labels}</span>
      <span className="hold-button__clip" aria-hidden="true">
        <span className="hold-button__fill">
          <span className="hold-button__label hold-button__label--fill">{labels}</span>
        </span>
        <span className="hold-button__crest" aria-hidden="true">
          <span className="hold-button__label hold-button__label--fill">{labels}</span>
        </span>
      </span>
      <span id={hintId} className="hold-button__sr">
        Press and hold for {Math.round(holdTime / 100) / 10} seconds to confirm
      </span>
    </button>
  );
}

```

### Component CSS
```css
.hold-button {
  --hb-radius: 14px;
  --hb-bg: #27272a;
  --hb-fill: #5227ff;
  --hb-text: #f5f5f5;
  --hb-fill-text: #ffffff;
  --hb-hold: 2000ms;
  --hb-release: 200ms;
  --hb-press: 0.97;
  --hb-wave: 6px;
  --hb-w: 0px;
  --hb-h: 0px;
  --hb-cycles: 2;
  --hb-p: 0;
  --hb-ease-out: cubic-bezier(0.23, 1, 0.32, 1);
  --hb-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.06);
  --hb-glow:
    inset 0 1px 0 rgba(255, 255, 255, 0.06), 0 10px 32px -6px color-mix(in srgb, var(--hb-fill) 70%, transparent);

  position: relative;
  display: inline-grid;
  place-items: center;
  isolation: isolate;
  margin: 0;
  border: 0;
  border-radius: var(--hb-radius);
  background: var(--hb-bg);
  color: var(--hb-text);
  font-family: inherit;
  font-weight: 500;
  letter-spacing: 0.01em;
  line-height: 1;
  box-shadow: var(--hb-shadow);
  cursor: pointer;
  outline: none;
  user-select: none;
  -webkit-user-select: none;
  -webkit-touch-callout: none;
  -webkit-tap-highlight-color: transparent;
  touch-action: manipulation;
  transition:
    transform 160ms var(--hb-ease-out),
    background-color 160ms ease,
    box-shadow var(--hb-release) var(--hb-ease-out);
}

.hold-button--sm {
  height: 36px;
  padding: 0 16px;
  font-size: 13px;
}

.hold-button--md {
  height: 44px;
  padding: 0 22px;
  font-size: 15px;
}

.hold-button--lg {
  height: 52px;
  padding: 0 28px;
  font-size: 17px;
}

@media (hover: hover) and (pointer: fine) {
  .hold-button:not(:disabled):hover {
    background: color-mix(in srgb, var(--hb-bg) 92%, #fff);
  }
}

.hold-button[data-phase='holding'][data-input='pointer'] {
  transform: scale(var(--hb-press));
}

.hold-button[data-glow='true'][data-phase='holding'],
.hold-button[data-glow='true'][data-phase='done'] {
  box-shadow: var(--hb-glow);
}

.hold-button[data-glow='true'][data-phase='holding'] {
  transition:
    transform 160ms var(--hb-ease-out),
    background-color 160ms ease,
    box-shadow var(--hb-hold) linear;
}

.hold-button:focus-visible {
  outline: 2px solid var(--hb-fill);
  outline-offset: 3px;
}

.hold-button:disabled {
  opacity: 0.5;
  cursor: default;
  pointer-events: none;
}

.hold-button__pulse {
  position: absolute;
  inset: 0;
  z-index: 0;
  border-radius: var(--hb-radius);
  pointer-events: none;
  opacity: 0;
}

.hold-button[data-glow='true'][data-phase='done'] .hold-button__pulse {
  animation: hb-pulse 600ms var(--hb-ease-out) forwards;
}

@keyframes hb-pulse {
  from {
    opacity: 1;
    box-shadow: 0 0 0 0 color-mix(in srgb, var(--hb-fill) 55%, transparent);
  }
  to {
    opacity: 0;
    box-shadow: 0 0 0 14px color-mix(in srgb, var(--hb-fill) 0%, transparent);
  }
}

.hold-button__label {
  position: relative;
  z-index: 2;
  display: grid;
  place-items: center;
}

.hold-button__label > span {
  grid-area: 1 / 1;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  white-space: nowrap;
  transition:
    opacity 200ms ease,
    filter 200ms ease;
}

.hold-button__icon {
  display: inline-flex;
  flex: none;
}

.hold-button__icon > svg {
  display: block;
}

.hold-button__done {
  opacity: 0;
  filter: blur(2px);
}

.hold-button[data-phase='done'] .hold-button__idle {
  opacity: 0;
  filter: blur(2px);
}

.hold-button[data-phase='done'] .hold-button__done {
  opacity: 1;
  filter: blur(0);
}

.hold-button__clip {
  position: absolute;
  inset: 0;
  z-index: 3;
  clip-path: inset(0 round var(--hb-radius));
  pointer-events: none;
}

.hold-button__fill {
  position: absolute;
  inset: 0;
  display: grid;
  place-items: center;
  background: var(--hb-fill);
  color: var(--hb-fill-text);
  clip-path: inset(
    0 calc((1 - var(--hb-p)) * (100% + 0.75 * var(--hb-wave)) - var(--hb-p) * 0.25 * var(--hb-wave)) 0 0
  );
}

.hold-button[data-direction='up'] .hold-button__fill {
  clip-path: inset(
    calc((1 - var(--hb-p)) * (100% + 0.75 * var(--hb-wave)) - var(--hb-p) * 0.25 * var(--hb-wave)) 0 0 0
  );
}

.hold-button__crest {
  position: absolute;
  inset: 0;
  display: grid;
  place-items: center;
  background: var(--hb-fill);
  color: var(--hb-fill-text);
  -webkit-mask-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='20' height='200' viewBox='0 0 20 200' preserveAspectRatio='none'%3E%3Cpath d='M0 0H10C18 8 18 25.3 10 33.3S2 58.7 10 66.7S18 92 10 100S2 125.3 10 133.3S18 158.7 10 166.7S2 192 10 200H0Z'/%3E%3C/svg%3E");
  mask-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='20' height='200' viewBox='0 0 20 200' preserveAspectRatio='none'%3E%3Cpath d='M0 0H10C18 8 18 25.3 10 33.3S2 58.7 10 66.7S18 92 10 100S2 125.3 10 133.3S18 158.7 10 166.7S2 192 10 200H0Z'/%3E%3C/svg%3E");
  -webkit-mask-repeat: repeat-y;
  mask-repeat: repeat-y;
  -webkit-mask-size: var(--hb-wave) calc(var(--hb-h) * 2);
  mask-size: var(--hb-wave) calc(var(--hb-h) * 2);
  -webkit-mask-position-x: calc(-1 * var(--hb-wave) + var(--hb-p) * (var(--hb-w) + var(--hb-wave)));
  mask-position-x: calc(-1 * var(--hb-wave) + var(--hb-p) * (var(--hb-w) + var(--hb-wave)));
  -webkit-mask-position-y: calc(-1 * var(--hb-p) * var(--hb-cycles) * var(--hb-h));
  mask-position-y: calc(-1 * var(--hb-p) * var(--hb-cycles) * var(--hb-h));
}

.hold-button[data-direction='up'] .hold-button__crest {
  -webkit-mask-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='200' height='20' viewBox='0 0 200 20' preserveAspectRatio='none'%3E%3Cpath d='M0 20V10C12 2 38 2 50 10S88 18 100 10S138 2 150 10S188 18 200 10V20Z'/%3E%3C/svg%3E");
  mask-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='200' height='20' viewBox='0 0 200 20' preserveAspectRatio='none'%3E%3Cpath d='M0 20V10C12 2 38 2 50 10S88 18 100 10S138 2 150 10S188 18 200 10V20Z'/%3E%3C/svg%3E");
  -webkit-mask-repeat: repeat-x;
  mask-repeat: repeat-x;
  -webkit-mask-size: calc(var(--hb-w) * 2) var(--hb-wave);
  mask-size: calc(var(--hb-w) * 2) var(--hb-wave);
  -webkit-mask-position-x: calc(-1 * var(--hb-p) * var(--hb-cycles) * var(--hb-w));
  mask-position-x: calc(-1 * var(--hb-p) * var(--hb-cycles) * var(--hb-w));
  -webkit-mask-position-y: calc(var(--hb-h) - var(--hb-p) * (var(--hb-h) + var(--hb-wave)));
  mask-position-y: calc(var(--hb-h) - var(--hb-p) * (var(--hb-h) + var(--hb-wave)));
}

.hold-button__label--fill {
  z-index: auto;
}

.hold-button__sr {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}

@media (prefers-contrast: more) {
  .hold-button {
    outline: 1px solid var(--hb-text);
  }
}

@media (prefers-reduced-motion: reduce) {
  .hold-button {
    transition:
      background-color 160ms ease,
      box-shadow var(--hb-release) ease;
    transform: none !important;
  }

  .hold-button__fill,
  .hold-button[data-direction='up'] .hold-button__fill {
    clip-path: inset(0);
    opacity: 0;
    transition: opacity var(--hb-release) ease;
  }

  .hold-button__crest {
    display: none;
  }

  .hold-button[data-phase='holding'] .hold-button__fill,
  .hold-button[data-phase='done'] .hold-button__fill {
    opacity: 1;
    transition: opacity var(--hb-hold) linear;
  }

  .hold-button__pulse {
    animation: none !important;
  }

  .hold-button__label > span {
    transition: opacity 200ms ease;
    filter: none !important;
  }
}

```

### Integration Instructions
1. Install any listed dependencies.
2. Copy the component source into the appropriate directory in the project.
3. Import the CSS file alongside the component.
4. Import and render the component using the usage example above as a starting point.
5. Adjust props as needed for the specific use case — refer to the props table for all available options.

### More from React Bits
The full library index, including everything reactbits.dev offers, is at https://reactbits.dev/llms.txt — fetch it if this component is not the right fit or the project needs more pieces.