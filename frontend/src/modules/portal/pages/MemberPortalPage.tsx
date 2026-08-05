import { Link } from 'react-router-dom'
import { CalendarDays, Flame, Gift, NotebookPen, QrCode, Zap } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { StatCard } from '@/shared/components/StatCard'
import { useAuthStore } from '@/stores/authStore'
import { SectionCard, dateFormat, dateTimeFormat, classTimeFormat } from '@/modules/portal/components/portalShared'
import {
  useMyAttendance,
  useMyClassBookings,
  useMyExperience,
  useMyProfile,
  useMyReferrals,
} from '@/modules/portal/api/portalApi'

/**
 * The member's home: who they are, whether their membership is healthy, and what's next.
 *
 * This page used to be the entire portal — 801 lines rendering 36 cards on one route, which meant a
 * member scrolled past their training, nutrition, records, achievements and challenges just to see
 * whether their membership was active. Those now live on their own screens (My Training, My
 * Nutrition, My Progress, Challenges); what stays here is only what belongs on a landing page.
 */
export default function MemberPortalPage() {
  const user = useAuthStore((s) => s.user)
  const profile = useMyProfile()
  const attendance = useMyAttendance({ page: 1, pageSize: 10 })
  const classBookings = useMyClassBookings()
  const referrals = useMyReferrals()
  const experience = useMyExperience()

  if (profile.isError) {
    const status = (profile.error as { response?: { status?: number } })?.response?.status
    return (
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold tracking-tight">Welcome, {user?.firstName}</h1>
        <p className="text-sm text-muted-foreground">
          {status === 404
            ? "Your account isn't linked to a member profile yet. Ask the front desk to link your login to your membership record."
            : 'Something went wrong loading your profile.'}
        </p>
      </div>
    )
  }

  const activeMembership = profile.data?.memberMemberships.find((m) => m.status === 'Active')
  const currentPlanLabel = activeMembership
    ? `${activeMembership.membershipPlanName} — active through ${dateFormat.format(new Date(activeMembership.endDate))}`
    : (profile.data?.memberMemberships[0]?.status ?? 'No membership on file')

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            Welcome, {profile.data?.firstName ?? user?.firstName}
          </h1>
          <p className="text-sm text-muted-foreground">Your membership at a glance.</p>
        </div>
        <Button asChild>
          <Link to="/log-activity">
            <NotebookPen className="size-4" />
            Log today's workout
          </Link>
        </Button>
      </div>

      {profile.isLoading ? (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-28 w-full" />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <StatCard
            label="Membership Status"
            value={profile.data?.status ?? '—'}
            icon={CalendarDays}
            tone={profile.data?.status === 'Active' ? 'success' : 'warning'}
            hint={currentPlanLabel}
          />
          <StatCard label="Member Code" value={profile.data?.memberCode ?? '—'} icon={QrCode} />
          <StatCard
            label="Visits Logged"
            value={(attendance.data?.totalCount ?? 0).toLocaleString()}
            icon={CalendarDays}
          />
        </div>
      )}

      {/* A compact level readout; the full XP ledger, streaks and badges live on My Progress. */}
      {experience.data && (
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="flex items-center gap-2 text-base">
              <Zap className="size-4 text-amber-500" />
              Level {experience.data.level}
            </CardTitle>
            <Button asChild variant="ghost" size="sm">
              <Link to="/my-progress">
                <Flame className="size-4" />
                View progress
              </Link>
            </Button>
          </CardHeader>
          <CardContent className="space-y-1">
            <div className="flex items-center justify-between text-xs text-muted-foreground">
              <span>{experience.data.totalXp.toLocaleString()} XP</span>
              <span>
                {experience.data.xpIntoLevel} / {experience.data.xpForNextLevel} XP to level {experience.data.level + 1}
              </span>
            </div>
            <div className="h-2 overflow-hidden rounded-full bg-muted">
              <div
                className="h-full rounded-full bg-amber-500 transition-all"
                style={{
                  width: `${
                    experience.data.xpForNextLevel > 0
                      ? Math.min(100, Math.round((experience.data.xpIntoLevel / experience.data.xpForNextLevel) * 100))
                      : 0
                  }%`,
                }}
              />
            </div>
          </CardContent>
        </Card>
      )}

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader className="flex-row items-center justify-between gap-2 space-y-0 pb-2">
            <CardTitle className="text-base">Your Upcoming Classes</CardTitle>
            <Button asChild size="sm" variant="outline">
              <Link to="/my-classes">Book a class</Link>
            </Button>
          </CardHeader>
          <CardContent>
            {classBookings.isLoading ? (
              <Skeleton className="h-24 w-full" />
            ) : classBookings.data && classBookings.data.length > 0 ? (
              <ul className="space-y-2 text-sm">
                {classBookings.data.slice(0, 5).map((b) => (
                  <li key={b.bookingId} className="flex items-center justify-between gap-2 border-b pb-2 last:border-0">
                    <div className="flex min-w-0 items-center gap-2">
                      <span
                        className="size-2.5 shrink-0 rounded-full"
                        style={{ backgroundColor: b.colorHex ?? 'var(--muted-foreground)' }}
                      />
                      <span className="truncate font-medium">{b.classTypeName}</span>
                    </div>
                    <span className="shrink-0 text-muted-foreground">
                      {classTimeFormat.format(new Date(b.startsAt))}
                    </span>
                  </li>
                ))}
              </ul>
            ) : (
              <div className="flex flex-col items-center gap-2 py-6 text-center text-sm text-muted-foreground">
                <CalendarDays className="size-6" />
                No classes booked yet.
              </div>
            )}
          </CardContent>
        </Card>

        <SectionCard title="Recent Check-ins">
          {attendance.isLoading ? (
            <Skeleton className="h-40 w-full" />
          ) : attendance.data && attendance.data.items.length > 0 ? (
            <ul className="space-y-2 text-sm">
              {attendance.data.items.slice(0, 8).map((a) => (
                <li key={a.id} className="flex items-center justify-between gap-2 border-b pb-2 last:border-0">
                  <span>{dateTimeFormat.format(new Date(a.checkInAt))}</span>
                  <span className="text-xs text-muted-foreground">{a.method}</span>
                </li>
              ))}
            </ul>
          ) : (
            <div className="flex flex-col items-center gap-2 py-6 text-center text-sm text-muted-foreground">
              <CalendarDays className="size-6" />
              No check-ins recorded yet.
            </div>
          )}
        </SectionCard>
      </div>

      <SectionCard title="Refer a Friend">
        {referrals.isLoading ? (
          <Skeleton className="h-24 w-full" />
        ) : (
          <div className="space-y-3">
            <p className="text-sm text-muted-foreground">
              Bring a friend along — have them mention your member code{' '}
              <span className="font-mono font-medium text-foreground">{referrals.data?.memberCode}</span> at the front
              desk when they sign up.
            </p>
            {referrals.data && referrals.data.referralCount > 0 && (
              <>
                <p className="flex items-center gap-2 text-sm font-medium">
                  <Gift className="size-4 text-emerald-500" />
                  You've brought in {referrals.data.referralCount} member
                  {referrals.data.referralCount === 1 ? '' : 's'} 🎉
                </p>
                <ul className="space-y-1 text-sm">
                  {referrals.data.referredMembers.map((m, i) => (
                    <li key={i} className="flex items-center justify-between border-b pb-1 last:border-0">
                      <span>{m.firstName}</span>
                      <span className="text-xs text-muted-foreground">
                        joined {dateFormat.format(new Date(m.joinDate))}
                      </span>
                    </li>
                  ))}
                </ul>
              </>
            )}
          </div>
        )}
      </SectionCard>
    </div>
  )
}
