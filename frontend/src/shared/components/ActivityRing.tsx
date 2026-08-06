import type { ReactNode } from 'react'

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
 */
export function ActivityRing({
  value,
  goal,
  size = 160,
  stroke = 14,
  colorClassName = 'text-primary',
  children,
}: ActivityRingProps) {
  const safeGoal = Math.max(1, goal)
  const fraction = Math.min(1, Math.max(0, value / safeGoal))

  // Drawn in a normalised 100x100 box and scaled by CSS, so one geometry works at any size.
  const radius = 50 - stroke / 2
  const circumference = 2 * Math.PI * radius
  const complete = value >= safeGoal

  return (
    <div className={`relative shrink-0 ${colorClassName}`} style={{ width: size, height: size }}>
      <svg viewBox="0 0 100 100" className="size-full -rotate-90" aria-hidden>
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
