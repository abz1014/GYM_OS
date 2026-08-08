import type { ReactNode } from 'react'

/**
 * The staff console's page title block.
 *
 * This exists because the redesign's heading is not one class but four working together
 * (`font-display` + `text-3xl` + `font-black` + `tracking-tight`), and it was already written out
 * by hand on Dashboard, Members and Front desk. Twelve more hand-copies is how a design language
 * drifts: one page ends up `text-2xl`, another keeps `font-semibold`, and the console stops looking
 * like one product. Changing the console's voice should be one edit here.
 */
export function PageHeader({
  eyebrow,
  title,
  description,
  actions,
}: {
  /** Small caps line above the title — usually the branch or section this page is scoped to. */
  eyebrow?: ReactNode
  title: ReactNode
  /** One line under the title. Keep it to what the page is for, not what it contains. */
  description?: ReactNode
  /** Primary action(s), right-aligned. Wraps under the title on narrow screens. */
  actions?: ReactNode
}) {
  return (
    <header className="flex flex-wrap items-start justify-between gap-3">
      <div className="min-w-0">
        {eyebrow && <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">{eyebrow}</p>}
        <h1 className="font-display text-3xl leading-tight font-black tracking-tight">{title}</h1>
        {description && <p className="mt-1 text-sm text-muted-foreground">{description}</p>}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </header>
  )
}
