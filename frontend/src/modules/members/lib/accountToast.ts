import { toast } from 'sonner'

/**
 * The one-time handover for a member's portal password — there is no mail sender in this product, so
 * whoever is at the desk reads this out (or copies it) once, and it is never recoverable again.
 * Mirrors StaffTab's announceTemporaryPassword: same duration, same copy-to-clipboard action, because
 * a generated password is where it gets mistranscribed if read off the screen and retyped.
 */
export function announceTemporaryPassword(password: string, who: string) {
  toast.success(`${who} — temporary password: ${password}`, {
    duration: 20000,
    action: {
      label: 'Copy',
      onClick: () => void navigator.clipboard?.writeText(password),
    },
  })
}

/** The server's reason, never a guess — a refusal here is a rule, and the desk needs to read it. */
export function serverReason(err: unknown, fallback: string): string {
  return (err as { response?: { data?: { title?: string } } })?.response?.data?.title ?? fallback
}
