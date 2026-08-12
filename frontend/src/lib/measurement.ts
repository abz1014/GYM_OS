/**
 * How a set reads when it is written down.
 *
 * One formatter, because there are four screens that describe a set back to the member — the live
 * session card, the "same as last time" proposal, the quick logger's pending list, and the picker's
 * last-done line — and they were each doing it inline as `{sets}×{reps}`. That worked only while
 * every movement had reps. Once a run stopped having them, all four printed "1 ×" with nothing after
 * it: not a fabricated number, but the shape of a number that failed to arrive, which reads as a bug
 * to the person looking at it and tells them nothing about the 3km they just ran.
 *
 * The rule is that a measurement appears when it exists and is silent when it does not. There is no
 * placeholder, no zero and no dash inside the phrase — a plank simply reads "3 × 45s".
 */
export interface SetMeasurement {
  sets: number
  reps?: number | null
  weightKg?: number | null
  durationSeconds?: number | null
  distanceMeters?: number | null
}

/** A duration as a person would say it: "45s" under a minute, "20:00" above one. */
export function formatDuration(seconds: number): string {
  if (seconds < 60) return `${seconds}s`
  const mins = Math.floor(seconds / 60)
  return `${mins}:${String(seconds % 60).padStart(2, '0')}`
}

/** A distance in the unit that keeps it readable: metres below a kilometre, kilometres above. */
export function formatDistance(metres: number): string {
  return metres >= 1000 ? `${(metres / 1000).toFixed(2)}km` : `${metres}m`
}

/**
 * "3 × 8 · 60kg", "1 × 3.00km in 20:00", "3 × 45s", or just "4 sets" when nothing else is recorded.
 *
 * The bare-set fallback is deliberate. A movement can be genuinely logged with only a set count —
 * the proposal serves one before any measurement is chosen — and "4 sets" says exactly that, where
 * "4 × " says the app lost something.
 */
export function describeSets(m: SetMeasurement): string {
  const measures: string[] = []
  if (m.reps !== null && m.reps !== undefined) measures.push(String(m.reps))
  if (m.distanceMeters !== null && m.distanceMeters !== undefined) {
    measures.push(formatDistance(m.distanceMeters))
  }
  if (m.durationSeconds !== null && m.durationSeconds !== undefined) {
    measures.push(formatDuration(m.durationSeconds))
  }

  // "in" reads correctly for a distance done over a time; a plain list would give "3.00km 20:00".
  const body =
    measures.length > 0
      ? `${m.sets} × ${measures.join(' in ')}`
      : `${m.sets} ${m.sets === 1 ? 'set' : 'sets'}`
  const load = m.weightKg !== null && m.weightKg !== undefined ? ` · ${m.weightKg}kg` : ''
  return `${body}${load}`
}
