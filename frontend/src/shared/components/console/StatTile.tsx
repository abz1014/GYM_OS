import type { ReactNode } from 'react'

import { cn } from '@/lib/utils'

export type StatTone = 'muted' | 'success' | 'warning' | 'destructive'

const CAPTION_TONE_CLASSES: Record<StatTone, string> = {
  muted: 'text-muted-foreground',
  success: 'text-success',
  warning: 'text-warning',
  destructive: 'text-destructive',
}

interface StatTileProps {
  label: string
  value: string
  /** One line of context under the number. Plain text unless it can link somewhere real. */
  caption?: ReactNode
  captionTone?: StatTone
}

/**
 * The console's headline-number tile: eyebrow, one big Archivo number, one line of context.
 *
 * There is deliberately no trend-delta slot and no icon. The delta is missing because no endpoint in
 * this system reports a previous period — a decorative percentage is the single worst thing to fake
 * on a dashboard, because it is the number people act on hardest. The icon is missing because the
 * redesign dropped it: a tinted glyph beside every figure was decoration that made four tiles harder
 * to scan, not easier. (The older icon-bearing `shared/components/StatCard` is still used by the
 * member portal, which is a different surface with a different visual language.)
 */
export function StatTile({ label, value, caption, captionTone = 'muted' }: StatTileProps) {
  return (
    <div className="flex flex-col rounded-2xl border border-border bg-card p-5 shadow-sm">
      <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">{label}</p>
      <p className="mt-3 font-display text-4xl leading-none font-black tracking-tight tabular-nums">{value}</p>
      {/* mt-auto so captions line up across the row even when one card's number wraps. */}
      {caption && <div className={cn('mt-auto pt-4 text-sm', CAPTION_TONE_CLASSES[captionTone])}>{caption}</div>}
    </div>
  )
}
