import { Link, useNavigate } from 'react-router-dom'
import { ChevronRight, LogOut, QrCode } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { useLogout } from '@/modules/auth/api/authApi'
import { useMyProfile } from '@/modules/portal/api/portalApi'
import { useAuthStore } from '@/stores/authStore'
import { MEMBER_MORE_LINKS, type MemberMoreLink } from '@/shared/nav/memberNav'

const GROUP_ORDER: MemberMoreLink['group'][] = ['Training', 'Community', 'Account']
const renewalFmt = new Intl.DateTimeFormat('en-US', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' })

/**
 * Everything that isn't a daily action. Keeping these off the tab bar is the whole point of the
 * five-slot shell: a member opens the app to log a session or check progress, not to read their
 * membership plan — so those live one tap deeper rather than competing for primary navigation.
 *
 * Membership is the exception the redesign promotes: it was a row in the Account list, indistinguishable
 * from "Account & security", when it is the thing a member actually holds up at the front desk. It
 * becomes a card at the top carrying the plan, its state and the member code, and keeps its own page
 * for the detail behind it.
 */
export default function MorePage() {
  const user = useAuthStore((s) => s.user)
  // The member profile is the authority on a member's name; the login account is only a fallback.
  // Without this the same person is greeted as "Noah" on Home and "Member Demo" here, because the
  // two names come from different rows and nothing forces them to agree.
  const profile = useMyProfile()
  const refreshToken = useAuthStore((s) => s.refreshToken)
  const clearSession = useAuthStore((s) => s.clearSession)
  const logout = useLogout()
  const navigate = useNavigate()

  const handleLogout = () => {
    if (refreshToken) logout.mutate(refreshToken)
    clearSession()
    navigate('/login')
  }

  /**
   * The membership actually in force. Members carry a history of them (renewals are new rows), so
   * the latest-ending one is the current plan; an expired-only history correctly yields the most
   * recent, which is what its own "Expired" badge should then say.
   */
  const membership = [...(profile.data?.memberMemberships ?? [])]
    .sort((a, b) => b.endDate.localeCompare(a.endDate))[0]

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div>
        <h1 className="font-display text-3xl font-black tracking-tight">More</h1>
        <p className="text-sm text-muted-foreground">
          {profile.data
            ? `${profile.data.firstName} ${profile.data.lastName}`
            : user
              ? `${user.firstName} ${user.lastName}`
              : 'Your gym'}
        </p>
      </div>

      {profile.data && (
        <Link
          to="/membership"
          className="block rounded-3xl border border-border bg-card p-5 transition-colors hover:bg-accent"
        >
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">Membership</p>
              {/* Wraps rather than truncates: this is the card's headline, and "Quarterly Stand…"
                  tells a member less than the two lines it would have taken to say it. */}
              <p className="mt-1 font-display text-2xl leading-tight font-black tracking-tight">
                {membership?.membershipPlanName ?? 'No active plan'}
              </p>
              {membership && (
                <p className="mt-0.5 text-sm text-muted-foreground">
                  {/* "Renews" would be a promise the record doesn't make — autoRenew says whether it
                      actually will, and a plan that won't simply ends on that date. */}
                  {membership.autoRenew ? 'Renews' : 'Ends'} {renewalFmt.format(new Date(`${membership.endDate}T00:00:00Z`))}
                </p>
              )}
            </div>
            <span
              className={
                profile.data.status === 'Active'
                  ? 'shrink-0 rounded-full bg-success/15 px-3 py-1 text-[11px] font-bold tracking-wider text-success uppercase'
                  : 'shrink-0 rounded-full bg-muted px-3 py-1 text-[11px] font-bold tracking-wider text-muted-foreground uppercase'
              }
            >
              {profile.data.status}
            </span>
          </div>

          <div className="mt-4 flex items-center gap-3 border-t border-border pt-4">
            <span className="flex size-11 shrink-0 items-center justify-center rounded-2xl bg-primary text-primary-foreground">
              <QrCode className="size-6" />
            </span>
            <span className="min-w-0 flex-1">
              <span className="block text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                Member code
              </span>
              <span className="block truncate font-display text-lg font-black tracking-tight tabular-nums">
                {profile.data.memberCode}
              </span>
            </span>
            <ChevronRight className="size-5 shrink-0 text-muted-foreground" />
          </div>
        </Link>
      )}

      {GROUP_ORDER.map((group) => {
        const links = MEMBER_MORE_LINKS.filter((l) => l.group === group)
        if (links.length === 0) return null

        return (
          <section key={group} className="space-y-2">
            <h2 className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">{group}</h2>
            <div className="overflow-hidden rounded-2xl border border-border bg-card">
              {links.map((link, i) => (
                <Link
                  key={link.path}
                  to={link.path}
                  className={`flex min-h-16 items-center gap-3 px-4 py-3 transition-colors hover:bg-accent ${
                    i > 0 ? 'border-t border-border' : ''
                  }`}
                >
                  <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-muted">
                    <link.icon className="size-5 text-foreground" />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block font-medium">{link.label}</span>
                    <span className="block truncate text-sm text-muted-foreground">{link.description}</span>
                  </span>
                  <ChevronRight className="size-5 shrink-0 text-muted-foreground" />
                </Link>
              ))}
            </div>
          </section>
        )
      })}

      <Button variant="outline" className="h-12 w-full rounded-2xl" onClick={handleLogout}>
        <LogOut className="size-4" />
        Sign out
      </Button>
    </div>
  )
}
