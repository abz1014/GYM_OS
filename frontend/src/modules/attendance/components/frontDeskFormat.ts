/**
 * Formatting shared by the two halves of the front desk (the scan panel and the right rail) so a
 * name and a time can't render one way on the verdict card and a different way in the feed two
 * hundred pixels away — at a counter that inconsistency reads as two different systems.
 */

/** Wall-clock only. The kiosk never shows a date: everything on it happened in the last few hours. */
export const kioskTimeFormat = new Intl.DateTimeFormat('en-US', { hour: '2-digit', minute: '2-digit', hour12: false })

export const kioskDateFormat = new Intl.DateTimeFormat('en-US', { day: 'numeric', month: 'short', year: 'numeric' })

/**
 * Two letters, from the first and last word of the name the API returned. Members have no avatar
 * imagery in this product (profilePhotoUrl is nullable and empty across the board), so initials are
 * the identity chip rather than a fallback for one.
 */
export function initials(fullName: string): string {
  const parts = fullName.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return '?'
  const first = parts[0]!.charAt(0)
  const last = parts.length > 1 ? parts[parts.length - 1]!.charAt(0) : ''
  return (first + last).toUpperCase()
}

/** "14th visit this month" needs the ordinal; 11–13 are the exceptions every naive version gets wrong. */
export function ordinal(n: number): string {
  const rem100 = n % 100
  if (rem100 >= 11 && rem100 <= 13) return `${n}th`
  switch (n % 10) {
    case 1:
      return `${n}st`
    case 2:
      return `${n}nd`
    case 3:
      return `${n}rd`
    default:
      return `${n}th`
  }
}
