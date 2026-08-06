import { Link, useLocation } from 'react-router-dom'

import { cn } from '@/lib/utils'
import { MEMBER_TABS } from '@/shared/nav/memberNav'

/**
 * The member's primary navigation: a bottom tab bar, the pattern every consumer fitness app uses.
 *
 * Bottom-anchored rather than a sidebar or drawer because members use this one-handed, mid-session,
 * often sweating — the thumb reaches the bottom of the screen, not a hamburger in the top corner.
 * On desktop it stays as a bottom bar too so the member experience is identical everywhere rather
 * than silently becoming a different product at ≥768px.
 */
export function MemberTabBar() {
  const { pathname } = useLocation()

  return (
    <nav
      aria-label="Main"
      className="fixed inset-x-0 bottom-0 z-40 border-t bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/80"
      // Keeps the bar clear of the iOS home indicator / Android gesture bar.
      style={{ paddingBottom: 'env(safe-area-inset-bottom)' }}
    >
      <ul className="mx-auto flex max-w-2xl items-stretch">
        {MEMBER_TABS.map((tab) => {
          const active = pathname === tab.path || (tab.alsoMatches ?? []).some((p) => pathname.startsWith(p))

          return (
            <li key={tab.path} className="flex-1">
              {/*
                A plain Link, not a NavLink, on purpose. NavLink decides "active" by matching its own
                `to` and then sets aria-current itself, overriding anything passed in — so on a More
                sub-route like /leaderboard the tab looked active to sighted users but reported
                nothing to a screen reader. This tab bar already computes `active` from alsoMatches,
                so it owns that state and says so.
              */}
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
        })}
      </ul>
    </nav>
  )
}
