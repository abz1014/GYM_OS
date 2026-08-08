import { CloudOff } from 'lucide-react'

import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { useAttendanceHistory } from '@/modules/attendance/api/attendanceApi'
import { initials, kioskTimeFormat } from '@/modules/attendance/components/frontDeskFormat'
import { useClassSessions } from '@/modules/classes/api/classesApi'
import { ClassRosterDialog } from '@/modules/classes/components/ClassRosterDialog'
import { useUiStore } from '@/stores/uiStore'

const EYEBROW = 'text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase'

/**
 * The rail the desk reads between scans: how full the room is, who just came through, and what's
 * about to start.
 *
 * The "/ 180" and the fill bar are here, but only for a site that has actually been given a capacity.
 * `Branch.Capacity` is nullable on purpose, so a gym that never filled it in keeps the bare count
 * rather than being measured against a ceiling this app invented for it.
 *
 * Two things the mockup puts here are still missing on purpose:
 *
 * - **"Turnstile online".** There is no turnstile, no door controller and no `IDoorAccessProvider`
 *   implementation in this system — check-in is a row in a table. A green hardware-health dot is the
 *   worst possible thing to fake, because staff would trust it to mean the door is working. The top
 *   strip shows the branch instead, which is both true and the thing a multi-site desk actually needs.
 * - **The per-row plan name and the amber "Payment overdue · let through" flag.** The feed comes from
 *   GET /api/attendance, whose row is (id, memberId, memberName, checkInAt, checkOutAt, method) —
 *   there is no plan and no billing state on it. The second line carries what the row does know: still
 *   in the building, or the time they left.
 *
 * The uplift gives the feed two states and no new facts: the newest row is volt-tinted because it is
 * the scan the desk just made, and a row whose visit has closed drops to 60% — present, because "did
 * she come in today" is a question staff ask, but no longer competing with the people in the room.
 */
/**
 * @param capacity The licensed number for this site, or null when the gym has never set one. Passed
 *   in rather than fetched: the page above already has the branch list, and the branches endpoint is
 *   fetched in enough places already without this adding another.
 */
export function FrontDeskRail({ capacity }: { capacity: number | null }) {
  const branchId = useUiStore((s) => s.selectedBranchId)

  /**
   * Bounded to today, which is load-bearing rather than tidy. The bare "checked in only" filter also
   * returns visits from previous days that nobody ever closed — members routinely leave without
   * checking out — so unbounded it reported 35 people in a building that had seen 16 check-ins all
   * day, and disagreed with the dashboard's own count of the same thing on the next screen along.
   * The number a desk trusts to say how full the room is has to be today's.
   */
  const today = new Date().toISOString().slice(0, 10)

  // totalCount, not the page — one row is fetched purely to read the count off the envelope.
  const inBuilding = useAttendanceHistory({
    branchId,
    checkedInOnly: true,
    fromDate: today,
    toDate: today,
    page: 1,
    pageSize: 1,
  })
  const recent = useAttendanceHistory({ branchId, page: 1, pageSize: 6 })
  const sessions = useClassSessions({ branchId })

  // "Next" means next — the endpoint's default window opens at midnight today, so this morning's
  // finished classes are in the list and would otherwise be announced as upcoming.
  const now = Date.now()
  const nextClass = sessions.data
    ?.filter((s) => s.status === 'Scheduled' && new Date(s.startsAt).getTime() > now)
    .sort((a, b) => new Date(a.startsAt).getTime() - new Date(b.startsAt).getTime())[0]

  return (
    <aside className="flex flex-col gap-6 border-border p-6 lg:h-full lg:border-l">
      <div>
        <div className="flex items-baseline justify-between gap-3">
          <h2 className="font-display text-xl font-bold tracking-tight">In the building</h2>
          {inBuilding.isPending ? (
            <Skeleton className="h-9 w-14" />
          ) : inBuilding.isError ? (
            <CloudOff className="size-6 text-muted-foreground" />
          ) : (
            <span className="flex items-baseline gap-1.5">
              <span className="font-display text-5xl leading-none font-black tracking-tight text-primary tabular-nums">
                {inBuilding.data.totalCount}
              </span>
              {/*
                The denominator, and only when the gym has actually given one. Branch.Capacity is
                nullable because "nobody has told us" is a real and common state — so a site that
                never filled it in keeps the bare count rather than being measured against a ceiling
                this app invented for it.
              */}
              {capacity !== null && (
                <span className="text-lg text-muted-foreground tabular-nums">/ {capacity}</span>
              )}
            </span>
          )}
        </div>

        {capacity !== null && !inBuilding.isPending && !inBuilding.isError && (
          <div className="mt-3 h-1.5 w-full overflow-hidden rounded-full bg-border">
            <div
              className={cn(
                'h-full rounded-full transition-[width] duration-700 ease-out',
                // Over the licensed number is a fact staff need to act on, not a full bar — the fill
                // is clamped so the bar stays a bar, and the colour is what carries the alarm.
                inBuilding.data.totalCount >= capacity && 'bg-destructive',
              )}
              style={{
                width: `${Math.min(100, (inBuilding.data.totalCount / capacity) * 100)}%`,
                // The kit's volt gradient, the same one the member app's rings and progress fills
                // use. Dropped entirely when the room is over its licensed number: a gradient there
                // would soften the one reading on this rail that is supposed to look wrong.
                ...(inBuilding.data.totalCount >= capacity
                  ? null
                  : { background: 'linear-gradient(90deg,#B8DC2B,#EDFF8A)' }),
              }}
            />
          </div>
        )}

        {inBuilding.isError && (
          <p className="mt-2 text-sm text-muted-foreground">Can't reach the desk's records right now.</p>
        )}
      </div>

      <div className="min-h-0 flex-1">
        <p className={EYEBROW}>Just now</p>

        {recent.isPending && (
          <div className="mt-3 space-y-1">
            {Array.from({ length: 4 }).map((_, i) => (
              <Skeleton key={i} className="h-[58px] w-full rounded-[14px]" />
            ))}
          </div>
        )}

        {recent.isSuccess && recent.data.items.length === 0 && (
          <p className="mt-3 rounded-2xl border border-border px-4 py-6 text-center text-sm text-muted-foreground">
            No check-ins recorded at this branch yet.
          </p>
        )}

        {recent.isSuccess && recent.data.items.length > 0 && (
          <ul className="mt-3 space-y-1">
            {recent.data.items.map((record, index) => {
              const checkedOutAt = record.checkOutAt
              // The row the desk is actually looking at — the scan that just happened. It is keyed by
              // record id, so a new arrival mounts a new element and the fade runs on its own without
              // any "is this new" bookkeeping.
              const isNewest = index === 0

              return (
                <li
                  key={record.id}
                  className={cn(
                    'flex items-center gap-3 rounded-[14px] px-3 py-2.5',
                    isNewest && 'animate-in fade-in slide-in-from-top-2 bg-secondary duration-[240ms]',
                    // Someone who has left is still worth showing — staff read this to answer "did he
                    // come in today" — but at 60% it stops competing with the people in the room.
                    checkedOutAt && 'opacity-60'
                  )}
                >
                  <span
                    className={cn(
                      'flex size-9 shrink-0 items-center justify-center rounded-xl font-display text-xs font-black',
                      isNewest ? 'bg-primary text-primary-foreground' : 'bg-border'
                    )}
                  >
                    {initials(record.memberName)}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-medium">{record.memberName}</span>
                    <span className="block truncate text-sm text-muted-foreground tabular-nums">
                      {checkedOutAt ? `Left at ${kioskTimeFormat.format(new Date(checkedOutAt))}` : 'In the building'}
                    </span>
                  </span>
                  <span
                    className={cn(
                      'shrink-0 text-sm font-medium tabular-nums',
                      isNewest ? 'text-primary' : 'text-muted-foreground'
                    )}
                  >
                    {kioskTimeFormat.format(new Date(record.checkInAt))}
                  </span>
                </li>
              )
            })}
          </ul>
        )}
      </div>

      {/* Only drawn when there genuinely is a scheduled session still ahead of the clock — an empty
          "Next class" frame at 10pm tells the desk nothing it didn't already know. */}
      {nextClass && (
        <div className="rounded-[22px] border border-border bg-card p-4 edge-light">
          <p className={EYEBROW}>Next class</p>
          <div className="mt-3 flex items-center gap-3">
            <span className="flex size-14 shrink-0 flex-col items-center justify-center rounded-xl bg-muted font-display leading-none font-black tracking-tight text-chart-2 tabular-nums">
              <span className="text-xl">{kioskTimeFormat.format(new Date(nextClass.startsAt)).slice(0, 2)}</span>
              <span className="mt-0.5 text-[11px] opacity-70">
                {kioskTimeFormat.format(new Date(nextClass.startsAt)).slice(2)}
              </span>
            </span>
            <span className="min-w-0 flex-1">
              <span className="block truncate font-display text-lg font-bold tracking-tight">
                {nextClass.classTypeName}
              </span>
              <span className="block truncate text-sm text-muted-foreground">
                {/* trainerName is nullable on the session DTO — an unstaffed session says the room
                    instead of printing an empty separator. */}
                {nextClass.trainerName ?? nextClass.location ?? 'Booked in'}
              </span>
            </span>
            {/* The real roster, with the real check-in actions on it — the same dialog the Classes
                page opens, rather than a shortcut that goes somewhere adjacent. Its trigger already
                reads "{booked}/{capacity}", so the fill count isn't repeated beside it. */}
            <ClassRosterDialog session={nextClass} />
          </div>
        </div>
      )}
    </aside>
  )
}
