import { useId, type ReactNode } from 'react'

interface ActivityRingProps {
  /** Progress so far (e.g. sessions trained this week). */
  value: number
  /** The target the ring closes at. */
  goal: number
  size?: number
  stroke?: number
  /** Tailwind text-* class; the arc inherits from it via currentColor. */
  colorClassName?: string
  children?: ReactNode
  /**
   * One entry per day, in the order they should be drawn. Supplying this switches the ring from a
   * single progress arc to seven separate ones — see the component remarks. Absent, the ring renders
   * exactly as it always has, which is why the coach and nutrition pages using it are untouched.
   */
  segments?: boolean[]
  /** Initials drawn outside each arc. Only read in segmented mode; length must match `segments`. */
  segmentLabels?: string[]
  /** Which segment is today, so it can be marked without being claimed as trained. */
  todayIndex?: number
  /**
   * Draws the arc as a tonal gradient with a soft glow instead of a flat stroke — the celebration
   * ring of spec §4.4.
   *
   * Opt-in, and built from `currentColor` rather than the volt pair the segmented mode hardcodes,
   * because this ring is not always volt: the celebration turns it emerald the moment the weekly
   * goal is met. A fixed `#EDFF8A → #B8DC2B` would have quietly repainted that emerald ring
   * yellow-green at exactly the moment it is meant to say something different.
   */
  gradient?: boolean
}

/*
 * Segmented geometry, from the uplift spec §4.2. These are exact values rather than derived ones so
 * the drawn ring matches the design board to the pixel; the arithmetic that produced them is
 * r = 76 → circumference 477.46, divided into seven slots of 68.21, each drawing 50 units with a
 * 9-unit lead-in so the rounded caps sit inside their slot instead of touching the next one.
 */
const SEG_RADIUS = 76
const SEG_CIRCUMFERENCE = 477.46
const SEG_SLOT = 68.21
const SEG_DASH = 50
const SEG_LEAD_IN = 9
const SEG_STROKE = 13
/** Label ring, just outside the 82.5 outer edge of the stroke. */
const SEG_LABEL_RADIUS = 97

function segmentLabelPosition(index: number) {
  const centreAlongPath = index * SEG_SLOT + SEG_LEAD_IN + SEG_DASH / 2
  const radians = ((centreAlongPath / SEG_CIRCUMFERENCE) * 360 * Math.PI) / 180
  return {
    x: 100 + SEG_LABEL_RADIUS * Math.sin(radians),
    y: 100 - SEG_LABEL_RADIUS * Math.cos(radians),
  }
}

/**
 * A closable progress ring — the "am I on track?" answer in a single glance.
 *
 * Modelled on the Apple Watch rings pattern for a specific reason: the ring is the whole story, so
 * a member who only wants reassurance never has to read a chart or a table. Detail stays one tap
 * away on My Progress rather than competing for attention here (progressive disclosure).
 *
 * Overshoot is deliberately preserved in the caption but capped in the arc — a closed ring should
 * read as unambiguously "done", not creep round a second lap.
 *
 * **Segmented mode.** Given `segments`, the ring becomes the week: seven arcs, one per day, filled
 * where a session landed. This exists because Today was drawing the same `daysTrainedThisWeek`
 * array twice — once as a ring, once as a 7-dot strip underneath — two pictures of one fact costing
 * ~90px of the most valuable screen in the product. One picture that carries both the count and its
 * shape is strictly more informative than either half was.
 */
export function ActivityRing({
  value,
  goal,
  size = 160,
  stroke = 14,
  colorClassName = 'text-primary',
  children,
  segments,
  segmentLabels,
  todayIndex,
  gradient = false,
}: ActivityRingProps) {
  const safeGoal = Math.max(1, goal)
  const fraction = Math.min(1, Math.max(0, value / safeGoal))
  const complete = value >= safeGoal

  // Unique per instance: two rings on one page would otherwise share a gradient id and the second
  // would silently take the first's stops.
  const gradientId = useId()

  if (segments && segments.length > 0) {
    const trainedCount = segments.filter(Boolean).length

    return (
      <div className="relative shrink-0" style={{ width: size, height: size }}>
        {/* The wider viewBox leaves room for the day initials, which sit outside the stroke. */}
        <svg viewBox="-14 -14 228 228" className="size-full" aria-hidden>
          <defs>
            <linearGradient id={gradientId} x1="0" y1="0" x2="1" y2="1">
              <stop offset="0%" stopColor="#EDFF8A" />
              <stop offset="100%" stopColor="#B8DC2B" />
            </linearGradient>
          </defs>

          <g transform="rotate(-90 100 100)">
            {segments.map((trained, i) => {
              const isToday = i === todayIndex
              return (
                <circle
                  key={i}
                  cx="100"
                  cy="100"
                  r={SEG_RADIUS}
                  fill="none"
                  strokeWidth={SEG_STROKE}
                  strokeLinecap="round"
                  strokeDasharray={`${SEG_DASH} ${SEG_CIRCUMFERENCE - SEG_DASH}`}
                  strokeDashoffset={-(i * SEG_SLOT + SEG_LEAD_IN)}
                  stroke={trained ? `url(#${gradientId})` : 'var(--border)'}
                  // Today, before it has been trained, is marked rather than claimed: a dim volt
                  // arc says "this one is still open", where a filled one would say it was done.
                  opacity={!trained && isToday ? 0.26 : 1}
                  style={{
                    ...(trained ? { filter: 'drop-shadow(0 0 7px rgb(214 249 74 / 0.55))' } : {}),
                    ...(!trained && isToday ? { stroke: 'var(--primary)' } : {}),
                  }}
                />
              )
            })}
          </g>

          {segmentLabels?.map((label, i) => {
            const { x, y } = segmentLabelPosition(i)
            const trained = segments[i]
            const isToday = i === todayIndex
            return (
              <text
                key={i}
                x={x}
                y={y}
                textAnchor="middle"
                dominantBaseline="central"
                fontSize="10.5"
                fontWeight="700"
                fill={isToday ? 'var(--primary)' : trained ? '#8E8E82' : '#57574F'}
              >
                {label}
              </text>
            )
          })}
        </svg>

        <div className="absolute inset-0 flex flex-col items-center justify-center text-center">
          {children}
        </div>

        {/* The week strip this ring replaced carried exactly this sentence; it moves here with it. */}
        <span className="sr-only">
          {trainedCount} of {segments.length} days trained this week.
        </span>
      </div>
    )
  }

  // Drawn in a normalised 100x100 box and scaled by CSS, so one geometry works at any size.
  const radius = 50 - stroke / 2
  const circumference = 2 * Math.PI * radius

  return (
    <div className={`relative shrink-0 ${colorClassName}`} style={{ width: size, height: size }}>
      <svg viewBox="0 0 100 100" className="size-full -rotate-90" aria-hidden>
        {gradient && (
          <defs>
            {/*
              Both stops are currentColor; only the opacity differs, so the sweep is tonal rather
              than a second hue. That is what lets one gradient serve a volt ring and an emerald one.
            */}
            <linearGradient id={gradientId} x1="0" y1="0" x2="1" y2="1">
              <stop offset="0%" stopColor="currentColor" stopOpacity="1" />
              <stop offset="100%" stopColor="currentColor" stopOpacity="0.55" />
            </linearGradient>
          </defs>
        )}
        {/*
          The unfilled remainder. Drawn in the border colour rather than a faded copy of the arc:
          a 15%-opacity volt track on a dark card reads as a heavy olive band competing with the arc
          itself, when all it has to say is "this part isn't done".
        */}
        <circle
          cx="50"
          cy="50"
          r={radius}
          fill="none"
          strokeWidth={stroke}
          className="stroke-border"
        />
        <circle
          cx="50"
          cy="50"
          r={radius}
          fill="none"
          strokeWidth={stroke}
          strokeLinecap="round"
          className="stroke-current transition-[stroke-dashoffset] duration-700 ease-out"
          strokeDasharray={circumference}
          strokeDashoffset={circumference * (1 - fraction)}
          // Both overrides are currentColor-derived, so the arc stays whatever colour the caller
          // set; `stroke` wins over the `stroke-current` class, which stays for the non-gradient path.
          stroke={gradient ? `url(#${gradientId})` : undefined}
          style={gradient ? { filter: 'drop-shadow(0 0 5px currentColor)' } : undefined}
        />
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center text-center">
        {children}
      </div>
      <span className="sr-only">
        {value} of {goal} sessions this week{complete ? ', goal reached' : ''}
      </span>
    </div>
  )
}
