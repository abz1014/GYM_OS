import { Bell } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { useMyNotifications } from '@/modules/portal/api/portalApi'
import { MemberLoadError, dateTimeFormat, relativeWhen } from '@/modules/portal/components/portalShared'
import { isStale } from '@/shared/lib/queryTrust'

/**
 * Everything the gym has sent this member, in one place they can go back to.
 *
 * Renewal reminders, class cancellations and gym announcements were being dispatched by email and
 * SMS with no record inside the app at all, so a member who deleted the email — or never got it,
 * because the address on file was three years old — had no way to find out what they had been told.
 * The consequences of that land on them: a renewal they didn't know was coming, a class they turned
 * up to that had been called off.
 *
 * This is a LOG, not an inbox. Nothing here is unread, dismissible or actionable, because the
 * backend records what was sent and not whether it was seen — a blue dot here would be a claim the
 * data cannot support.
 */
export default function MyNotificationsPage() {
  const notifications = useMyNotifications()

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div>
        <h1 className="font-display text-3xl font-black tracking-tight">Notifications</h1>
        <p className="text-sm text-muted-foreground">What your gym has sent you.</p>
      </div>

      {notifications.isLoading ? (
        <div className="space-y-2">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-20 w-full rounded-2xl shimmer" />
          ))}
        </div>
      ) : isStale(notifications) ? (
        /* An empty list here says "your gym has told you nothing", which is exactly the belief that
           makes someone miss a renewal. A dropped request must not be allowed to say it. */
        <MemberLoadError
          title="We couldn't load your notifications"
          hint="Anything your gym has sent you is still on record."
          onRetry={() => void notifications.refetch()}
          isRetrying={notifications.isFetching}
        />
      ) : notifications.data && notifications.data.length > 0 ? (
        <ul className="space-y-2">
          {notifications.data.map((n) => (
            <li key={n.id} className="space-y-1.5 rounded-2xl border border-border bg-card p-4">
              <div className="flex items-start justify-between gap-3">
                <p className="min-w-0 font-display text-base font-bold">{n.title}</p>
                {/* How it reached them — the difference between "I never got the email" and "it went
                    to a phone number I changed". */}
                <Badge variant="outline" className="shrink-0">
                  {n.channel}
                </Badge>
              </div>
              {/* A message can arrive with a title and no body. That renders as a title and no body,
                  not as an empty line the member reads as something failing to load. */}
              {n.body && <p className="text-sm text-muted-foreground">{n.body}</p>}
              {/* Relative for reading, exact on hover: "3 days ago" is what a member is scanning for,
                  and the timestamp is what they need if they're disputing one. */}
              <p className="text-xs text-muted-foreground" title={dateTimeFormat.format(new Date(n.occurredAt))}>
                {relativeWhen(n.occurredAt)}
              </p>
            </li>
          ))}
        </ul>
      ) : (
        <div className="flex flex-col items-center gap-2 py-12 text-center">
          <span className="flex size-12 items-center justify-center rounded-full bg-primary/10">
            <Bell className="size-6 text-primary" />
          </span>
          <p className="font-medium">Nothing yet</p>
          <p className="max-w-xs text-sm text-muted-foreground">
            Renewal reminders and gym messages will appear here.
          </p>
        </div>
      )}
    </div>
  )
}
