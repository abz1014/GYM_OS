import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import {
  OCCUPANCY_BASELINE_DAYS,
  occupancyBaselineRange,
  todayDateOnly,
  useHourlyCheckIns,
  type HourlyCheckIns,
} from '@/modules/dashboard/api/dashboardApi'

/** The window the chart always draws, widened by any hour that actually has traffic. */
const DEFAULT_FIRST_HOUR = 6
const DEFAULT_LAST_HOUR = 22

function hourLabel(hour: number): string {
  if (hour === 0) return '12a'
  if (hour === 12) return '12p'
  return hour < 12 ? `${hour}a` : `${hour - 12}p`
}

function toBuckets(data: HourlyCheckIns[] | undefined): number[] {
  const buckets = new Array<number>(24).fill(0)
  for (const bucket of data ?? []) {
    buckets[bucket.hourOfDay] = bucket.checkInCount
  }
  return buckets
}

/**
 * The redesign called this panel "Occupancy today" over a bar chart of hour buckets. It is titled
 * check-ins here because that is what the data is: /api/attendance/peak-hours counts door swipes per
 * hour, and occupancy would need those swipes netted off against check-OUTS, which members routinely
 * never do. The two numbers diverge by more than half in real gyms, and the one this endpoint can
 * answer is the one on the label.
 *
 * The mockup's "138 of 180 capacity" line is gone with it: Branch has no capacity field anywhere in
 * the domain, so there is no denominator to divide by.
 *
 * The second series is real, not decoration — the same endpoint over the previous 28 full days,
 * divided by 28, is a genuine "typical day at this hour" and is what makes a bar readable as busy or
 * quiet rather than just tall.
 */
export function CheckInsByHourPanel() {
  const today = todayDateOnly()
  const baselineRange = occupancyBaselineRange()

  const todayQuery = useHourlyCheckIns(today, today)
  const baselineQuery = useHourlyCheckIns(baselineRange.fromDate, baselineRange.toDate)

  const todayByHour = toBuckets(todayQuery.data)
  const averageByHour = toBuckets(baselineQuery.data).map((count) => count / OCCUPANCY_BASELINE_DAYS)

  const totalToday = todayByHour.reduce((sum, count) => sum + count, 0)
  const peakCount = Math.max(...todayByHour)
  const peakHour = peakCount > 0 ? todayByHour.indexOf(peakCount) : null
  const hasBaseline = averageByHour.some((average) => average > 0)

  // Never hide a check-in: the fixed 6a–10p window widens to cover any hour with real traffic in
  // either series, so an unusual 5am or 11pm visit still shows up instead of being cropped away.
  const hoursWithData = todayByHour
    .map((count, hour) => ({ hour, busy: count > 0 || averageByHour[hour] > 0 }))
    .filter((entry) => entry.busy)
    .map((entry) => entry.hour)

  const firstHour = Math.min(DEFAULT_FIRST_HOUR, ...hoursWithData)
  const lastHour = Math.max(DEFAULT_LAST_HOUR, ...hoursWithData)
  const hours = Array.from({ length: lastHour - firstHour + 1 }, (_, i) => firstHour + i)

  const scaleMax = Math.max(peakCount, ...averageByHour, 1)

  return (
    <section className="rounded-3xl border border-border bg-card p-6 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="font-display text-lg font-bold tracking-tight">Check-ins by hour</h2>
          {todayQuery.isLoading ? (
            <Skeleton className="mt-2 h-4 w-48" />
          ) : (
            <p className="mt-1 text-sm text-muted-foreground">
              {totalToday === 0
                ? 'No check-ins recorded today yet'
                : `${totalToday} today · busiest at ${hourLabel(peakHour ?? 0)}`}
            </p>
          )}
        </div>

        <div className="flex items-center gap-4 text-xs text-muted-foreground">
          <span className="flex items-center gap-1.5">
            <span className="size-2.5 rounded-sm bg-foreground" />
            Today
          </span>
          {hasBaseline && (
            <span className="flex items-center gap-1.5">
              <span className="h-0.5 w-3 bg-muted-foreground" />
              4-wk avg
            </span>
          )}
        </div>
      </div>

      {todayQuery.isLoading ? (
        <Skeleton className="mt-6 h-56 w-full" />
      ) : todayQuery.isError ? (
        <p className="mt-6 text-sm text-muted-foreground">Couldn't load today's check-ins.</p>
      ) : (
        <>
          {/* Fixed plot height on purpose: this sits beside a queue whose length varies with how
              much is wrong today, and stretching the bars to match it would make the chart taller on
              a bad day for reasons that have nothing to do with attendance. */}
          <div className="mt-6 flex h-56 items-end gap-1 border-b border-border sm:gap-2">
            {hours.map((hour) => {
              const count = todayByHour[hour]
              const average = averageByHour[hour]
              const isPeak = peakCount > 0 && count === peakCount

              return (
                <div
                  key={hour}
                  className="relative flex h-full min-w-0 flex-1 items-end"
                  title={`${hourLabel(hour)}: ${count} check-in${count === 1 ? '' : 's'}${
                    hasBaseline ? ` · 4-wk avg ${average.toFixed(1)}` : ''
                  }`}
                >
                  <div
                    // Peak hour in volt, the rest in ink — the one thing a manager reads off this
                    // chart is "when is the floor full", so it gets the accent.
                    className={cn(
                      'w-full rounded-t-md transition-[height] duration-700 ease-out',
                      isPeak ? 'bg-primary' : 'bg-foreground'
                    )}
                    style={{ height: `${(count / scaleMax) * 100}%` }}
                  />
                  {average > 0 && (
                    <span
                      aria-hidden
                      className="absolute inset-x-0 border-t-2 border-muted-foreground/60"
                      style={{ bottom: `${(average / scaleMax) * 100}%` }}
                    />
                  )}
                </div>
              )
            })}
          </div>

          <div className="mt-2 flex gap-1 sm:gap-2">
            {hours.map((hour) => (
              <span key={hour} className="min-w-0 flex-1 text-center text-[10px] text-muted-foreground tabular-nums">
                {hour % 2 === 0 ? hourLabel(hour) : ''}
              </span>
            ))}
          </div>

          <p className="sr-only">
            {totalToday} check-ins today across {hours.length} hours
            {peakHour !== null ? `, busiest at ${hourLabel(peakHour)} with ${peakCount}` : ''}.
          </p>
        </>
      )}
    </section>
  )
}
