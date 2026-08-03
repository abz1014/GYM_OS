import { TrendingUp } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
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
  useUpdateLeadStage,
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

export default function CrmPage() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const { data: summary } = useCrmPipelineSummary(branchId)
  const { data: leads, isLoading } = useLeadsList({ branchId })
  const updateStage = useUpdateLeadStage()

  const leadsByStage = (stage: LeadStage) => leads?.filter((l) => l.stage === stage) ?? []

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
                      <p className="text-sm font-medium">{lead.fullName}</p>
                      <p className="truncate text-xs text-muted-foreground">{lead.email}</p>
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
