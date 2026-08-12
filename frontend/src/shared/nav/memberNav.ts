import { Apple, CalendarDays, Dumbbell, Home, LayoutGrid, MapPin, MessageCircle, NotebookPen, Shield, Trophy, TrendingUp, ShieldCheck, type LucideIcon } from 'lucide-react'

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
  /**
   * Rendered as the elevated round action rather than a flat tab. Exactly one tab carries this.
   *
   * A flag rather than a label match: MemberTabBar used to find the centre button with
   * `tab.label === 'Log'`, so renaming the tab demoted it to a flat tab with no compile error.
   */
  isCentre?: boolean
}

/**
 * Five slots: four destinations either side of the centre action. Fitness-app UX research is
 * consistent that a member surface should carry 3–5 top-level areas with big tap targets; the portal
 * previously exposed seven sidebar items, which is what made a split-up-but-still-busy app feel
 * complicated.
 *
 * TRAIN IS THE CENTRE ACTION. The bar used to carry Train as a flat tab AND a "Log" centre button
 * pointing straight at the session recorder, so the PRIMARY navigation offered two doors into one
 * job — and the one a member hit first skipped the screen that tells them what to do and which
 * muscles still need rest. One door in the tab bar now, through Train.
 *
 * Being precise about the scope of that, because an earlier version of this comment overstated it:
 * this is about primary navigation, not about the only code path that can write a workout.
 * /log-activity still carries a Workout tab, deliberately — recording a session you did on Tuesday
 * and forgot to log is a genuinely different job from recording the set you just finished, and it
 * lives one level down under More where after-the-fact admin belongs.
 *
 * COMMUNITY TOOK THE FREED SLOT. The leaderboard and challenges were reachable from exactly one
 * place in the entire app — a row on the More page, below a full-bleed membership card and a
 * six-row Training group, which on a phone is below the fold. A social feature nobody can find is
 * not a social feature.
 *
 * Index 2 is dead centre and MemberTabBar renders that entry as the elevated round action; a centre
 * slot is only actually centred with an equal number of tabs either side, so this array's length and
 * the promoted entry's position are both load-bearing. MemberTabBar finds it by `isCentre` rather
 * than by matching a label string, which used to mean renaming the tab silently demoted it.
 */
export const MEMBER_TABS: MemberTab[] = [
  { label: 'Home', path: '/portal', icon: Home },
  // Rank, not Progress: it answers "where do I stand" in one look, which is the question a member
  // opens the app with. Progress is a sit-down-and-study screen and is linked from the top of Rank.
  { label: 'Rank', path: '/my-rank', icon: Shield },
  {
    label: 'Train',
    path: '/my-training',
    icon: Dumbbell,
    isCentre: true,
    // The live session recorder is reached FROM Train, so the button stays lit while a member is
    // mid-session rather than the bar going dark on the screen they spend the most time on.
    alsoMatches: ['/workout'],
  },
  { label: 'Community', path: '/community', icon: Trophy, alsoMatches: ['/leaderboard', '/my-challenges'] },
  {
    label: 'More',
    path: '/more',
    icon: LayoutGrid,
    // Every path in MEMBER_MORE_LINKS belongs here. Two were missed as they were added — My Coach
    // and Gym Passport — and on those screens no tab lit at all, so the app quietly stopped saying
    // where the member was, and aria-current went with it. (/my-training and the community paths are
    // absent on purpose: they are their own tabs, and listing them would light two tabs at once.)
    alsoMatches: [
      '/my-nutrition', '/my-classes', '/my-coach', '/my-passport', '/my-progress',
      '/membership', '/account', '/log-activity',
    ],
  },
]

export interface MemberMoreLink {
  label: string
  description: string
  path: string
  icon: LucideIcon
  group: 'Training' | 'Account'
}

/**
 * Secondary destinations, surfaced on the More screen and grouped so it scans in one look.
 *
 * My Training and the two community screens are deliberately absent — they are top-level tabs now,
 * and a destination that is both a tab and a More row is the same place reached two ways, which is
 * how a menu starts feeling long.
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
  // Membership is deliberately absent: the More screen already opens with a full-bleed membership
  // hero card linking to the same place, and one screen offering one destination twice is how a
  // menu starts feeling longer than it is.
  { group: 'Account', label: 'Account & security', description: 'Password, two-factor and sign-in', path: '/account', icon: ShieldCheck },
]
