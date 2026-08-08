import { cn } from '@/lib/utils'

export interface FilterTab<T extends string> {
  key: T
  label: string
  /**
   * Undefined renders no count at all, which is the point: a tab whose count query hasn't answered
   * shows a bare label rather than a "0" that is about to become a 47. A blank beats a wrong zero.
   */
  count?: number
  /** Optional tone for the count, e.g. overdue in destructive. Defaults to muted. */
  countClassName?: string
}

/**
 * The console's underline tab row, used for status filters over a list.
 *
 * Deliberately not the shadcn `Tabs` primitive: these do not switch panels, they narrow one list
 * that stays mounted underneath, and they carry counts. Radix's roving-tabindex and panel semantics
 * would describe something this isn't.
 */
export function FilterTabs<T extends string>({
  tabs,
  active,
  onChange,
  className,
}: {
  tabs: readonly FilterTab<T>[]
  active: T
  onChange: (key: T) => void
  className?: string
}) {
  return (
    <div className={cn('flex flex-wrap items-center gap-5 border-b border-border', className)}>
      {tabs.map((tab) => {
        const isActive = active === tab.key
        return (
          <button
            key={tab.key}
            type="button"
            onClick={() => onChange(tab.key)}
            aria-current={isActive ? 'true' : undefined}
            className={cn(
              '-mb-px border-b-2 pb-2.5 text-sm font-medium transition-colors',
              isActive
                ? 'border-foreground text-foreground'
                : 'border-transparent text-muted-foreground hover:text-foreground',
            )}
          >
            {tab.label}
            {tab.count !== undefined && (
              <span className={cn('ml-1.5 tabular-nums', tab.countClassName ?? 'text-muted-foreground')}>
                {tab.count.toLocaleString()}
              </span>
            )}
          </button>
        )
      })}
    </div>
  )
}
