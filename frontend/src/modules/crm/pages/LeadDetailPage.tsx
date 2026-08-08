import { useParams, Link } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { ListEmpty, ListError, PageHeader } from '@/shared/components/console'
import {
  PIPELINE_STAGES,
  useLead,
  useUpdateLeadStage,
  type LeadActivityType,
  type LeadSource,
  type LeadStage,
} from '@/modules/crm/api/crmApi'
import { AddLeadActivityDialog } from '@/modules/crm/components/AddLeadActivityDialog'
import { CompleteActivityButton } from '@/modules/crm/components/CompleteActivityButton'

const STAGE_LABELS: Record<LeadStage, string> = {
  Lead: 'Lead',
  FollowUp: 'Follow-up',
  Trial: 'Trial',
  Member: 'Member',
  Lost: 'Lost',
}

/** Both of these are C# enum names, and two of them read as typos in a sentence. */
const SOURCE_LABELS: Record<LeadSource, string> = {
  WalkIn: 'Walk-in',
  Referral: 'Referral',
  SocialMedia: 'Social media',
  Website: 'Website',
  Advertisement: 'Advertisement',
  Other: 'Other',
}

const ACTIVITY_LABELS: Record<LeadActivityType, string> = {
  Call: 'Call',
  Email: 'Email',
  Meeting: 'Meeting',
  Note: 'Note',
  TrialScheduled: 'Trial scheduled',
}

// Matches LeadScorePolicy's 0-100 scale: >=70 is hot, <40 is cooling off.
function scoreBadgeVariant(score: number): 'success' | 'warning' | 'outline' {
  if (score >= 70) return 'success'
  if (score >= 40) return 'warning'
  return 'outline'
}

function BackToCrm() {
  return (
    <Link to="/crm" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
      <ArrowLeft className="size-4" />
      Back to CRM
    </Link>
  )
}

export default function LeadDetailPage() {
  const { id } = useParams<{ id: string }>()
  const leadQuery = useLead(id)
  const lead = leadQuery.data
  const updateStage = useUpdateLeadStage()

  // This screen used to sit on its skeleton forever when the request failed, because `isLoading ||
  // !lead` cannot tell "still fetching" from "failed for good".
  if (leadQuery.isError) {
    return (
      <div className="space-y-4">
        <BackToCrm />
        <ListError
          message="We couldn't load this lead"
          onRetry={() => leadQuery.refetch()}
          isRetrying={leadQuery.isFetching}
        />
      </div>
    )
  }

  if (leadQuery.isLoading || !lead) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-40 w-full rounded-2xl" />
      </div>
    )
  }

  const sortedActivities = [...lead.activities].sort(
    (a, b) => new Date(b.dueDate ?? 0).getTime() - new Date(a.dueDate ?? 0).getTime()
  )

  return (
    <div className="space-y-6">
      <BackToCrm />

      <PageHeader
        eyebrow="Lead"
        title={`${lead.firstName} ${lead.lastName}`}
        description={lead.email}
        actions={
          <Select
            value={lead.stage}
            onValueChange={(value) => updateStage.mutate({ leadId: lead.id, stage: value as LeadStage })}
          >
            <SelectTrigger className="w-44 rounded-xl">
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
        }
      />

      <div className="space-y-4 rounded-2xl border border-border bg-card p-5 shadow-sm">
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant="secondary">{SOURCE_LABELS[lead.source]}</Badge>
          <Badge variant={scoreBadgeVariant(lead.score)} className="tabular-nums">
            Score {lead.score}
          </Badge>
        </div>

        {/*
          No "last contacted" or "days in stage" line here, though the detail view invites one:
          GET /api/leads/{id} returns createdAt and the activity log, and nothing records when the
          stage last changed — a day count would have to be measured from the wrong event.
        */}
        <dl className="grid gap-4 sm:grid-cols-3">
          <div>
            <dt className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">Phone</dt>
            <dd className="mt-1 text-sm tabular-nums">{lead.phone ?? '—'}</dd>
          </div>
          <div>
            <dt className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">Added</dt>
            <dd className="mt-1 text-sm tabular-nums">{new Date(lead.createdAt).toLocaleDateString()}</dd>
          </div>
          <div>
            <dt className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">Stage</dt>
            <dd className="mt-1 text-sm">{STAGE_LABELS[lead.stage]}</dd>
          </div>
        </dl>

        {lead.notes && <p className="text-sm">{lead.notes}</p>}

        {lead.convertedMemberId && (
          <Link
            to={`/members/${lead.convertedMemberId}`}
            className="inline-block text-sm font-medium text-primary hover:underline"
          >
            View converted member
          </Link>
        )}
      </div>

      <section className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="font-display text-xl font-bold tracking-tight">Activities</h2>
          <AddLeadActivityDialog leadId={lead.id} />
        </div>

        {sortedActivities.length === 0 && (
          <ListEmpty message="No activities logged yet." hint="Calls, emails and meetings you log show up here." />
        )}

        {sortedActivities.map((a) => (
          <div
            key={a.id}
            className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-border bg-card p-4 shadow-sm"
          >
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <Badge variant="outline">{ACTIVITY_LABELS[a.type]}</Badge>
                <span className="text-xs text-muted-foreground tabular-nums">
                  Logged {new Date(a.createdAt).toLocaleDateString()}
                </span>
                {a.dueDate && (
                  <span className="text-xs text-muted-foreground tabular-nums">
                    · Due {new Date(a.dueDate).toLocaleString()}
                  </span>
                )}
              </div>
              <p className="mt-1.5 text-sm">{a.notes}</p>
            </div>
            {a.completedAt ? (
              <Badge variant="success" className="tabular-nums">
                Done {new Date(a.completedAt).toLocaleDateString()}
              </Badge>
            ) : (
              <CompleteActivityButton leadId={lead.id} activityId={a.id} />
            )}
          </div>
        ))}
      </section>
    </div>
  )
}
