import { Link, useLocation } from 'react-router-dom'
import { Plus } from 'lucide-react'

import { cn } from '@/lib/utils'
import { MEMBER_TABS, type MemberTab as MemberTabData } from '@/shared/nav/memberNav'
import { useMyToday } from '@/modules/portal/api/portalApi'

/**
 * A plain Link, not a NavLink, on purpose. NavLink decides "active" by matching its own `to` and then
 * sets aria-current itself, overriding anything passed in — so on a More sub-route like /leaderboard
 * the tab looked active to sighted users but reported nothing to a screen reader. MemberTabBar already
 * computes `active` from alsoMatches, so it owns that state and says so.
 */
function MemberTab({ tab, pathname, dot }: { tab: MemberTabData; pathname: string; dot?: boolean }) {
  const active = pathname === tab.path || (tab.alsoMatches ?? []).some((p) => pathname.startsWith(p))

  return (
    <li className="flex-1">
      <Link
        to={tab.path}
        aria-current={active ? 'page' : undefined}
        className={cn(
          // min-h-16 keeps every target comfortably past the 44px touch minimum.
          'flex min-h-16 flex-col items-center justify-center gap-1 text-xs font-medium transition-colors',
          active ? 'text-primary' : 'text-muted-foreground hover:text-foreground',
        )}
      >
        <span className="relative">
          <tab.icon className={cn('size-6', active && 'stroke-[2.5]')} />
          {/*
            A dot, not a number. The count is on the screen the dot leads to; repeating it here would
            have the member reading "3" twice and reconciling them the moment one lagged. The label
            below carries the meaning for anyone not seeing colour.
          */}
          {dot && (
            <span
              className="absolute -top-0.5 -right-1 size-2.5 rounded-full bg-primary ring-2 ring-background"
              aria-hidden
            />
          )}
        </span>
        {tab.label}
        {dot && <span className="sr-only">, unread message from your coach</span>}
      </Link>
    </li>
  )
}

/**
 * The member's primary navigation: a bottom tab bar, the pattern every consumer fitness app uses.
 *
 * Bottom-anchored rather than a sidebar or drawer because members use this one-handed, mid-session,
 * often sweating — the thumb reaches the bottom of the screen, not a hamburger in the top corner.
 * On desktop it stays as a bottom bar too so the member experience is identical everywhere rather
 * than silently becoming a different product at ≥768px.
 *
 * MEMBER_TABS (data, order, alsoMatches, aria-current logic) is untouched by the redesign — only how
 * the 2nd entry ("Log") renders changed: it's promoted to an elevated circular FAB, centered and
 * floating above the bar rather than sitting flush inside it as a 4th flat icon+label tab. It still
 * points at exactly the same route (`/log-activity`) and still gets an aria-current when active, via
 * the same `alsoMatches`-driven `active` check every other tab uses — the promotion is visual only.
 */
export function MemberTabBar() {
  const { pathname } = useLocation()
  /*
   * The one thing on this bar that is not navigation: whether the member's coach is waiting on them.
   *
   * It reads the home query rather than one of its own, so it costs nothing extra and can never
   * disagree with the count on the Coach screen. It is deliberately NOT a notification in the push
   * sense — nothing in this system can reach a member who has not opened it — but a member who HAS
   * opened it no longer has to go looking, which is the half of the problem that was solvable.
   */
  const today = useMyToday()
  const hasCoachReply = (today.data?.unreadCoachMessages ?? 0) > 0
  const fabIndex = MEMBER_TABS.findIndex((tab) => tab.label === 'Log')
  const fab = MEMBER_TABS[fabIndex]
  const fabActive = fab ? pathname === fab.path || (fab.alsoMatches ?? []).some((p) => pathname.startsWith(p)) : false

  return (
    <nav
      aria-label="Main"
      className="fixed inset-x-0 bottom-0 z-40 border-t border-border bg-background/85 backdrop-blur-md supports-[backdrop-filter]:bg-background/70"
      style={{ paddingBottom: 'env(safe-area-inset-bottom)' }}
    >
      <ul className="mx-auto flex max-w-2xl items-stretch">
        {MEMBER_TABS.map((tab, index) =>
          index === fabIndex ? (
            /*
              The FAB keeps its own equal-width slot in the row and centres itself inside THAT, rather
              than being absolutely positioned against the whole bar. Positioning it at the bar's 50%
              put it on the boundary of its slot, overlapping the next tab's label — the slot's centre
              and the bar's centre are only the same point when the tabs either side are balanced,
              which they are not with four tabs and the second one promoted.
            */
            <li key={tab.path} className="relative flex-1">
              <Link
                to={tab.path}
                aria-current={fabActive ? 'page' : undefined}
                aria-label="Log a workout"
                className={cn(
                  'absolute top-0 left-1/2 flex size-14 -translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-full',
                  'bg-primary text-primary-foreground shadow-[0_8px_22px_-6px_var(--primary)] transition-transform hover:scale-105',
                  fabActive && 'ring-2 ring-primary ring-offset-2 ring-offset-background',
                )}
              >
                <Plus className="size-7" strokeWidth={2.5} />
              </Link>
            </li>
          ) : (
            <MemberTab
              key={tab.path}
              tab={tab}
              pathname={pathname}
              dot={hasCoachReply && (tab.alsoMatches ?? []).some((p) => p.startsWith('/my-coach'))}
            />
          ),
        )}
      </ul>
    </nav>
  )
}
