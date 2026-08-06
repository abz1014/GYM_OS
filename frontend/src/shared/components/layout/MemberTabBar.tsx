import { Link, useLocation } from 'react-router-dom'
import { Plus } from 'lucide-react'

import { cn } from '@/lib/utils'
import { MEMBER_TABS, type MemberTab as MemberTabData } from '@/shared/nav/memberNav'

/**
 * A plain Link, not a NavLink, on purpose. NavLink decides "active" by matching its own `to` and then
 * sets aria-current itself, overriding anything passed in — so on a More sub-route like /leaderboard
 * the tab looked active to sighted users but reported nothing to a screen reader. MemberTabBar already
 * computes `active` from alsoMatches, so it owns that state and says so.
 */
function MemberTab({ tab, pathname }: { tab: MemberTabData; pathname: string }) {
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
        <tab.icon className={cn('size-6', active && 'stroke-[2.5]')} />
        {tab.label}
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
            <MemberTab key={tab.path} tab={tab} pathname={pathname} />
          ),
        )}
      </ul>
    </nav>
  )
}
