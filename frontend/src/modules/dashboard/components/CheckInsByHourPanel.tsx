import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { PanelError } from '@/shared/components/console'
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
 * quiet rather than just tall. Both series stayed exactly as they were through the uplift pass; what
 * that pass added is the peak bar's gradient and glow, and the dashed NOW marker — which is free,
 * because the current hour is the one fact on this chart the browser already knows.
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

  // Where "now" falls inside its own column, so the marker sits at 18:40 rather than on the 18:00
  // gridline. Read at render rather than from a timer of its own: DashboardPage already re-renders
  // this subtree every five seconds for its "Updated {n}s ago" chip, which is finer than a marker
  // measured in hours needs.
  const clock = new Date()
  const nowHour = clock.getHours()
  const nowOffsetPercent = (clock.getMinutes() / 60) * 100

  return (
    <section className="rounded-3xl border border-border bg-card p-6 edge-light-soft">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="font-display text-lg font-bold tracking-tight">Check-ins by hour</h2>
          {/*
            The subtitle needed the same error branch the plot below already had, and didn't have
            one. `totalToday` is summed out of `toBuckets(undefined)`, which is 24 zeroes, so a
            failed request landed on the `=== 0` arm and printed "No check-ins recorded today yet"
            in the panel header — directly above a plot area correctly saying it couldn't load. The
            header is the line someone reads; it was the one telling them the door hadn't opened.
          */}
          {todayQuery.isLoading ? (
            <Skeleton className="mt-2 h-4 w-48" />
          ) : todayQuery.isError && !todayQuery.data ? null : (
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
              <span className="size-2.5 rounded-sm bg-muted" />
              4-wk avg
            </span>
          )}
        </div>
      </div>

      {todayQuery.isLoading ? (
        <Skeleton className="mt-6 h-56 w-full" />
      ) : todayQuery.isError && !todayQuery.data ? (
        <PanelError
          message="We couldn't load today's check-ins."
          onRetry={() => void todayQuery.refetch()}
          isRetrying={todayQuery.isFetching}
        />
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
                  {/*
                    The NOW marker. Drawn first so the bars paint over it — it is a reference line,
                    not a series, and it must never be mistaken for one. It renders only inside the
                    hour that is currently running, so it disappears by construction on a window that
                    doesn't contain the present (a chart left open past midnight, say).
                  */}
                  {hour === nowHour && (
                    <>
                      <span
                        aria-hidden
                        className="absolute top-0 bottom-0 w-[1.5px] opacity-30"
                        style={{
                          left: `${nowOffsetPercent}%`,
                          backgroundImage:
                            'repeating-linear-gradient(180deg, var(--foreground) 0 4px, transparent 4px 8px)',
                        }}
                      />
                      <span
                        aria-hidden
                        className="absolute -top-3.5 -translate-x-1/2 rounded-md bg-foreground px-2 py-[3px] text-[10px] leading-none font-bold tracking-[0.06em] text-primary"
                        style={{ left: `${nowOffsetPercent}%` }}
                      >
                        NOW
                      </span>
                    </>
                  )}

                  {/*
                    The average is a bar behind today's, not a line across it.

                    It was a floating tick per column, which failed in all three positions it can
                    take: below today's bar it drew a grey line THROUGH the ink and read as a
                    rendering fault, above it floated in empty space attached to nothing, and on an
                    hour with no check-ins yet it was a lone dash with nothing to compare against.
                    Seventeen of them across the plot read as specks rather than a series.

                    As a backdrop band the comparison is the shape itself: today's bar standing
                    proud of the grey is a busy hour, sitting inside it is a quiet one, and a grey
                    bar alone says the floor is normally busy now and isn't. Today's bar is narrower
                    so the grey stays visible either side rather than being covered whenever the day
                    is going well.
                  */}
                  {average > 0 && (
                    <div
                      aria-hidden
                      className="absolute inset-x-0 bottom-0 rounded-t-md bg-muted"
                      style={{ height: `${(average / scaleMax) * 100}%` }}
                    />
                  )}
                  <div
                    // Peak hour in volt, the rest in ink — the one thing a manager reads off this
                    // chart is "when is the floor full", so it gets the accent, and now the gradient
                    // and coloured shadow that lift it off the plot. Non-sequential bars, so only
                    // the peak is coloured: a ramp across the day would imply an order these hours
                    // don't have.
                    className={cn(
                      'relative mx-auto w-3/5 rounded-t-md transition-[height] duration-700 ease-out',
                      isPeak ? 'shadow-[0_6px_18px_-6px_rgba(214,249,74,0.9)]' : 'bg-foreground'
                    )}
                    style={{
                      height: `${(count / scaleMax) * 100}%`,
                      ...(isPeak ? { background: 'linear-gradient(180deg,#D6F94A,#C0E33A)' } : null),
                    }}
                  />
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
