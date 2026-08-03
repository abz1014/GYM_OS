import { useParams, Link } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { PIPELINE_STAGES, useLead, useUpdateLeadStage, type LeadStage } from '@/modules/crm/api/crmApi'
import { AddLeadActivityDialog } from '@/modules/crm/components/AddLeadActivityDialog'
import { CompleteActivityButton } from '@/modules/crm/components/CompleteActivityButton'

const STAGE_LABELS: Record<LeadStage, string> = {
  Lead: 'Lead',
  FollowUp: 'Follow-up',
  Trial: 'Trial',
  Member: 'Member',
  Lost: 'Lost',
}

export default function LeadDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data: lead, isLoading } = useLead(id)
  const updateStage = useUpdateLeadStage()

  if (isLoading || !lead) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-40 w-full" />
      </div>
    )
  }

  const sortedActivities = [...lead.activities].sort(
    (a, b) => new Date(b.dueDate ?? 0).getTime() - new Date(a.dueDate ?? 0).getTime()
  )

  return (
    <div className="space-y-6">
      <Link to="/crm" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="size-4" />
        Back to CRM
      </Link>

      <Card>
        <CardContent className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-xl font-semibold">
                {lead.firstName} {lead.lastName}
              </h1>
              <Badge variant="secondary">{lead.source}</Badge>
            </div>
            <p className="text-sm text-muted-foreground">{lead.email}</p>
            {lead.phone && <p className="text-sm text-muted-foreground">{lead.phone}</p>}
            {lead.convertedMemberId && (
              <Link to={`/members/${lead.convertedMemberId}`} className="mt-1 inline-block text-sm text-primary hover:underline">
                View converted member
              </Link>
            )}
            {lead.notes && <p className="mt-2 text-sm">{lead.notes}</p>}
          </div>
          <div className="w-40">
            <Select
              value={lead.stage}
              onValueChange={(value) => updateStage.mutate({ leadId: lead.id, stage: value as LeadStage })}
            >
              <SelectTrigger className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {PIPELINE_STAGES.map((s) => (
                  <SelectItem key={s} value={s}>
                    {STAGE_LABELS[s]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="text-sm font-medium">Activities</h2>
          <AddLeadActivityDialog leadId={lead.id} />
        </div>
        {sortedActivities.length === 0 && <p className="text-sm text-muted-foreground">No activities logged yet.</p>}
        {sortedActivities.map((a) => (
          <Card key={a.id}>
            <CardContent className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <div className="flex items-center gap-2">
                  <Badge variant="outline">{a.type}</Badge>
                  {a.dueDate && (
                    <span className="text-xs text-muted-foreground">Due {new Date(a.dueDate).toLocaleString()}</span>
                  )}
                </div>
                <p className="mt-1 text-sm">{a.notes}</p>
              </div>
              {a.completedAt ? (
                <Badge variant="default">Done {new Date(a.completedAt).toLocaleDateString()}</Badge>
              ) : (
                <CompleteActivityButton leadId={lead.id} activityId={a.id} />
              )}
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}
