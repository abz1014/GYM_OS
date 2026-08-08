import { useId, useMemo, useState } from 'react'

export interface TrendPoint {
  /** X-axis label, already formatted for display (e.g. "Aug 5"). */
  label: string
  value: number
}

interface TrendChartProps {
  data: TrendPoint[]
  /** Formats the value in the tooltip and on the Y axis. */
  valueFormatter?: (value: number) => string
  /** Tailwind text-* colour class; the line, area gradient and dots all inherit from it. */
  colorClassName?: string
  height?: number
  /** Draw the filled area under the line. Off for sparse/among-zero series where it reads as noise. */
  showArea?: boolean
  /** Force the Y axis to start at zero. Off by default so weight trends don't flatten into a line. */
  zeroBaseline?: boolean
  /**
   * Volt glow on the line, plus a persistent marker on the last non-zero point.
   *
   * Opt-in rather than always-on, and deliberately not derived from `colorClassName`: the glow is the
   * accent's own volt, so switching it on for a `text-chart-2` series (body weight, body fat) would
   * halo a blue line in yellow-green. It belongs to the member's volume trend, where volt is the
   * series colour and the latest value is the one figure they opened the page for.
   */
  glow?: boolean
  emptyMessage?: string
}

/**
 * A real line/area trend chart — the counterpart to SimpleBarChart, which can only show categorical
 * totals. Progress over time (body weight, training volume) is a shape, not a set of bars, and
 * reading it as bars hides exactly the thing a member cares about: direction.
 *
 * Hand-rolled SVG rather than a charting dependency: the bundle already ships no chart library, and
 * this needs to theme off the same CSS variables as the rest of the app in both light and dark mode.
 */
export function TrendChart({
  data,
  valueFormatter = (v) => String(Math.round(v)),
  colorClassName = 'text-primary',
  height = 200,
  showArea = true,
  zeroBaseline = false,
  glow = false,
  emptyMessage = 'No data yet.',
}: TrendChartProps) {
  const gradientId = useId()
  const [hoverIndex, setHoverIndex] = useState<number | null>(null)

  // Fixed viewBox + preserveAspectRatio="none" would distort strokes, so instead the chart draws in
  // a normalised 0..100 x 0..100 space and scales via CSS — strokes stay crisp with vectorEffect.
  const geometry = useMemo(() => {
    if (data.length === 0) return null

    const values = data.map((d) => d.value)
    const rawMin = Math.min(...values)
    const rawMax = Math.max(...values)

    // A perfectly flat series would otherwise divide by zero and collapse to the top edge.
    let min = zeroBaseline ? 0 : rawMin
    let max = rawMax
    if (max === min) {
      max = min + 1
      min = Math.max(0, min - 1)
    } else if (!zeroBaseline) {
      const pad = (max - min) * 0.15
      min -= pad
      max += pad
    }

    const span = max - min
    const x = (i: number) => (data.length === 1 ? 50 : (i / (data.length - 1)) * 100)
    const y = (v: number) => 100 - ((v - min) / span) * 100

    const points = data.map((d, i) => ({ x: x(i), y: y(d.value), ...d }))
    const line = points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x} ${p.y}`).join(' ')
    const area = `${line} L 100 100 L 0 100 Z`

    return { points, line, area, min, max }
  }, [data, zeroBaseline])

  if (!geometry) {
    return <p className="py-10 text-center text-sm text-muted-foreground">{emptyMessage}</p>
  }

  const { points, line, area, min, max } = geometry
  const active = hoverIndex !== null ? points[hoverIndex] : null
  // Thin X labels the same way SimpleBarChart does, so 30 daily points don't collide.
  const labelStride = Math.max(1, Math.ceil(points.length / 6))
  /*
   * The latest value that actually happened. Dense series draw no markers at all — thirty daily
   * points, most of them rest-day zeros, read as noise — but the member's most recent session is the
   * one point worth pinning, and pinning a zero would mark a rest day as the story.
   */
  const lastNonZeroIndex = glow ? points.reduce((last, p, i) => (p.value > 0 ? i : last), -1) : -1

  return (
    <div className={colorClassName}>
      <div className="flex gap-2">
        {/* Y axis */}
        <div
          className="flex shrink-0 flex-col justify-between py-0.5 text-right text-[10px] text-muted-foreground tabular-nums"
          style={{ height }}
          aria-hidden
        >
          <span>{valueFormatter(max)}</span>
          <span>{valueFormatter((max + min) / 2)}</span>
          <span>{valueFormatter(min)}</span>
        </div>

        <div className="relative min-w-0 flex-1" style={{ height }}>
          <svg
            viewBox="0 0 100 100"
            preserveAspectRatio="none"
            className="h-full w-full overflow-visible"
            role="img"
            aria-label={`Trend from ${data[0].label} to ${data[data.length - 1].label}`}
          >
            <defs>
              <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor="currentColor" stopOpacity="0.34" />
                <stop offset="100%" stopColor="currentColor" stopOpacity="0" />
              </linearGradient>
            </defs>

            {/* Horizontal gridlines at 0/25/50/75/100% */}
            {[0, 25, 50, 75, 100].map((gy) => (
              <line
                key={gy}
                x1="0"
                y1={gy}
                x2="100"
                y2={gy}
                className="stroke-border"
                strokeWidth={1}
                vectorEffect="non-scaling-stroke"
                strokeDasharray={gy === 100 ? undefined : '3 3'}
                opacity={gy === 100 ? 1 : 0.5}
              />
            ))}

            {showArea && <path d={area} fill={`url(#${gradientId})`} />}

            <path
              d={line}
              fill="none"
              stroke="currentColor"
              strokeWidth={2}
              strokeLinecap="round"
              strokeLinejoin="round"
              vectorEffect="non-scaling-stroke"
              style={glow ? { filter: 'drop-shadow(0 0 5px rgb(214 249 74 / 0.55))' } : undefined}
            />

            {active && (
              <line
                x1={active.x}
                y1={0}
                x2={active.x}
                y2={100}
                stroke="currentColor"
                strokeWidth={1}
                strokeDasharray="3 3"
                vectorEffect="non-scaling-stroke"
                opacity={0.5}
              />
            )}

          </svg>

          {/*
            Dots live in an HTML layer rather than as SVG <circle>s: the chart scales its 0..100
            viewBox with preserveAspectRatio="none" (which is what lets the line span any container
            width), and that same non-uniform scale stretches a circle into an ellipse. Positioning
            them in HTML keeps them round at every container size.
            Dense series only get the hovered dot — a marker on all 30 points, most of which are
            rest-day zeros, reads as noise rather than data — plus, under `glow`, a persistent one on
            the last real value.
          */}
          <div className="pointer-events-none absolute inset-0">
            {points.map((p, i) =>
              points.length <= 14 || i === hoverIndex || i === lastNonZeroIndex ? (
                <span
                  key={i}
                  className="absolute rounded-full border-2 border-current bg-background"
                  style={{
                    left: `${p.x}%`,
                    top: `${p.y}%`,
                    width: i === hoverIndex ? 10 : 7,
                    height: i === hoverIndex ? 10 : 7,
                    transform: 'translate(-50%, -50%)',
                    ...(i === lastNonZeroIndex
                      ? { boxShadow: '0 0 0 3px rgb(214 249 74 / 0.16), 0 0 10px rgb(214 249 74 / 0.55)' }
                      : {}),
                  }}
                />
              ) : null,
            )}
          </div>

          {/* Hover targets sit above the SVG so pointer maths stays trivial and touch works. */}
          <div className="absolute inset-0 flex">
            {points.map((p, i) => (
              <button
                key={i}
                type="button"
                tabIndex={-1}
                aria-label={`${p.label}: ${valueFormatter(p.value)}`}
                className="h-full flex-1 cursor-default focus:outline-none"
                onMouseEnter={() => setHoverIndex(i)}
                onMouseLeave={() => setHoverIndex(null)}
                onFocus={() => setHoverIndex(i)}
              />
            ))}
          </div>

          {active && (
            <div
              className="pointer-events-none absolute z-10 -translate-x-1/2 -translate-y-full rounded-md border bg-popover px-2 py-1 text-xs whitespace-nowrap text-popover-foreground shadow-md"
              style={{ left: `${active.x}%`, top: `${active.y}%`, marginTop: -8 }}
            >
              <span className="font-medium tabular-nums">{valueFormatter(active.value)}</span>
              <span className="ml-1.5 text-muted-foreground">{active.label}</span>
            </div>
          )}
        </div>
      </div>

      {/* X axis */}
      <div className="mt-1.5 flex pl-[calc(2.5rem+0.5rem)]">
        {points.map((p, i) => (
          <span key={i} className="min-w-0 flex-1 text-center text-[10px] whitespace-nowrap text-muted-foreground">
            {i % labelStride === 0 ? p.label : ''}
          </span>
        ))}
      </div>
    </div>
  )
}
