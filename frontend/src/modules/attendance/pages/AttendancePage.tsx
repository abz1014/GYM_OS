import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Dumbbell } from 'lucide-react'

import { cn } from '@/lib/utils'
import { apiClient } from '@/lib/apiClient'
import { AttendanceHistoryPanel } from '@/modules/attendance/components/AttendanceHistoryPanel'
import { CheckInPanel } from '@/modules/attendance/components/CheckInPanel'
import { FrontDeskRail } from '@/modules/attendance/components/FrontDeskRail'
import { kioskTimeFormat } from '@/modules/attendance/components/frontDeskFormat'
import { useUiStore } from '@/stores/uiStore'

interface Branch {
  id: string
  name: string
}

/**
 * Wall clock. A minute is the smallest unit anything on this screen is measured in, so it re-renders
 * on the minute rather than every second — a seconds hand on a screen nobody is watching is a
 * re-render per second for the life of the shift.
 */
function useKioskClock(): Date {
  const [now, setNow] = useState(() => new Date())

  useEffect(() => {
    const id = window.setInterval(() => setNow(new Date()), 15_000)
    return () => window.clearInterval(id)
  }, [])

  return now
}

/**
 * The front desk, and the one screen in the staff console that runs dark.
 *
 * That is not a theme preference, it's a viewing distance: every other staff screen is read at a desk
 * by the person driving it, and this one is read across a counter — often over someone's shoulder,
 * often from two metres away — so it trades the console's light surfaces for maximum contrast and
 * sizes the scan field, the member name and the verdict far beyond normal UI text.
 *
 * `text-foreground` next to `dark` is not redundant. The base layer puts that class on <body>, which
 * is OUTSIDE this element, so body resolves --foreground against :root — the LIGHT staff palette's
 * near-black ink — and every descendant that doesn't set its own colour inherits it, rendering dark on
 * dark. Re-declaring it here computes the variable inside the .dark scope. The login page hit exactly
 * this and carries the same note.
 *
 * Negative margins cancel AppShell's content padding so the dark surface reaches the edges of the
 * content area instead of floating as a panel inside a light frame — a kiosk that stops short of the
 * bezel reads as a window, not a mode.
 */
export default function AttendancePage() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const now = useKioskClock()
  const [view, setView] = useState<'checkin' | 'history'>('checkin')

  // Same query key as the branch switcher in the top bar, so the name is read out of a cache that is
  // already populated rather than fetched a second time.
  const { data: branches } = useQuery({
    queryKey: ['branches'],
    queryFn: async () => (await apiClient.get<Branch[]>('/api/branches')).data,
  })
  const branchName = branches?.find((b) => b.id === branchId)?.name

  return (
    <div className="dark -m-3 flex min-h-[calc(100%+1.5rem)] flex-col bg-background text-foreground sm:-m-6 sm:min-h-[calc(100%+3rem)]">
      <header className="flex flex-wrap items-center gap-x-4 gap-y-3 border-b border-border px-6 py-4">
        <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-primary text-primary-foreground">
          <Dumbbell className="size-5" />
        </span>
        <h1 className="font-display text-xl font-black tracking-[0.06em] uppercase">Front desk</h1>

        {/*
          The mockup puts a "Turnstile online" status dot here. Nothing in this product talks to a
          turnstile — there is no door controller, no IDoorAccessProvider implementation, no hardware
          heartbeat to read — and a green dot that staff would read as "the door is working" is the
          single most dangerous thing on this screen to fake. The branch takes its place: true, already
          loaded, and the thing a desk on a multi-site tenant actually needs to confirm.
        */}
        {branchName && (
          <span className="rounded-xl border border-border px-3 py-1.5 text-sm text-muted-foreground">{branchName}</span>
        )}

        <div className="ml-auto flex items-center gap-4">
          <div className="flex rounded-xl border border-border p-1">
            {(
              [
                ['checkin', 'Check in'],
                ['history', 'History'],
              ] as const
            ).map(([value, label]) => (
              <button
                key={value}
                type="button"
                onClick={() => setView(value)}
                aria-pressed={view === value}
                className={cn(
                  'rounded-lg px-4 py-1.5 text-sm font-bold transition-colors',
                  view === value ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground'
                )}
              >
                {label}
              </button>
            ))}
          </div>
          <time
            dateTime={now.toISOString()}
            className="font-display text-3xl leading-none font-black tracking-tight tabular-nums"
          >
            {kioskTimeFormat.format(now)}
          </time>
        </div>
      </header>

      {view === 'checkin' ? (
        <div className="grid min-h-0 flex-1 grid-cols-1 lg:grid-cols-[1fr_420px]">
          <div className="flex min-w-0 items-center px-6">
            <CheckInPanel />
          </div>
          <FrontDeskRail />
        </div>
      ) : (
        <AttendanceHistoryPanel />
      )}
    </div>
  )
}
