import { Apple, CalendarDays, Dumbbell, Home, LayoutGrid, MapPin, MessageCircle, NotebookPen, Shield, Trophy, TrendingUp, UserCircle, ShieldCheck, Flag, type LucideIcon } from 'lucide-react'

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
 * Five slots: four destinations either side of the centre action. Fitness-app UX research is
 * consistent that a member surface should carry 3–5 top-level areas with big tap targets; the portal
 * previously exposed seven sidebar items, which is what made a split-up-but-still-busy app feel
 * complicated. Everything else (nutrition, classes, coach, passport, challenges, leaderboard,
 * membership admin) lives one level down under More.
 *
 * "Log" sits at index 2 — dead centre — because MemberTabBar renders it as the elevated round action
 * rather than a flat tab, and a centre slot is only actually centred with an equal number of tabs on
 * each side. It keeps its entry here rather than being special-cased in the bar so its route,
 * matching and aria-current all stay identical to every other tab's.
 *
 * "Train" was promoted out of More for that symmetry, and because it is the one member destination
 * that answers "what should I do in the gym right now" — the question the tab bar should be able to
 * answer without a detour through a menu.
 */
export const MEMBER_TABS: MemberTab[] = [
  { label: 'Home', path: '/portal', icon: Home },
  { label: 'Train', path: '/my-training', icon: Dumbbell },
  // The centre action starts a live session. It used to open the after-the-fact logger, which is a
  // different job and now lives under More — a member tapping the big button mid-session wants to
  // record the set they just did, not fill in a form about a workout that already ended.
  { label: 'Log', path: '/workout', icon: NotebookPen },
  // Rank, not Progress, and the swap is forced rather than preferred: the bar holds four flat tabs
  // around the centre action, and a fifth would push that action off-centre — the one layout rule
  // this file already defends. Rank wins the slot because it answers "where do I stand" in one look,
  // which is the question a member opens the app with; Progress is a sit-down-and-study screen, which
  // is exactly what a More destination is for. It is linked prominently from the top of Rank.
  { label: 'Rank', path: '/my-rank', icon: Shield },
  {
    label: 'More',
    path: '/more',
    icon: LayoutGrid,
    // Every path in MEMBER_MORE_LINKS belongs here. Two were missed as they were added — My Coach
    // and Gym Passport — and on those screens no tab lit at all, so the app quietly stopped saying
    // where the member was, and aria-current went with it. (/my-training is absent on purpose: it is
    // its own tab now, and listing it here would light two tabs at once.)
    alsoMatches: [
      '/my-nutrition', '/my-classes', '/my-coach', '/my-passport', '/my-progress',
      '/my-challenges', '/leaderboard', '/membership', '/account', '/log-activity',
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

/**
 * Secondary destinations, surfaced on the More screen and grouped so it scans in one look.
 * My Training is deliberately absent — it is a top-level tab now, and a destination that is both a
 * tab and a More row is the same place reached two ways, which is how a menu starts feeling long.
 */
export const MEMBER_MORE_LINKS: MemberMoreLink[] = [
  { group: 'Training', label: 'My Nutrition', description: "Today's macros, plans and water", path: '/my-nutrition', icon: Apple },
  { group: 'Training', label: 'My Classes', description: 'Book and manage your classes', path: '/my-classes', icon: CalendarDays },
  { group: 'Training', label: 'My Coach', description: 'Talk to your trainer about your training', path: '/my-coach', icon: MessageCircle },
  { group: 'Training', label: 'Gym Passport', description: "What you've used here, and what you haven't", path: '/my-passport', icon: MapPin },
  // Reachable from here since the centre action became the live session. Everything recorded after
  // the fact — a workout you didn't log at the time, a meal, a measurement — is one job, one screen.
  { group: 'Training', label: 'Log something else', description: 'A past workout, a meal or your measurements', path: '/log-activity', icon: NotebookPen },
  // Left the tab bar when Rank took the slot. Still a first-class destination — the charts, goals,
  // measurements, mastery and timeline all live here, and Rank links straight to it.
  { group: 'Training', label: 'Progress', description: 'Volume, records, measurements and your story', path: '/my-progress', icon: TrendingUp },
  { group: 'Community', label: 'Leaderboard', description: 'How you rank against your gym', path: '/leaderboard', icon: Trophy },
  { group: 'Community', label: 'Challenges', description: 'Join a challenge and compete', path: '/my-challenges', icon: Flag },
  { group: 'Account', label: 'Membership', description: 'Your plan, member code and referrals', path: '/membership', icon: UserCircle },
  { group: 'Account', label: 'Account & security', description: 'Password, two-factor and sign-in', path: '/account', icon: ShieldCheck },
]
