import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { CornerDownLeft, Receipt, Search, Users } from 'lucide-react'

import { Dialog, DialogContent, DialogTitle } from '@/components/ui/dialog'
import { cn } from '@/lib/utils'
import { useAuthStore } from '@/stores/authStore'
import { buildCommands, filterCommands } from '@/shared/commands/commandRegistry'
import { useGlobalSearch, type SearchHit } from '@/shared/search/searchApi'

/** One selectable line, flattened so the keyboard walks a single list regardless of grouping. */
interface Row {
  key: string
  label: string
  hint?: string | null
  group: string
  route: string
  icon: React.ComponentType<{ className?: string }>
}

/**
 * ⌘K. One box for "take me to a thing" and "take me to a screen".
 *
 * **Records first, screens second.** The two source lists are not interchangeable: commands are a
 * fixed set the person could also reach from the sidebar, whereas a search hit is a specific member
 * or invoice that is otherwise several clicks and a filter away. Ranking records above screens means
 * the palette earns its keystroke on the case the sidebar cannot serve.
 *
 * **Keyboard is the point.** The whole list is flattened into `rows` precisely so ↑/↓ traverse it
 * without the caret hopping between groups, Enter opens the highlighted row, and Escape closes. A
 * palette that needs the mouse is a menu, and this app already has one of those down the left side.
 *
 * Everything shown here is already permission-filtered: commands by `buildCommands`, and hits by the
 * server, which returns only the groups the caller can read. There is no gating in this component
 * and there should not be — see the remarks on `useGlobalSearch`.
 */
export function CommandPalette({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const navigate = useNavigate()
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const roles = useAuthStore((s) => s.user?.roles)

  const [term, setTerm] = useState('')
  const [activeIndex, setActiveIndex] = useState(0)
  const listRef = useRef<HTMLDivElement>(null)

  const search = useGlobalSearch(term)

  const commands = useMemo(() => buildCommands(hasPermission, roles ?? []), [hasPermission, roles])

  const rows = useMemo<Row[]>(() => {
    const hitRows = (hits: SearchHit[], group: string, icon: Row['icon']): Row[] =>
      hits.map((h) => ({ key: `${group}:${h.id}`, label: h.title, hint: h.subtitle, group, route: h.route, icon }))

    const result = search.data
    const records: Row[] = result
      ? [
          ...hitRows(result.members, 'Members', Users),
          ...hitRows(result.invoices, 'Invoices', Receipt),
          ...hitRows(result.classes, 'Classes', Search),
        ]
      : []

    const screens: Row[] = filterCommands(commands, term).map((c) => ({
      key: c.id,
      label: c.label,
      group: c.group,
      route: c.route,
      icon: c.icon,
    }))

    return [...records, ...screens]
  }, [search.data, commands, term])

  // Reset to the top whenever the candidate set changes, otherwise the highlight can sit on an index
  // that no longer exists and Enter opens whatever slid into that position.
  useEffect(() => {
    setActiveIndex(0)
  }, [term, search.data])

  // Clearing on close rather than on open: the palette should never flash the previous query's
  // results for a frame while the new state settles.
  useEffect(() => {
    if (!open) {
      setTerm('')
      setActiveIndex(0)
    }
  }, [open])

  useEffect(() => {
    listRef.current?.querySelector('[data-active="true"]')?.scrollIntoView({ block: 'nearest' })
  }, [activeIndex])

  const go = (route: string) => {
    onOpenChange(false)
    navigate(route)
  }

  const onKeyDown = (event: React.KeyboardEvent) => {
    if (event.key === 'ArrowDown') {
      event.preventDefault()
      setActiveIndex((i) => (rows.length === 0 ? 0 : (i + 1) % rows.length))
    } else if (event.key === 'ArrowUp') {
      event.preventDefault()
      setActiveIndex((i) => (rows.length === 0 ? 0 : (i - 1 + rows.length) % rows.length))
    } else if (event.key === 'Enter') {
      event.preventDefault()
      const row = rows[activeIndex]
      if (row) {
        go(row.route)
      }
    }
  }

  // Group headings are rendered by comparing against the previous row rather than by nesting, so the
  // flat keyboard list and the grouped display stay the same array.
  let lastGroup: string | null = null

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className="top-[12%] max-w-xl translate-y-0 gap-0 overflow-hidden p-0"
        onKeyDown={onKeyDown}
      >
        <DialogTitle className="sr-only">Search and navigate</DialogTitle>

        <div className="flex items-center gap-3 border-b border-border px-4">
          <Search className="size-4 shrink-0 text-muted-foreground" aria-hidden />
          <input
            autoFocus
            value={term}
            onChange={(e) => setTerm(e.target.value)}
            placeholder="Search members, invoices, or jump to a screen"
            aria-label="Search members, invoices, or jump to a screen"
            className="h-12 w-full bg-transparent text-sm outline-none placeholder:text-muted-foreground"
          />
        </div>

        <div ref={listRef} className="max-h-80 overflow-y-auto p-2" role="listbox" aria-label="Results">
          {rows.length === 0 ? (
            <p className="px-3 py-6 text-center text-sm text-muted-foreground">
              {term.trim().length < 2
                ? 'Type at least two characters to search.'
                : search.isFetching
                  ? 'Searching…'
                  : 'Nothing matches that.'}
            </p>
          ) : (
            rows.map((row, index) => {
              const heading = row.group !== lastGroup ? row.group : null
              lastGroup = row.group
              const active = index === activeIndex
              const Icon = row.icon

              return (
                <div key={row.key}>
                  {heading && (
                    <p className="px-3 pt-3 pb-1 text-[10px] font-bold tracking-[0.13em] text-muted-foreground uppercase">
                      {heading}
                    </p>
                  )}
                  <button
                    type="button"
                    role="option"
                    aria-selected={active}
                    data-active={active}
                    // Mouse and keyboard drive the same single highlight, so hovering never leaves two
                    // rows looking selected at once.
                    onMouseMove={() => setActiveIndex(index)}
                    onClick={() => go(row.route)}
                    className={cn(
                      'flex w-full items-center gap-3 rounded-xl px-3 py-2 text-left text-sm transition-colors',
                      active ? 'bg-accent text-accent-foreground' : 'text-foreground',
                    )}
                  >
                    <Icon className="size-4 shrink-0 text-muted-foreground" aria-hidden />
                    <span className="min-w-0 flex-1 truncate">{row.label}</span>
                    {row.hint && <span className="shrink-0 text-xs text-muted-foreground">{row.hint}</span>}
                    {active && <CornerDownLeft className="size-3.5 shrink-0 text-muted-foreground" aria-hidden />}
                  </button>
                </div>
              )
            })
          )}
        </div>
      </DialogContent>
    </Dialog>
  )
}
