import { useCountUp } from '@/shared/hooks/useCountUp'

/**
 * The component form of the roll-up. `format` keeps the rolling value in the caller's own units —
 * "34.8t", "1,240", "87.5kg" — so a count-up never turns a formatted figure back into a raw one
 * halfway through.
 *
 * `tabular-nums` is the caller's job and is already on every metric in this product; without it the
 * digits jitter as they roll.
 */
export function CountUp({
  to,
  from = 0,
  format = (n: number) => Math.round(n).toLocaleString(),
  className,
}: {
  to: number
  from?: number
  format?: (value: number) => string
  className?: string
}) {
  const value = useCountUp(to, { from })
  return <span className={className}>{format(value)}</span>
}
