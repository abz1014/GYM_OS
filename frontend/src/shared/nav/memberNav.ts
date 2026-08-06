import { Apple, CalendarDays, Dumbbell, Home, LayoutGrid, MapPin, MessageCircle, NotebookPen, Trophy, TrendingUp, UserCircle, ShieldCheck, Flag, type LucideIcon } from 'lucide-react'

import { useAuthStore } from '@/stores/authStore'

/**
 * A member-only login is one whose permissions are exactly the portal surface and nothing else.
 *
 * Checking it this way (rather than "has portal.view") is deliberate and load-bearing: the member
 * shell replaces the staff sidebar entirely, so any account that also holds a staff permission must
 * keep the admin navigation. DemoDataSeeder never grants portal.view to a staff role, so in practice
 * this is exactly the Member role — but the check stays behavioural rather than role-name based so a
 * future custom role can't accidentally lose its staff nav.
 */
export function useIsMemberOnly(): boolean {
  const permissions = useAuthStore((s) => s.user?.permissions ?? [])
  return permissions.length > 0 && permissions.every((p) => p === 'portal.view')
}

export interface MemberTab {
  label: string
  path: string
  icon: LucideIcon
  /** Routes that should also light this tab up (e.g. "More" stays active on its sub-pages). */
  alsoMatches?: string[]
}

/**
 * Four destinations, deliberately. Fitness-app UX research is consistent that a member surface
 * should carry 3–5 top-level areas with big tap targets; the portal previously exposed seven
 * sidebar items, which is what made a split-up-but-still-busy app feel complicated. Everything else
 * (classes, nutrition, challenges, leaderboard, membership admin) lives one level down under More.
 */
export const MEMBER_TABS: MemberTab[] = [
  { label: 'Home', path: '/portal', icon: Home },
  { label: 'Log', path: '/log-activity', icon: NotebookPen },
  { label: 'Progress', path: '/my-progress', icon: TrendingUp },
  {
    label: 'More',
    path: '/more',
    icon: LayoutGrid,
    // Every path in MEMBER_MORE_LINKS belongs here. Two were missed as they were added — My Coach
    // and Gym Passport — and on those screens no tab lit at all, so the app quietly stopped saying
    // where the member was, and aria-current went with it.
    alsoMatches: [
      '/my-training', '/my-nutrition', '/my-classes', '/my-coach', '/my-passport',
      '/my-challenges', '/leaderboard', '/membership', '/account',
    ],
  },
]

export interface MemberMoreLink {
  label: string
  description: string
  path: string
  icon: LucideIcon
  group: 'Training' | 'Community' | 'Account'
}

/** Secondary destinations, surfaced on the More screen and grouped so it scans in one look. */
export const MEMBER_MORE_LINKS: MemberMoreLink[] = [
  { group: 'Training', label: 'My Training', description: 'Recovery, suggestions and session history', path: '/my-training', icon: Dumbbell },
  { group: 'Training', label: 'My Nutrition', description: "Today's macros, plans and water", path: '/my-nutrition', icon: Apple },
  { group: 'Training', label: 'My Classes', description: 'Book and manage your classes', path: '/my-classes', icon: CalendarDays },
  { group: 'Training', label: 'My Coach', description: 'Talk to your trainer about your training', path: '/my-coach', icon: MessageCircle },
  { group: 'Training', label: 'Gym Passport', description: "What you've used here, and what you haven't", path: '/my-passport', icon: MapPin },
  { group: 'Community', label: 'Leaderboard', description: 'How you rank against your gym', path: '/leaderboard', icon: Trophy },
  { group: 'Community', label: 'Challenges', description: 'Join a challenge and compete', path: '/my-challenges', icon: Flag },
  { group: 'Account', label: 'Membership', description: 'Your plan, member code and referrals', path: '/membership', icon: UserCircle },
  { group: 'Account', label: 'Account & security', description: 'Password, two-factor and sign-in', path: '/account', icon: ShieldCheck },
]
