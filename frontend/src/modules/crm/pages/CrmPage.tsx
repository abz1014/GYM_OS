import { Flame, TrendingUp, Users } from 'lucide-react'
import { Link } from 'react-router-dom'

import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { useUiStore } from '@/stores/uiStore'
import {
  PIPELINE_STAGES,
  useCrmPipelineSummary,
  useLeadsList,
  useTopReferrers,
  useUpdateLeadStage,
  type LeadListItem,
  type LeadStage,
} from '@/modules/crm/api/crmApi'
import { CreateLeadDialog } from '@/modules/crm/components/CreateLeadDialog'

const STAGE_LABELS: Record<LeadStage, string> = {
  Lead: 'Lead',
  FollowUp: 'Follow-up',
  Trial: 'Trial',
  Member: 'Member',
  Lost: 'Lost',
}

// >=70 is genuinely hot (deep into the pipeline, engaged recently); <40 is cooling off and worth a
// nudge; the band between is a normal working lead. Matches LeadScorePolicy's 0-100 scale.
function scoreBadgeVariant(score: number): 'success' | 'warning' | 'outline' {
  if (score >= 70) return 'success'
  if (score >= 40) return 'warning'
  return 'outline'
}

function ScoreBadge({ score }: { score: number }) {
  return (
    <Badge variant={scoreBadgeVariant(score)} className="text-[10px]">
      {score}
    </Badge>
  )
}

export default function CrmPage() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const { data: summary } = useCrmPipelineSummary(branchId)
  const { data: topReferrers } = useTopReferrers(branchId)
  // The Kanban board buckets every lead into a stage column client-side, so it needs the full
  // set in one page rather than a paginated slice — 200 (the API's max page size) comfortably
  // covers this app's demo data volumes.
  const { data, isLoading } = useLeadsList({ branchId, page: 1, pageSize: 200 })
  const leads = data?.items
  const updateStage = useUpdateLeadStage()

  const leadsByStage = (stage: LeadStage) => leads?.filter((l) => l.stage === stage) ?? []

  // The board already buckets stages client-side, so the hottest-leads list is just a client-side
  // sort of the same data rather than a separate endpoint — mirrors the Top Referrers card above.
  const hotLeads: LeadListItem[] = [...(leads ?? [])]
    .filter((l) => l.stage !== 'Member' && l.stage !== 'Lost')
    .sort((a, b) => b.score - a.score)
    .slice(0, 5)

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">CRM & Leads</h1>
          <p className="text-sm text-muted-foreground">Lead → Follow-up → Trial → Member pipeline.</p>
        </div>
        <CreateLeadDialog />
      </div>

      {summary && (
        <Card>
          <CardContent className="flex flex-wrap items-center gap-6">
            <div className="flex items-center gap-2">
              <TrendingUp className="size-5 text-success" />
              <div>
                <p className="text-2xl font-semibold">{summary.conversionRatePercent}%</p>
                <p className="text-xs text-muted-foreground">Conversion rate</p>
              </div>
            </div>
            <div className="flex gap-4 text-sm text-muted-foreground">
              <span>{summary.leadCount} Leads</span>
              <span>{summary.followUpCount} Follow-up</span>
              <span>{summary.trialCount} Trial</span>
              <span className="text-success">{summary.memberCount} Converted</span>
              <span className="text-destructive">{summary.lostCount} Lost</span>
            </div>
          </CardContent>
        </Card>
      )}

      {hotLeads.length > 0 && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-base">
              <Flame className="size-4 text-orange-500" />
              Hot leads
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="divide-y">
              {hotLeads.map((lead) => (
                <Link
                  key={lead.id}
                  to={`/crm/${lead.id}`}
                  className="flex items-center justify-between gap-3 py-2 hover:underline"
                >
                  <div className="flex min-w-0 items-center gap-2">
                    <span className="truncate text-sm font-medium">{lead.fullName}</span>
                    <Badge variant="outline" className="shrink-0 text-[10px]">
                      {STAGE_LABELS[lead.stage]}
                    </Badge>
                  </div>
                  <ScoreBadge score={lead.score} />
                </Link>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {topReferrers && topReferrers.length > 0 && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-base">
              <Users className="size-4" />
              Top referrers
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="divide-y">
              {topReferrers.map((r, i) => (
                <div key={r.memberId} className="flex items-center justify-between gap-3 py-2">
                  <div className="flex min-w-0 items-center gap-3">
                    <span className="w-5 shrink-0 text-sm font-semibold text-muted-foreground">#{i + 1}</span>
                    <Link to={`/members/${r.memberId}`} className="truncate text-sm font-medium hover:underline">
                      {r.fullName}
                    </Link>
                    <span className="shrink-0 text-xs text-muted-foreground">{r.memberCode}</span>
                  </div>
                  <Badge variant="secondary" className="shrink-0">
                    {r.referralCount} {r.referralCount === 1 ? 'referral' : 'referrals'}
                  </Badge>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {isLoading ? (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-5">
          {Array.from({ length: 5 }).map((_, i) => (
            <Skeleton key={i} className="h-64 w-full" />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-5">
          {PIPELINE_STAGES.map((stage) => (
            <div key={stage} className="space-y-2">
              <div className="flex items-center justify-between px-1">
                <h2 className="text-sm font-semibold">{STAGE_LABELS[stage]}</h2>
                <Badge variant="outline">{leadsByStage(stage).length}</Badge>
              </div>
              <div className="space-y-2">
                {leadsByStage(stage).map((lead) => (
                  <Card key={lead.id}>
                    <CardContent className="space-y-2 p-3">
                      <Link to={`/crm/${lead.id}`} className="block hover:underline">
                        <div className="flex items-center justify-between gap-2">
                          <p className="truncate text-sm font-medium">{lead.fullName}</p>
                          <ScoreBadge score={lead.score} />
                        </div>
                        <p className="truncate text-xs text-muted-foreground">{lead.email}</p>
                      </Link>
                      <Badge variant="secondary" className="text-[10px]">
                        {lead.source}
                      </Badge>
                      <Select
                        value={lead.stage}
                        onValueChange={(value) => updateStage.mutate({ leadId: lead.id, stage: value as LeadStage })}
                      >
                        <SelectTrigger size="sm" className="w-full">
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
                    </CardContent>
                  </Card>
                ))}
                {leadsByStage(stage).length === 0 && (
                  <p className="rounded-md border border-dashed p-3 text-center text-xs text-muted-foreground">Empty</p>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
