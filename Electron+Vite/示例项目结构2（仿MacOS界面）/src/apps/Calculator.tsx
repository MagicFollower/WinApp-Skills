import { useCallback, useEffect, useState } from 'react'

type Op = '+' | '-' | '×' | '÷'

const KEYS: { label: string; kind: 'fn' | 'op' | 'eq' | 'num'; value: string; wide?: boolean }[] = [
  { label: 'AC', kind: 'fn', value: 'ac' },
  { label: '+/−', kind: 'fn', value: 'sign' },
  { label: '%', kind: 'fn', value: 'pct' },
  { label: '÷', kind: 'op', value: '/' },
  { label: '7', kind: 'num', value: '7' },
  { label: '8', kind: 'num', value: '8' },
  { label: '9', kind: 'num', value: '9' },
  { label: '×', kind: 'op', value: '*' },
  { label: '4', kind: 'num', value: '4' },
  { label: '5', kind: 'num', value: '5' },
  { label: '6', kind: 'num', value: '6' },
  { label: '−', kind: 'op', value: '-' },
  { label: '1', kind: 'num', value: '1' },
  { label: '2', kind: 'num', value: '2' },
  { label: '3', kind: 'num', value: '3' },
  { label: '+', kind: 'op', value: '+' },
  { label: '0', kind: 'num', value: '0', wide: true },
  { label: '.', kind: 'num', value: '.' },
  { label: '=', kind: 'eq', value: '=' }
]

const OP_OF: Record<string, Op | undefined> = { '+': '+', '-': '-', '*': '×', '/': '÷' }
const OP_INPUT: Record<string, string> = { '+': '+', '-': '-', '*': '*', '/': '/' }

function fmt(n: number): string {
  if (!Number.isFinite(n)) return '错误'
  const rounded = Number(n.toPrecision(12))
  return String(rounded)
}

function compute(a: number, b: number, op: Op): number {
  switch (op) {
    case '+':
      return a + b
    case '-':
      return a - b
    case '×':
      return a * b
    case '÷':
      return b === 0 ? NaN : a / b
  }
}

export default function Calculator() {
  const [display, setDisplay] = useState('0')
  const [acc, setAcc] = useState<number | null>(null)
  const [op, setOp] = useState<Op | null>(null)
  const [fresh, setFresh] = useState(true)
  const [hint, setHint] = useState('')

  const press = useCallback(
    (value: string) => {
      const current = Number(display)

      if (value === 'ac') {
        setDisplay('0')
        setAcc(null)
        setOp(null)
        setFresh(true)
        setHint('')
        return
      }
      if (value === 'sign') {
        setDisplay(fmt(-current))
        return
      }
      if (value === 'pct') {
        setDisplay(fmt(current / 100))
        return
      }

      const nextOp = OP_OF[value]
      if (nextOp) {
        if (acc !== null && op && !fresh) {
          const folded = compute(acc, current, op)
          setAcc(folded)
          setDisplay(fmt(folded))
          setHint(`${fmt(folded)} ${nextOp}`)
        } else {
          setAcc(current)
          setHint(`${fmt(current)} ${nextOp}`)
        }
        setOp(nextOp)
        setFresh(true)
        return
      }

      if (value === '=') {
        if (acc !== null && op) {
          const result = compute(acc, current, op)
          setHint(`${fmt(acc)} ${op} ${fmt(current)} =`)
          setDisplay(fmt(result))
          setAcc(null)
          setOp(null)
          setFresh(true)
        }
        return
      }

      // 数字与小数点
      if (value === '.') {
        if (fresh) {
          setDisplay('0.')
          setFresh(false)
        } else if (!display.includes('.')) {
          setDisplay(display + '.')
        }
        return
      }

      if (fresh) {
        setDisplay(value)
        setFresh(false)
      } else if (display.replace(/[-.]/g, '').length < 12) {
        setDisplay(display === '0' ? value : display + value)
      }
    },
    [acc, display, fresh, op]
  )

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement | null
      const tag = target?.tagName ?? ''
      if (tag === 'INPUT' || tag === 'TEXTAREA') return
      if (/^[0-9]$/.test(e.key)) return press(e.key)
      if (e.key === '.') return press('.')
      if (OP_INPUT[e.key]) return press(OP_INPUT[e.key])
      if (e.key === 'Enter' || e.key === '=') return press('=')
      if (e.key === 'Escape' || e.key.toLowerCase() === 'c') return press('ac')
      if (e.key === '%') return press('pct')
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [press])

  return (
    <div className="calc">
      <div className="calc-display" aria-live="polite">
        {display}
      </div>
      <div className="calc-hint">{hint || '键盘可输入 0-9 + − * / Enter Esc'}</div>
      <div className="calc-grid">
        {KEYS.map(k => (
          <button
            type="button"
            key={k.value + k.label}
            className={`calc-key ${k.kind} ${k.wide ? 'is-wide' : ''}`}
            onClick={() => press(k.value)}
          >
            {k.label}
          </button>
        ))}
      </div>
    </div>
  )
}
