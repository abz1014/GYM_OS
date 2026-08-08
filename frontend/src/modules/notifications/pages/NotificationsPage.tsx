import { Loader2, Mail, PlayCircle } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { ListEmpty, ListError, ListSkeleton, PageHeader } from '@/shared/components/console'
import { EditNotificationTemplateDialog } from '@/modules/notifications/components/EditNotificationTemplateDialog'
import {
  useNotificationLogs,
  useNotificationTemplates,
  useScheduledNotifications,
  useTriggerNotificationChecks,
} from '@/modules/notifications/api/notificationsApi'

/**
 * The console's tab row is an underline strip; shadcn's Tabs ships a pill sitting in a grey tray.
 * These two strings flatten it into the same shape `FilterTabs` draws.
 *
 * Radix stays underneath rather than being swapped for `FilterTabs` because these tabs genuinely
 * switch panels — sent mail, a queue and a template library are three different things, not three
 * filters over one list — which is the case FilterTabs' own doc says it is not for.
 */
const TAB_LIST_CLASS =
  'h-auto w-full flex-wrap justify-start gap-5 rounded-none border-b border-border bg-transparent p-0'
const TAB_TRIGGER_CLASS =
  '-mb-px h-auto flex-none rounded-none border-0 border-b-2 border-transparent px-0 py-0 pb-2.5 text-muted-foreground shadow-none transition-colors hover:text-foreground data-[state=active]:border-foreground data-[state=active]:bg-transparent data-[state=active]:text-foreground data-[state=active]:shadow-none'

/**
 * Channels and categories arrive as raw C# enum names — "MembershipExpiry", "InApp", "WhatsApp" —
 * and the DTO types both as plain `string`, so there is no exhaustive union to switch on. A split
 * on the camel-case hump handles the ones that are two plain words and keeps working the day a new
 * category ships; the table is only for the names where that split would be wrong.
 */
const LABEL_OVERRIDES: Record<string, string> = {
  InApp: 'In-app',
  Sms: 'SMS',
  WhatsApp: 'WhatsApp',
}

function humanise(value: string) {
  return LABEL_OVERRIDES[value] ?? value.replace(/([a-z\d])([A-Z])/g, (_, head: string, tail: string) => `${head} ${tail.toLowerCase()}`)
}

function statusVariant(status: string) {
  if (status === 'Sent') return 'default' as const
  if (status === 'Failed' || status === 'Cancelled') return 'destructive' as const
  return 'outline' as const
}

const timestampFmt = new Intl.DateTimeFormat('en-US', {
  month: 'short',
  day: 'numeric',
  hour: 'numeric',
  minute: '2-digit',
})

function RunChecksButton() {
  const trigger = useTriggerNotificationChecks()

  return (
    <Button
      size="sm"
      className="rounded-xl"
      disabled={trigger.isPending}
      onClick={() =>
        trigger.mutate(undefined, {
          onSuccess: (result) =>
            toast.success(`Scheduled ${result.scheduledCount} new notification(s), dispatched ${result.dispatchedCount}.`),
          onError: () => toast.error('Failed to run notification checks.'),
        })
      }
    >
      {trigger.isPending ? <Loader2 className="size-4 animate-spin" /> : <PlayCircle />}
      Run checks now
    </Button>
  )
}

function DevMailboxTab() {
  const logsQuery = useNotificationLogs()
  const logs = logsQuery.data

  return (
    <div className="space-y-2">
      {logsQuery.isError && (
        <ListError
          message="We couldn't load the mailbox"
          onRetry={() => logsQuery.refetch()}
          isRetrying={logsQuery.isFetching}
        />
      )}

      {logsQuery.isLoading && <ListSkeleton rows={4} className="h-28 w-full rounded-2xl" />}

      {!logsQuery.isLoading && logs?.length === 0 && (
        <ListEmpty message="Nothing has been sent yet." hint="Run checks now to see what the rules would fire." />
      )}

      {logs?.map((log) => (
        <article key={log.id} className="space-y-1 rounded-2xl border border-border bg-card p-4 shadow-sm">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex min-w-0 items-center gap-2">
              <Mail className="size-4 shrink-0 text-muted-foreground" />
              <p className="truncate font-medium">{log.subject}</p>
              <Badge variant="outline">{humanise(log.channel)}</Badge>
              {!log.success && <Badge variant="destructive">Failed</Badge>}
            </div>
            <span className="text-xs text-muted-foreground tabular-nums">{timestampFmt.format(new Date(log.sentAt))}</span>
          </div>
          <p className="text-sm text-muted-foreground">To: {log.recipientAddress}</p>
          <p className="text-sm">{log.body}</p>
        </article>
      ))}
    </div>
  )
}

function ScheduledTab() {
  const scheduledQuery = useScheduledNotifications()
  const scheduled = scheduledQuery.data

  return (
    <div className="space-y-2">
      {scheduledQuery.isError && (
        <ListError
          message="We couldn't load the queue"
          onRetry={() => scheduledQuery.refetch()}
          isRetrying={scheduledQuery.isFetching}
        />
      )}

      {scheduledQuery.isLoading && <ListSkeleton rows={5} />}

      {!scheduledQuery.isLoading && scheduled?.length === 0 && (
        <ListEmpty message="Nothing is queued." hint="Run checks now to see what the rules would fire." />
      )}

      {scheduled && scheduled.length > 0 && (
        <>
          {/* Mobile: card list — five columns of category, channel and timestamp have no room on a
              phone and end up clipped mid-word. */}
          <div className="space-y-2 md:hidden">
            {scheduled.map((n) => (
              <div key={n.id} className="space-y-1.5 rounded-2xl border border-border bg-card p-3">
                <div className="flex items-center justify-between gap-2">
                  {/* A scheduled notification can point at a member, at a staff user, or at
                      neither, and ScheduledNotificationDto resolves the name to null in the last
                      case. "No recipient" is what that means; it is not a name we failed to load. */}
                  <p className="truncate font-medium">{n.recipientName ?? 'No recipient'}</p>
                  <Badge variant={statusVariant(n.status)} className="shrink-0">
                    {humanise(n.status)}
                  </Badge>
                </div>
                <p className="text-sm text-muted-foreground">
                  {humanise(n.category)} · {humanise(n.channel)}
                </p>
                <p className="text-sm text-muted-foreground tabular-nums">
                  {timestampFmt.format(new Date(n.scheduledFor))}
                </p>
              </div>
            ))}
          </div>

          {/* Desktop / tablet: full table */}
          <div className="hidden overflow-hidden rounded-2xl border border-border bg-card md:block">
            <Table>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                    Recipient
                  </TableHead>
                  <TableHead className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                    Category
                  </TableHead>
                  <TableHead className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                    Channel
                  </TableHead>
                  <TableHead className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                    Scheduled for
                  </TableHead>
                  <TableHead className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                    Status
                  </TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {scheduled.map((n) => (
                  <TableRow key={n.id}>
                    <TableCell className="font-medium">{n.recipientName ?? '—'}</TableCell>
                    <TableCell className="text-muted-foreground">{humanise(n.category)}</TableCell>
                    <TableCell className="text-muted-foreground">{humanise(n.channel)}</TableCell>
                    <TableCell className="text-muted-foreground tabular-nums">
                      {timestampFmt.format(new Date(n.scheduledFor))}
                    </TableCell>
                    <TableCell>
                      <Badge variant={statusVariant(n.status)}>{humanise(n.status)}</Badge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </>
      )}
    </div>
  )
}

function TemplatesTab() {
  const templatesQuery = useNotificationTemplates()
  const templates = templatesQuery.data

  return (
    <div className="space-y-4">
      {templatesQuery.isError && (
        <ListError
          message="We couldn't load the templates"
          onRetry={() => templatesQuery.refetch()}
          isRetrying={templatesQuery.isFetching}
        />
      )}

      {/* One ListSkeleton per grid column rather than one tall stack, so the placeholders land in
          the same two columns the cards will occupy. */}
      {templatesQuery.isLoading && (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {Array.from({ length: 2 }).map((_, i) => (
            <ListSkeleton key={i} rows={2} className="h-40 w-full rounded-2xl" />
          ))}
        </div>
      )}

      {!templatesQuery.isLoading && templates?.length === 0 && <ListEmpty message="No templates configured." />}

      {templates && templates.length > 0 && (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {templates.map((template) => (
            <article
              key={template.id}
              className="space-y-2 rounded-2xl border border-border bg-card p-4 shadow-sm"
            >
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <p className="truncate font-medium">{template.subject}</p>
                  <p className="truncate font-mono text-xs text-muted-foreground tabular-nums">{template.code}</p>
                </div>
                <EditNotificationTemplateDialog template={template} />
              </div>
              <p className="text-sm text-muted-foreground">{template.bodyTemplate}</p>
              <div className="flex flex-wrap gap-1">
                <Badge variant="outline">{humanise(template.category)}</Badge>
                <Badge variant="outline">{humanise(template.channel)}</Badge>
                {/*
                  Only the off state is called out. "Active" on every card but one is a badge staff
                  learn to stop reading, and inactive is the state that explains why a member never
                  got the message.
                */}
                {!template.isActive && <Badge variant="secondary">Inactive</Badge>}
              </div>
            </article>
          ))}
        </div>
      )}
    </div>
  )
}

/**
 * Everything the system sends on its own: what already went out, what is queued to go, and the
 * wording behind both.
 *
 * There is no stat row here, and the tabs carry no counts, for one reason that applies to both.
 * `/api/notifications/logs` and `/api/notifications/scheduled` are capped server-side at the most
 * recent hundred rows and report no total, so `logs.length` is the size of a window, not a volume,
 * and a delivery or failure rate computed from it would silently mean "of the last hundred" while
 * reading as "overall" — the number would keep looking plausible long after it stopped being true.
 * The run-checks response does return real counts (scheduled, dispatched), but those describe one
 * run rather than the state of the queue, which is why they stay in that run's toast.
 */
export default function NotificationsPage() {
  return (
    <div className="space-y-4">
      {/*
        "Demo mode" is stated flatly rather than read from anywhere, and that is correct here: the
        email, SMS and WhatsApp senders are registered as no-ops unconditionally — there is no flag
        that turns real delivery on — so this build cannot send. The title matches the rail's label
        for this module exactly; a page that renames itself on arrival reads as the wrong page.
      */}
      <PageHeader
        eyebrow="Demo mode"
        title="Notification Center"
        description="Automated alerts, the in-app Dev Mailbox, and the templates behind them. Sends are logged, never delivered."
        actions={<RunChecksButton />}
      />

      <Tabs defaultValue="mailbox" className="gap-4">
        <TabsList className={TAB_LIST_CLASS}>
          <TabsTrigger className={TAB_TRIGGER_CLASS} value="mailbox">
            Dev Mailbox
          </TabsTrigger>
          <TabsTrigger className={TAB_TRIGGER_CLASS} value="scheduled">
            Scheduled
          </TabsTrigger>
          <TabsTrigger className={TAB_TRIGGER_CLASS} value="templates">
            Templates
          </TabsTrigger>
        </TabsList>

        <TabsContent value="mailbox">
          <DevMailboxTab />
        </TabsContent>
        <TabsContent value="scheduled">
          <ScheduledTab />
        </TabsContent>
        <TabsContent value="templates">
          <TemplatesTab />
        </TabsContent>
      </Tabs>
    </div>
  )
}
