import { Link } from 'react-router-dom'
import { Building2, CalendarDays, Flame, Gift, Mail, NotebookPen, Phone, QrCode, Receipt, Zap } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { StatCard } from '@/shared/components/StatCard'
import { useAuthStore } from '@/stores/authStore'
import type { InvoiceStatus } from '@/modules/billing/api/billingApi'
import { ManageMembership } from '@/modules/portal/components/ManageMembership'
import {
  MemberEmptyState,
  MemberLoadError,
  SectionCard,
  classTimeFormat,
  dateFormat,
  dateTimeFormat,
  money,
} from '@/modules/portal/components/portalShared'
import {
  checkInMethodLabel,
  currentMembership,
  statusLabel,
  useMyAttendance,
  useMyClassBookings,
  useMyExperience,
  useMyGym,
  useMyInvoices,
  useMyProfile,
  useMyReferrals,
  type MyInvoice,
} from '@/modules/portal/api/portalApi'
import { isStale } from '@/shared/lib/queryTrust'

const MS_PER_DAY = 86_400_000

/** DateOnly wire values ("2026-08-27") are calendar dates — parse at local midnight, not UTC. */
function parseDateOnly(value: string): Date {
  return new Date(`${value.slice(0, 10)}T00:00:00`)
}

/**
 * An invoice's state, coloured by what it asks of the member: nothing (Paid), attention (Overdue),
 * or simply information. Every status the billing enum has is listed — a chip that falls through to
 * a default would print a state nobody chose a colour for.
 */
const INVOICE_STATUS_VARIANT: Record<InvoiceStatus, 'default' | 'secondary' | 'destructive' | 'outline' | 'success' | 'warning'> = {
  Draft: 'outline',
  Issued: 'secondary',
  PartiallyPaid: 'warning',
  Paid: 'success',
  Overdue: 'destructive',
  Cancelled: 'outline',
  Refunded: 'outline',
}

function InvoiceRow({ invoice }: { invoice: MyInvoice }) {
  // "Paid X of Y" only where it says something the total and the chip do not: a deposit against a
  // bill that is still open. A fully paid invoice already reads as paid, and a wholly unpaid one has
  // nothing part-paid about it.
  const partlyPaid = invoice.paidAmount > 0 && invoice.paidAmount < invoice.totalAmount

  return (
    <li className="flex items-start justify-between gap-3 border-b py-3 last:border-0">
      <div className="min-w-0">
        <p className="truncate text-sm font-medium">{invoice.invoiceNumber}</p>
        <p className="mt-0.5 text-xs text-muted-foreground">
          {dateFormat.format(parseDateOnly(invoice.issueDate))}
        </p>
        {partlyPaid && (
          <p className="mt-0.5 text-xs text-muted-foreground">
            Paid {money(invoice.paidAmount, invoice.currency)} of {money(invoice.totalAmount, invoice.currency)}
          </p>
        )}
      </div>
      <div className="flex shrink-0 flex-col items-end gap-1">
        <span className="text-sm font-semibold tabular-nums">
          {money(invoice.totalAmount, invoice.currency)}
        </span>
        <Badge variant={INVOICE_STATUS_VARIANT[invoice.status]}>{statusLabel(invoice.status)}</Badge>
      </div>
    </li>
  )
}

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
  const invoices = useMyInvoices()
  const gym = useMyGym()

  if (isStale(profile)) {
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

  // The same helper the More card uses — the two screens used to compute "current" differently
  // and could name different plans for the same person.
  const membership = currentMembership(profile.data?.memberMemberships)
  // parseDateOnly, not `new Date(...)`: the end date is a calendar date, and parsing it as UTC
  // midnight renders as the PREVIOUS day for anyone west of Greenwich. That would now disagree
  // by one day with the auto-renew line right below, which states the same date from the same field.
  const currentPlanLabel = membership?.status === 'Active'
    ? `${membership.membershipPlanName} — active through ${dateFormat.format(parseDateOnly(membership.endDate))}`
    : membership
      ? `${membership.membershipPlanName} — ${statusLabel(membership.status).toLowerCase()}`
      : 'No membership on file'

  /*
   * What it cost and over what term — the first question anyone asks about a membership, and the app
   * held the answer (pricePaid, currency, the two dates) without ever printing it. The term is
   * measured between the membership's own dates rather than read off a plan duration, because those
   * dates are what the member is actually bought for; a renewal that started late is a shorter term
   * and saying otherwise would be a rate the gym never charged.
   */
  const priceLabel = membership
    ? `${money(membership.pricePaid, membership.currency)} / ${Math.max(
        1,
        Math.round(
          (parseDateOnly(membership.endDate).getTime() - parseDateOnly(membership.startDate).getTime()) / MS_PER_DAY,
        ),
      )} days`
    : null

  // Only where the plan actually grants freeze days. "0 of 0 freeze days left" on a plan that has no
  // freeze allowance invents an entitlement and then reports it exhausted.
  const freezeLabel =
    membership && membership.planMaxFreezeDays !== null && membership.planMaxFreezeDays > 0
      ? `${Math.max(0, membership.planMaxFreezeDays - membership.freezeDaysUsed)} of ${
          membership.planMaxFreezeDays
        } freeze days left`
      : null

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
            hint={[currentPlanLabel, priceLabel, freezeLabel].filter(Boolean).join(' · ')}
          />
          <StatCard label="Member Code" value={profile.data?.memberCode ?? '—'} icon={QrCode} />
          {/*
            An em dash, never a zero. "Visits Logged: 0" beside a green "Active" membership badge is
            the most confident thing on this screen and it was being printed by a failed attendance
            call — telling someone who has trained three times a week for a year that the gym has no
            record of them ever coming in. The other two tiles already fall back to a dash for the
            same reason; this one had a `?? 0` because a count feels like it has a safe default, and
            it is the one figure here where it does not.
          */}
          <StatCard
            label="Visits Logged"
            value={attendance.data ? attendance.data.totalCount.toLocaleString() : '—'}
            icon={CalendarDays}
            hint={isStale(attendance) ? "Couldn't load your visits" : undefined}
          />
        </div>
      )}

      {/*
        The membership as something a member can act on, not only read.
        Everything reachable here used to require standing at the desk during opening hours.
      */}
      <SectionCard title="Manage membership">
        {profile.isLoading ? (
          <Skeleton className="h-40 w-full" />
        ) : membership ? (
          <ManageMembership membership={membership} />
        ) : (
          // The profile loaded and genuinely has no membership row. Not an error, and not a set of
          // dead buttons either — there is nothing here to freeze, renew or cancel.
          <p className="py-2 text-sm text-muted-foreground">
            There's no membership on your record to manage yet. The front desk can set one up for you.
          </p>
        )}
      </SectionCard>

      <SectionCard title="Invoices &amp; payments">
        {invoices.isLoading ? (
          <Skeleton className="h-32 w-full" />
        ) : isStale(invoices) ? (
          /* An empty billing list reads as "I owe nothing", which is the single most expensive thing
             a dropped request could say on this page. */
          <MemberLoadError
            title="We couldn't load your invoices"
            hint="Every payment you've made is still recorded against your account."
            onRetry={() => void invoices.refetch()}
            isRetrying={invoices.isFetching}
          />
        ) : invoices.data && invoices.data.length > 0 ? (
          <ul>
            {invoices.data.map((invoice) => (
              <InvoiceRow key={invoice.id} invoice={invoice} />
            ))}
          </ul>
        ) : (
          <MemberEmptyState
            icon={Receipt}
            title="Nothing billed yet"
            hint="Invoices for your membership and anything you buy at the gym will appear here."
          />
        )}
      </SectionCard>

      {/*
        Where the gym actually is, and how to reach a human in it.
        Half the copy in this app used to end at "ask at the front desk" without ever saying where the
        front desk is or how to call it. Each field is printed only when the gym has filled it in —
        an invented address sends someone across town.
      */}
      <SectionCard title="Your gym">
        {gym.isLoading ? (
          <Skeleton className="h-24 w-full" />
        ) : isStale(gym) ? (
          <MemberLoadError
            title="We couldn't load your gym's details"
            hint="Your branch hasn't gone anywhere — we just can't reach it right now."
            onRetry={() => void gym.refetch()}
            isRetrying={gym.isFetching}
          />
        ) : gym.data ? (
          (() => {
            const address = [gym.data.addressLine, gym.data.city, gym.data.country].filter(Boolean).join(', ')
            const hasAnything = Boolean(
              gym.data.branchName || address || gym.data.supportEmail || gym.data.supportPhone,
            )

            if (!hasAnything) {
              return (
                <p className="py-2 text-sm text-muted-foreground">
                  Your gym hasn't added its address or contact details yet.
                </p>
              )
            }

            return (
              <div className="space-y-3 text-sm">
                <div className="flex items-start gap-2">
                  <Building2 className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
                  <div className="min-w-0">
                    {gym.data.branchName && <p className="font-medium">{gym.data.branchName}</p>}
                    <p className="text-muted-foreground">
                      {address || 'No address on file for this branch yet.'}
                    </p>
                  </div>
                </div>
                {/* Links, not text: on a phone this is the difference between contacting the gym and
                    copying a number out by hand. */}
                {gym.data.supportPhone && (
                  <a
                    className="flex min-h-11 items-center gap-2 font-medium text-primary"
                    href={`tel:${gym.data.supportPhone}`}
                  >
                    <Phone className="size-4 shrink-0" />
                    {gym.data.supportPhone}
                  </a>
                )}
                {gym.data.supportEmail && (
                  <a
                    className="flex min-h-11 items-center gap-2 font-medium break-all text-primary"
                    href={`mailto:${gym.data.supportEmail}`}
                  >
                    <Mail className="size-4 shrink-0" />
                    {gym.data.supportEmail}
                  </a>
                )}
              </div>
            )
          })()
        ) : null}
      </SectionCard>

      {/* A compact level readout; the XP ledger and the badges they've unlocked live on My Progress,
          the latter on the timeline alongside the session that earned each one. */}
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
            {/* "No classes booked yet." is how a member misses a class they paid for: the booking
                exists, the request to list it didn't come back, and nothing on the screen suggests
                looking again. Each panel here fails on its own — the profile above has already
                loaded by this point, so one dead endpoint takes out one card and not the page. */}
            {classBookings.isLoading ? (
              <Skeleton className="h-24 w-full" />
            ) : isStale(classBookings) ? (
              <MemberLoadError
                title="We couldn't load your classes"
                hint="Any bookings you've made are still there."
                onRetry={() => void classBookings.refetch()}
                isRetrying={classBookings.isFetching}
              />
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
          ) : isStale(attendance) ? (
            <MemberLoadError
              title="We couldn't load your check-ins"
              hint="Every visit you've made is still on your record."
              onRetry={() => void attendance.refetch()}
              isRetrying={attendance.isFetching}
            />
          ) : attendance.data && attendance.data.items.length > 0 ? (
            <ul className="space-y-2 text-sm">
              {attendance.data.items.slice(0, 8).map((a) => (
                <li key={a.id} className="flex items-center justify-between gap-2 border-b pb-2 last:border-0">
                  <span>{dateTimeFormat.format(new Date(a.checkInAt))}</span>
                  <span className="text-xs text-muted-foreground">{checkInMethodLabel(a.method)}</span>
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
        {/* The sentence below has the member's code embedded mid-clause, so a failed request used to
            render "have them mention your member code ␣ at the front desk" — a hole a member cannot
            read as an error and will happily send a friend to the desk with. */}
        {referrals.isLoading ? (
          <Skeleton className="h-24 w-full" />
        ) : isStale(referrals) ? (
          <MemberLoadError
            title="We couldn't load your referral code"
            hint="Ask at the front desk and they can look it up."
            onRetry={() => void referrals.refetch()}
            isRetrying={referrals.isFetching}
          />
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
