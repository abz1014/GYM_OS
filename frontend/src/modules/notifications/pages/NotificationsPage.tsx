import { Loader2, Mail, PlayCircle } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { EditNotificationTemplateDialog } from '@/modules/notifications/components/EditNotificationTemplateDialog'
import {
  useNotificationLogs,
  useNotificationTemplates,
  useScheduledNotifications,
  useTriggerNotificationChecks,
} from '@/modules/notifications/api/notificationsApi'

function statusVariant(status: string) {
  if (status === 'Sent') return 'default' as const
  if (status === 'Failed' || status === 'Cancelled') return 'destructive' as const
  return 'outline' as const
}

function RunChecksButton() {
  const trigger = useTriggerNotificationChecks()

  return (
    <Button
      size="sm"
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
  const { data, isLoading } = useNotificationLogs()

  if (isLoading) {
    return (
      <div className="space-y-2">
        {Array.from({ length: 4 }).map((_, i) => (
          <Skeleton key={i} className="h-20 w-full" />
        ))}
      </div>
    )
  }

  if (!data?.length) {
    return <p className="text-sm text-muted-foreground">No notifications sent yet. Try "Run checks now".</p>
  }

  return (
    <div className="space-y-2">
      {data.map((log) => (
        <Card key={log.id}>
          <CardContent className="space-y-1 p-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div className="flex items-center gap-2">
                <Mail className="size-4 text-muted-foreground" />
                <p className="font-medium">{log.subject}</p>
                <Badge variant="outline">{log.channel}</Badge>
                {!log.success && <Badge variant="destructive">Failed</Badge>}
              </div>
              <span className="text-xs text-muted-foreground">{new Date(log.sentAt).toLocaleString()}</span>
            </div>
            <p className="text-sm text-muted-foreground">To: {log.recipientAddress}</p>
            <p className="text-sm">{log.body}</p>
          </CardContent>
        </Card>
      ))}
    </div>
  )
}

function ScheduledTab() {
  const { data, isLoading } = useScheduledNotifications()

  if (isLoading) {
    return (
      <div className="space-y-2">
        {Array.from({ length: 4 }).map((_, i) => (
          <Skeleton key={i} className="h-16 w-full" />
        ))}
      </div>
    )
  }

  if (!data?.length) {
    return <p className="text-sm text-muted-foreground">No scheduled notifications yet. Try "Run checks now".</p>
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-left text-muted-foreground">
            <th className="py-2 pr-4">Recipient</th>
            <th className="py-2 pr-4">Category</th>
            <th className="py-2 pr-4">Channel</th>
            <th className="py-2 pr-4">Scheduled For</th>
            <th className="py-2 pr-4">Status</th>
          </tr>
        </thead>
        <tbody>
          {data.map((n) => (
            <tr key={n.id} className="border-b last:border-0">
              <td className="py-2 pr-4">{n.recipientName ?? '—'}</td>
              <td className="py-2 pr-4">{n.category}</td>
              <td className="py-2 pr-4">{n.channel}</td>
              <td className="py-2 pr-4">{new Date(n.scheduledFor).toLocaleString()}</td>
              <td className="py-2 pr-4">
                <Badge variant={statusVariant(n.status)}>{n.status}</Badge>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function TemplatesTab() {
  const { data, isLoading } = useNotificationTemplates()

  if (isLoading) {
    return (
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        {Array.from({ length: 4 }).map((_, i) => (
          <Skeleton key={i} className="h-32 w-full" />
        ))}
      </div>
    )
  }

  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
      {data?.map((template) => (
        <Card key={template.id}>
          <CardContent className="space-y-2 p-3">
            <div className="flex items-start justify-between gap-2">
              <div>
                <p className="font-medium">{template.subject}</p>
                <p className="text-xs text-muted-foreground">{template.code}</p>
              </div>
              <EditNotificationTemplateDialog template={template} />
            </div>
            <p className="text-sm text-muted-foreground">{template.bodyTemplate}</p>
            <div className="flex flex-wrap gap-1">
              <Badge variant="outline">{template.category}</Badge>
              <Badge variant="outline">{template.channel}</Badge>
              <Badge variant={template.isActive ? 'default' : 'secondary'}>{template.isActive ? 'Active' : 'Inactive'}</Badge>
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  )
}

export default function NotificationsPage() {
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Notification Center</h1>
          <p className="text-sm text-muted-foreground">
            Automated alerts, in-app Dev Mailbox, and message templates — demo mode logs instead of sending.
          </p>
        </div>
        <RunChecksButton />
      </div>

      <Tabs defaultValue="mailbox">
        <TabsList>
          <TabsTrigger value="mailbox">Dev Mailbox</TabsTrigger>
          <TabsTrigger value="scheduled">Scheduled</TabsTrigger>
          <TabsTrigger value="templates">Templates</TabsTrigger>
        </TabsList>

        <TabsContent value="mailbox"><DevMailboxTab /></TabsContent>
        <TabsContent value="scheduled"><ScheduledTab /></TabsContent>
        <TabsContent value="templates"><TemplatesTab /></TabsContent>
      </Tabs>
    </div>
  )
}
