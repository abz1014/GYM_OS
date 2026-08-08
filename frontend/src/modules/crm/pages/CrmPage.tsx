import { Link } from 'react-router-dom'

import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { ListEmpty, ListError, PageHeader, StatTile } from '@/shared/components/console'
import { useUiStore } from '@/stores/uiStore'
import {
  PIPELINE_STAGES,
  useCrmPipelineSummary,
  useLeadsList,
  useTopReferrers,
  useUpdateLeadStage,
  type LeadListItem,
  type LeadSource,
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

/** LeadSource is a C# enum; nobody at a front desk reads "WalkIn" or "SocialMedia". */
const SOURCE_LABELS: Record<LeadSource, string> = {
  WalkIn: 'Walk-in',
  Referral: 'Referral',
  SocialMedia: 'Social media',
  Website: 'Website',
  Advertisement: 'Advertisement',
  Other: 'Other',
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
    <Badge variant={scoreBadgeVariant(score)} className="shrink-0 text-[10px] tabular-nums">
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
  const leadsQuery = useLeadsList({ branchId, page: 1, pageSize: 200 })
  const leads = leadsQuery.data?.items
  const updateStage = useUpdateLeadStage()

  const leadsByStage = (stage: LeadStage) => leads?.filter((l) => l.stage === stage) ?? []

  // The board already buckets stages client-side, so the hottest-leads list is just a client-side
  // sort of the same data rather than a separate endpoint — mirrors the Top Referrers card below.
  const hotLeads: LeadListItem[] = [...(leads ?? [])]
    .filter((l) => l.stage !== 'Member' && l.stage !== 'Lost')
    .sort((a, b) => b.score - a.score)
    .slice(0, 5)

  return (
    <div className="space-y-6">
      <PageHeader
        title="Leads & CRM"
        description="Lead → Follow-up → Trial → Member pipeline."
        actions={<CreateLeadDialog />}
      />

      {/*
        The tiles render only once /api/leads/summary has answered, and it only runs with a branch
        selected — an unscoped CRM has no honest pipeline figure to show, so it shows none rather
        than four zeros. There are no trend deltas either: the summary is a single snapshot with no
        previous period anywhere in the endpoint.
      */}
      {summary && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {/* The three pre-conversion stages, summed from the endpoint's own per-stage totals rather
              than from the board's loaded page, so it stays right no matter what the board holds. */}
          <StatTile
            label="Open leads"
            value={(summary.leadCount + summary.followUpCount + summary.trialCount).toLocaleString()}
            caption="Lead, follow-up and trial"
          />
          <StatTile label="Conversion rate" value={`${summary.conversionRatePercent}%`} />
          <StatTile label="Converted" value={summary.memberCount.toLocaleString()} />
          <StatTile label="Lost" value={summary.lostCount.toLocaleString()} />
        </div>
      )}

      <section className="space-y-3">
        <h2 className="font-display text-xl font-bold tracking-tight">Pipeline</h2>

        {leadsQuery.isError && (
          <ListError
            message="We couldn't load the lead pipeline"
            onRetry={() => leadsQuery.refetch()}
            isRetrying={leadsQuery.isFetching}
          />
        )}

        {leadsQuery.isLoading && (
          <div className="grid grid-cols-1 gap-4 md:grid-cols-5">
            {Array.from({ length: 5 }).map((_, i) => (
              <Skeleton key={i} className="h-64 w-full rounded-2xl" />
            ))}
          </div>
        )}

        {!leadsQuery.isLoading && leads?.length === 0 && (
          <ListEmpty message="No leads in this branch yet." hint="Add one and the board appears here." />
        )}

        {!leadsQuery.isLoading && leads && leads.length > 0 && (
          <div className="grid grid-cols-1 gap-4 md:grid-cols-5">
            {PIPELINE_STAGES.map((stage) => {
              const stageLeads = leadsByStage(stage)
              return (
                <div key={stage} className="space-y-2">
                  <div className="flex items-center justify-between px-1">
                    <h3 className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                      {STAGE_LABELS[stage]}
                    </h3>
                    {/* Counts the cards in the column, not a server total — it is the length of what
                        you are looking at, so it can never disagree with the column beneath it. */}
                    <span className="font-display text-sm font-black tabular-nums">{stageLeads.length}</span>
                  </div>
                  <div className="space-y-2">
                    {stageLeads.map((lead) => (
                      <div key={lead.id} className="space-y-2 rounded-2xl border border-border bg-card p-3 shadow-sm">
                        <Link to={`/crm/${lead.id}`} className="block hover:underline">
                          <div className="flex items-center justify-between gap-2">
                            <p className="truncate font-medium">{lead.fullName}</p>
                            <ScoreBadge score={lead.score} />
                          </div>
                          <p className="truncate text-xs text-muted-foreground">{lead.email}</p>
                        </Link>
                        <Badge variant="secondary" className="text-[10px]">
                          {SOURCE_LABELS[lead.source]}
                        </Badge>
                        <Select
                          value={lead.stage}
                          onValueChange={(value) => updateStage.mutate({ leadId: lead.id, stage: value as LeadStage })}
                        >
                          <SelectTrigger size="sm" className="w-full rounded-xl">
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
                    ))}
                    {stageLeads.length === 0 && (
                      <p className="rounded-2xl border border-dashed border-border p-3 text-center text-xs text-muted-foreground">
                        Empty
                      </p>
                    )}
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </section>

      <div className="grid gap-4 lg:grid-cols-2">
        {hotLeads.length > 0 && (
          <section className="rounded-2xl border border-border bg-card p-5 shadow-sm">
            <h2 className="font-display text-xl font-bold tracking-tight">Hot leads</h2>
            <p className="mt-1 text-sm text-muted-foreground">Highest scoring, still in play.</p>
            <ul className="mt-4 divide-y divide-border">
              {hotLeads.map((lead) => (
                <li key={lead.id}>
                  <Link
                    to={`/crm/${lead.id}`}
                    className="flex items-center justify-between gap-3 py-2.5 hover:underline"
                  >
                    <span className="flex min-w-0 items-center gap-2">
                      <span className="truncate text-sm font-medium">{lead.fullName}</span>
                      <Badge variant="outline" className="shrink-0 text-[10px]">
                        {STAGE_LABELS[lead.stage]}
                      </Badge>
                    </span>
                    <ScoreBadge score={lead.score} />
                  </Link>
                </li>
              ))}
            </ul>
          </section>
        )}

        {topReferrers && topReferrers.length > 0 && (
          <section className="rounded-2xl border border-border bg-card p-5 shadow-sm">
            <h2 className="font-display text-xl font-bold tracking-tight">Top referrers</h2>
            <p className="mt-1 text-sm text-muted-foreground">Members who send people through the door.</p>
            <ul className="mt-4 divide-y divide-border">
              {topReferrers.map((r, i) => (
                <li key={r.memberId} className="flex items-center justify-between gap-3 py-2.5">
                  <span className="flex min-w-0 items-center gap-3">
                    <span className="w-5 shrink-0 font-display text-sm font-black text-muted-foreground tabular-nums">
                      {i + 1}
                    </span>
                    <Link to={`/members/${r.memberId}`} className="truncate text-sm font-medium hover:underline">
                      {r.fullName}
                    </Link>
                    <span className="shrink-0 text-xs text-muted-foreground tabular-nums">{r.memberCode}</span>
                  </span>
                  <Badge variant="secondary" className="shrink-0 tabular-nums">
                    {r.referralCount} {r.referralCount === 1 ? 'referral' : 'referrals'}
                  </Badge>
                </li>
              ))}
            </ul>
          </section>
        )}
      </div>
    </div>
  )
}
