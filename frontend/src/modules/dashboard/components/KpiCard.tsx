import type { ReactNode } from 'react'

import { cn } from '@/lib/utils'

export type KpiCaptionTone = 'muted' | 'success' | 'warning' | 'destructive'

const CAPTION_TONE_CLASSES: Record<KpiCaptionTone, string> = {
  muted: 'text-muted-foreground',
  success: 'text-success',
  warning: 'text-warning',
  destructive: 'text-destructive',
}

interface KpiCardProps {
  label: string
  value: string
  /** One line of context under the number. Plain text unless it can link somewhere real — see DashboardPage. */
  caption?: ReactNode
  captionTone?: KpiCaptionTone
}

/**
 * The redesign's KPI tile: eyebrow, one big Archivo number, one line of context.
 *
 * There is deliberately no trend-delta slot. Every mockup tile carried a coloured percentage chip
 * ("3.2%", "1.8%"), and nothing in this system can produce one: /api/dashboard/summary returns a
 * single snapshot with no previous-period figures, and no other endpoint reports member counts or
 * churn over time. A delta is the one number on a dashboard people act on hardest, so a decorative
 * one is worse than none.
 */
export function KpiCard({ label, value, caption, captionTone = 'muted' }: KpiCardProps) {
  return (
    <div className="flex flex-col rounded-2xl border border-border bg-card p-5 shadow-sm">
      <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">{label}</p>
      <p className="mt-3 font-display text-4xl leading-none font-black tracking-tight tabular-nums">{value}</p>
      {/* mt-auto so captions line up across the row even when one card's number wraps. */}
      {caption && <div className={cn('mt-auto pt-4 text-sm', CAPTION_TONE_CLASSES[captionTone])}>{caption}</div>}
    </div>
  )
}
