import { useNavigate, Link } from 'react-router-dom'
import { AlertTriangle, Flame, Star, TrendingUp } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useTrainersList } from '@/modules/trainers/api/trainersApi'
import { CreateTrainerDialog } from '@/modules/trainers/components/CreateTrainerDialog'
import { useUiStore } from '@/stores/uiStore'
import { useCoachingCompliance, useCoachingPlateaus, useCoachingRisks } from '@/modules/coaching/api/coachingApi'

function TrainersTab() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const { data: trainers, isLoading } = useTrainersList(branchId)
  const navigate = useNavigate()

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-muted-foreground">{trainers?.length ?? '—'} trainers</p>
        <CreateTrainerDialog />
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-36 w-full" />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {trainers?.map((trainer) => (
            <Card key={trainer.id} className="cursor-pointer" onClick={() => navigate(`/trainers/${trainer.id}`)}>
              <CardContent className="space-y-2">
                <div className="flex items-center justify-between">
                  <p className="font-medium">{trainer.fullName}</p>
                  {!trainer.isActive && <Badge variant="secondary">Inactive</Badge>}
                </div>
                <p className="text-sm text-muted-foreground">{trainer.specialties}</p>
                <div className="flex items-center justify-between text-sm">
                  <span className="text-muted-foreground">{trainer.activeClientCount} active clients</span>
                  {trainer.averageRating && (
                    <span className="flex items-center gap-1 text-warning">
                      <Star className="size-3.5 fill-current" /> {trainer.averageRating.toFixed(1)}
                    </span>
                  )}
                </div>
                <Badge variant="outline">{trainer.commissionRate}% commission</Badge>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}

function MemberLink({ memberId, memberName, memberCode }: { memberId: string; memberName: string; memberCode: string }) {
  return (
    <Link to={`/members/${memberId}`} className="hover:underline">
      <span className="font-medium">{memberName}</span>{' '}
      <span className="text-xs text-muted-foreground">{memberCode}</span>
    </Link>
  )
}

function adherenceBadgeVariant(percent: number): 'destructive' | 'warning' | 'success' {
  if (percent < 50) return 'destructive'
  if (percent < 75) return 'warning'
  return 'success'
}

function PlateausCard() {
  const { data, isLoading } = useCoachingPlateaus()

  return (
    <Card>
      <CardHeader className="flex flex-row items-center gap-2">
        <TrendingUp className="size-4 text-primary" />
        <CardTitle className="text-base">Plateaus{data ? ` — ${data.length}` : ''}</CardTitle>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <Skeleton className="h-32 w-full" />
        ) : data && data.length > 0 ? (
          <div className="space-y-3">
            {data.map((row) => (
              <div key={`${row.memberId}-${row.exerciseId}`} className="flex items-center justify-between gap-3 border-b pb-2 last:border-0 last:pb-0">
                <div>
                  <MemberLink memberId={row.memberId} memberName={row.memberName} memberCode={row.memberCode} />
                  <p className="text-sm text-muted-foreground">
                    {row.exerciseName} — held {row.lastWeightKg ?? '—'}kg for two sessions
                  </p>
                </div>
                {row.suggestedNextWeightKg && (
                  <Badge variant="outline" className="shrink-0">Try {row.suggestedNextWeightKg}kg</Badge>
                )}
              </div>
            ))}
          </div>
        ) : (
          <p className="py-6 text-center text-sm text-muted-foreground">No one's plateaued right now.</p>
        )}
      </CardContent>
    </Card>
  )
}

function ComplianceCard() {
  const { data, isLoading } = useCoachingCompliance()

  return (
    <Card>
      <CardHeader className="flex flex-row items-center gap-2">
        <Flame className="size-4 text-primary" />
        <CardTitle className="text-base">Compliance</CardTitle>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <Skeleton className="h-32 w-full" />
        ) : data && data.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left text-muted-foreground">
                  <th className="py-2 pr-4">Member</th>
                  <th className="py-2 pr-4">Workouts</th>
                  <th className="py-2 pr-4">Nutrition</th>
                </tr>
              </thead>
              <tbody>
                {data.map((row) => (
                  <tr key={row.memberId} className="border-b last:border-0">
                    <td className="py-2 pr-4">
                      <MemberLink memberId={row.memberId} memberName={row.memberName} memberCode={row.memberCode} />
                    </td>
                    <td className="py-2 pr-4">
                      <Badge variant={adherenceBadgeVariant(row.workoutAdherencePercent)}>{row.workoutAdherencePercent}%</Badge>
                    </td>
                    <td className="py-2 pr-4">
                      {row.nutritionAdherencePercent === null ? (
                        <Badge variant="outline">No plan</Badge>
                      ) : (
                        <Badge variant={adherenceBadgeVariant(row.nutritionAdherencePercent)}>{row.nutritionAdherencePercent}%</Badge>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="py-6 text-center text-sm text-muted-foreground">No active members to report on.</p>
        )}
      </CardContent>
    </Card>
  )
}

function RisksCard() {
  const { data, isLoading } = useCoachingRisks()

  return (
    <Card>
      <CardHeader className="flex flex-row items-center gap-2">
        <AlertTriangle className="size-4 text-destructive" />
        <CardTitle className="text-base">Risks{data ? ` — ${data.length}` : ''}</CardTitle>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <Skeleton className="h-32 w-full" />
        ) : data && data.length > 0 ? (
          <div className="space-y-3">
            {data.map((row, i) => (
              <div key={`${row.memberId}-${row.riskType}-${i}`} className="flex items-start justify-between gap-3 border-b pb-2 last:border-0 last:pb-0">
                <div>
                  <MemberLink memberId={row.memberId} memberName={row.memberName} memberCode={row.memberCode} />
                  <p className="text-sm text-muted-foreground">{row.reason}</p>
                </div>
                <Badge variant={row.riskType === 'OvertrainingRisk' ? 'destructive' : 'warning'} className="shrink-0">
                  {row.riskType === 'OvertrainingRisk' ? 'Overtraining' : 'Streak at risk'}
                </Badge>
              </div>
            ))}
          </div>
        ) : (
          <p className="py-6 text-center text-sm text-muted-foreground">Nobody needs a check-in right now.</p>
        )}
      </CardContent>
    </Card>
  )
}

function CoachingTab() {
  return (
    <div className="space-y-4">
      <PlateausCard />
      <ComplianceCard />
      <RisksCard />
    </div>
  )
}

export default function TrainersListPage() {
  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Trainers</h1>
        <p className="text-sm text-muted-foreground">Roster, and the coaching dashboard for who needs attention.</p>
      </div>

      <Tabs defaultValue="trainers">
        <TabsList>
          <TabsTrigger value="trainers">Trainers</TabsTrigger>
          <TabsTrigger value="coaching">Coaching</TabsTrigger>
        </TabsList>

        <TabsContent value="trainers"><TrainersTab /></TabsContent>
        <TabsContent value="coaching"><CoachingTab /></TabsContent>
      </Tabs>
    </div>
  )
}
