import type { ReactNode } from 'react'

import { cn } from '@/lib/utils'

export type StatTone = 'muted' | 'success' | 'warning' | 'destructive'

/**
 * The rail is a severity signal, so its vocabulary is not the caption's: there is no `muted` rail
 * (a grey rail says "this is a rail" and nothing else), and there IS a `primary` one, which the
 * caption has no use for because volt text on a white card fails contrast.
 */
export type StatRailTone = 'primary' | 'success' | 'warning' | 'destructive'

const CAPTION_TONE_CLASSES: Record<StatTone, string> = {
  muted: 'text-muted-foreground',
  success: 'text-success',
  warning: 'text-warning',
  destructive: 'text-destructive',
}

const RAIL_CLASSES: Record<StatRailTone, string> = {
  primary: 'rail-primary',
  success: 'rail-success',
  warning: 'rail-warning',
  destructive: 'rail-destructive',
}

interface StatTileProps {
  label: string
  value: string
  /** One line of context under the number. Plain text unless it can link somewhere real. */
  caption?: ReactNode
  captionTone?: StatTone
  /**
   * Severity rail on the leading edge — omit it on any tile where nothing is escalating. A rail on
   * every card is the same as a rail on none: the point is that a rail means "look here".
   */
  tone?: StatRailTone
}

/**
 * The console's headline-number tile: eyebrow, one big Archivo number, one line of context.
 *
 * There is deliberately no trend-delta slot and no icon. The delta is missing because no endpoint in
 * this system reports a previous period — a decorative percentage is the single worst thing to fake
 * on a dashboard, because it is the number people act on hardest. The icon is missing because the
 * redesign dropped it: a tinted glyph beside every figure was decoration that made four tiles harder
 * to scan, not easier. (The older icon-bearing `shared/components/StatCard` is still used by the
 * member portal, which is a different surface with a different visual language.) There is no sparkline
 * for the same reason as the delta: one snapshot cannot draw a line.
 */
export function StatTile({ label, value, caption, captionTone = 'muted', tone }: StatTileProps) {
  return (
    <div
      className={cn(
        'flex flex-col rounded-panel border border-border bg-card p-5',
        // The rail and the light-surface edge light are both box-shadow, so a tile takes exactly one
        // of them rather than relying on which utility happens to win the cascade. Where a rail is
        // present it is the louder and more useful of the two.
        tone ? RAIL_CLASSES[tone] : 'edge-light-soft',
      )}
    >
      <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">{label}</p>
      <p className="mt-3 font-display text-4xl leading-none font-black tracking-tight tabular-nums">{value}</p>
      {/* mt-auto so captions line up across the row even when one card's number wraps. */}
      {caption && <div className={cn('mt-auto pt-4 text-sm', CAPTION_TONE_CLASSES[captionTone])}>{caption}</div>}
    </div>
  )
}
