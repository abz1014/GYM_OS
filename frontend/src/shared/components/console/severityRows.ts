/**
 * The severity row — the one element the redesign added to the console's list template (spec §9.4).
 *
 * A 3px inset rail plus a tinted ground on the rows that are escalating, and nothing on the rows
 * that are not. That asymmetry is the whole point: it is what makes nine rows worth acting on
 * findable in a page of fifty, and it stops working the moment every row carries one.
 *
 * These live here rather than in the billing pair that first drew them because twelve more modules
 * inherit the same vocabulary, and a hex repeated across thirteen files is a hex that drifts. The
 * dashboard's `NeedsYouPanel` grounds use the same three values, so a queue row and the list row it
 * links to are the same colour.
 *
 * The rail is a `box-shadow`, which means a row takes exactly one of rail / edge-light — never both.
 * On a `<tr>` this paints under Tailwind's collapsed border model (verified in Chrome); the tinted
 * ground carries the row on its own if a browser ever declines to.
 */
export const SEVERITY_ROW = {
  /** Past due, out of service, out of stock — someone has to act, and it is already late. */
  destructive: 'rail-destructive bg-[#FDF2F2] hover:bg-[#FAE8E8]',
  /** Due, low, under maintenance — heading for the red tier but not there yet. */
  warning: 'rail-warning bg-[#FDF6EC] hover:bg-[#F9EDDC]',
  /**
   * Set apart without being escalated. Written as a raw inset shadow because there is deliberately
   * no `rail-muted` utility — a grey rail in the kit would invite decorating rows that mean nothing.
   */
  neutral: 'bg-[#F4F4F0] shadow-[inset_3px_0_0_#8A8A80]',
} as const

export type SeverityTone = keyof typeof SEVERITY_ROW
