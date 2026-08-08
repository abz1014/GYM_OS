import { useEffect, useRef, useState } from 'react'

/** The one place the kit's expressive duration is read from JS. Keep in step with index.css. */
const EXPRESSIVE_MS = 640

/** Matches the CSS `--ease-expressive`, so a number and the ring beside it decelerate together. */
const easeExpressive = (t: number) => 1 - Math.pow(1 - t, 3)

/**
 * Whether the viewer has asked for less motion. Read live rather than once, so toggling the OS
 * setting takes effect without a reload.
 */
export function usePrefersReducedMotion(): boolean {
  const [reduced, setReduced] = useState(
    () => typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches,
  )

  useEffect(() => {
    const query = window.matchMedia('(prefers-reduced-motion: reduce)')
    const onChange = (e: MediaQueryListEvent) => setReduced(e.matches)
    query.addEventListener('change', onChange)
    return () => query.removeEventListener('change', onChange)
  }, [])

  return reduced
}

/**
 * Rolls a number up on mount, for Archivo numerals at 26px and above.
 *
 * `from` exists because the interesting roll-up is rarely from zero: a personal record should count
 * up from the weight it beat, so the movement itself carries the story. Where there is no previous
 * value, zero is the honest start.
 *
 * Under `prefers-reduced-motion` it returns the final value immediately — the number is information
 * and must never be withheld for the sake of an animation.
 */
export function useCountUp(
  to: number,
  { from = 0, durationMs = EXPRESSIVE_MS }: { from?: number; durationMs?: number } = {},
) {
  const reduced = usePrefersReducedMotion()
  const [value, setValue] = useState(reduced ? to : from)
  const frame = useRef<number | undefined>(undefined)

  useEffect(() => {
    if (reduced) {
      setValue(to)
      return
    }

    const start = performance.now()
    const tick = (now: number) => {
      const progress = Math.min(1, (now - start) / durationMs)
      setValue(from + (to - from) * easeExpressive(progress))
      if (progress < 1) {
        frame.current = requestAnimationFrame(tick)
      }
    }

    frame.current = requestAnimationFrame(tick)
    return () => {
      if (frame.current !== undefined) cancelAnimationFrame(frame.current)
    }
  }, [to, from, durationMs, reduced])

  return value
}
