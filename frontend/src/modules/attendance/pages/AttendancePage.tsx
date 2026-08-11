import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Dumbbell } from 'lucide-react'

import { cn } from '@/lib/utils'
import { apiClient } from '@/lib/apiClient'
import { AttendanceHistoryPanel } from '@/modules/attendance/components/AttendanceHistoryPanel'
import { CheckInPanel } from '@/modules/attendance/components/CheckInPanel'
import { FrontDeskRail } from '@/modules/attendance/components/FrontDeskRail'
import { kioskTimeFormat } from '@/modules/attendance/components/frontDeskFormat'
import { MemberDetailPanel } from '@/modules/members/components/MemberDetailPanel'
import { Bloom, GrainOverlay } from '@/shared/components/uplift'
import { useAuthStore } from '@/stores/authStore'
import { useUiStore } from '@/stores/uiStore'

interface Branch {
  id: string
  name: string
  /** Null when the gym has never set one — see Branch.Capacity. Not zero, and not a default. */
  capacity: number | null
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
 *
 * The screen's one bloom lives here rather than inside CheckInPanel: it is meant to sit behind the
 * whole left half and bleed off the edge, which is only possible from the element that owns the
 * edge. It is green rather than volt because the thing it is lighting is the granted verdict.
 */
export default function AttendancePage() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const now = useKioskClock()

  /*
   * This screen is reached on attendance.view, which is a READING grant — a Trainer holds it to see
   * whether their clients have been in, and does not hold attendance.check_in. Without this the
   * kiosk opened for them anyway: a scan field, a resolved member, and a check-in that 403s at the
   * counter. They keep the half they are entitled to (History) and are never shown the half they
   * are not, rather than being handed a till they have no key for.
   */
  const canCheckIn = useAuthStore((s) => s.hasPermission)('attendance.check_in')
  const [view, setView] = useState<'checkin' | 'history'>(canCheckIn ? 'checkin' : 'history')

  /*
   * The member the kiosk has resolved. While one is on screen, the right rail stops being ambient
   * (who's inside, next classes) and becomes THAT member's workspace — plan, billing, notes, actions
   * — because the person the rail could help with is standing at the counter right now. The kiosk's
   * Next button clears it and the ambient rail returns.
   *
   * This is the audit's front-desk finding closed at its root: the workspace existed, but only on
   * screens the receptionist had to leave the desk view to reach.
   */
  const [workspaceMemberId, setWorkspaceMemberId] = useState<string | null>(null)

  // Same query key as the branch switcher in the top bar, so the name is read out of a cache that is
  // already populated rather than fetched a second time.
  const { data: branches } = useQuery({
    queryKey: ['branches'],
    queryFn: async () => (await apiClient.get<Branch[]>('/api/branches')).data,
  })
  const branch = branches?.find((b) => b.id === branchId)
  const branchName = branch?.name

  return (
    <div className="dark relative -m-3 flex min-h-[calc(100%+1.5rem)] flex-col overflow-hidden bg-background text-foreground sm:-m-6 sm:min-h-[calc(100%+3rem)]">
      <Bloom className="-top-[30%] left-[6%] h-[110%] w-[52%] rounded-full" color="#A3E635" opacity={0.13} blur={70} />
      <GrainOverlay opacity={0.045} />

      <header className="relative flex flex-wrap items-center gap-x-4 gap-y-3 border-b border-border px-6 py-4">
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
          {/* One view and no toggle when only one is permitted — a two-button switch with a dead
              half is worse than no switch. */}
          <div className={cn('flex rounded-xl border border-border p-1', !canCheckIn && 'hidden')}>
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
            className="font-display text-[30px] leading-none font-black tracking-tight tabular-nums"
          >
            {kioskTimeFormat.format(now)}
          </time>
        </div>
      </header>

      {view === 'checkin' ? (
        <div className="relative grid min-h-0 flex-1 grid-cols-1 lg:grid-cols-[1fr_420px]">
          <div className="flex min-w-0 items-center px-6">
            <CheckInPanel onMemberChange={setWorkspaceMemberId} />
          </div>
          {workspaceMemberId ? (
            <aside className="min-h-0 overflow-y-auto border-border p-4 lg:h-full lg:border-l">
              <MemberDetailPanel
                memberId={workspaceMemberId}
                variant="rail"
                onClose={() => setWorkspaceMemberId(null)}
              />
            </aside>
          ) : (
            <FrontDeskRail capacity={branch?.capacity ?? null} />
          )}
        </div>
      ) : (
        // Wrapped only to give it a stacking context above the bloom, which is absolutely
        // positioned and would otherwise paint over this static subtree.
        <div className="relative">
          <AttendanceHistoryPanel />
        </div>
      )}
    </div>
  )
}
