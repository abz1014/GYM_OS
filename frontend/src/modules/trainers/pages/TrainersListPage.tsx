import { useNavigate, Link } from 'react-router-dom'
import { Star } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { ListEmpty, ListError, PageHeader } from '@/shared/components/console'
import { useTrainersList } from '@/modules/trainers/api/trainersApi'
import { CreateTrainerDialog } from '@/modules/trainers/components/CreateTrainerDialog'
import { useAuthStore } from '@/stores/authStore'
import { useUiStore } from '@/stores/uiStore'
import { useCoachingCompliance, useCoachingPlateaus, useCoachingRisks } from '@/modules/coaching/api/coachingApi'
import { isStale } from '@/shared/lib/queryTrust'

/** Two letters at most: "Dana Okonkwo" → DO, "Cher" → C. */
function initials(fullName: string): string {
  return fullName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part.charAt(0).toUpperCase())
    .join('')
}

/** The count chip the dashboard's queue panel uses, for panels whose length is the headline. */
function CountChip({ count }: { count: number }) {
  return (
    <span className="flex min-w-6 items-center justify-center rounded-lg bg-foreground px-2 py-0.5 font-display text-xs font-bold text-background tabular-nums">
      {count}
    </span>
  )
}

function PanelHeading({ title, count }: { title: string; count?: number }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <h2 className="font-display text-xl font-bold tracking-tight">{title}</h2>
      {count !== undefined && count > 0 && <CountChip count={count} />}
    </div>
  )
}

function TrainersTab() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const trainersQuery = useTrainersList(branchId)
  const trainers = trainersQuery.data
  const navigate = useNavigate()

  // POST /api/trainers is trainers.manage; the roster itself is trainers.view. A Trainer can see
  // the squad they're part of and cannot add to it.
  const canManage = hasPermission('trainers.manage')

  return (
    <div className="space-y-4">
      {/*
        No stat row over the roster. GET /api/trainers returns a flat list of trainers, and the two
        figures a manager would want above it are not in it: total active clients would be a sum of
        activeClientCount, which double-counts anyone working with two trainers, and a squad-wide
        rating would be an unweighted mean of per-trainer means — a different number from the one
        anybody computes. The per-trainer figures below are exact; a rolled-up one would not be.
      */}
      <div className="flex flex-wrap items-center gap-3">
        {/* The count waits for the roster instead of showing a dash: an em-dash where a number goes
            reads as "none", and this list is never empty for long. */}
        {trainers && (
          <p className="text-sm text-muted-foreground tabular-nums">
            {trainers.length} {trainers.length === 1 ? 'trainer' : 'trainers'}
          </p>
        )}
        {canManage && (
          <div className="ml-auto">
            <CreateTrainerDialog />
          </div>
        )}
      </div>

      {isStale(trainersQuery) && (
        <ListError
          message="We couldn't load the trainer roster"
          onRetry={() => trainersQuery.refetch()}
          isRetrying={trainersQuery.isFetching}
        />
      )}

      {trainersQuery.isLoading && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-40 w-full rounded-2xl" />
          ))}
        </div>
      )}

      {trainers?.length === 0 && (
        <ListEmpty
          message="No trainers on this branch yet."
          hint={canManage ? 'Add one to start assigning clients.' : undefined}
        />
      )}

      {trainers && trainers.length > 0 && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {trainers.map((trainer) => (
            <button
              key={trainer.id}
              type="button"
              onClick={() => navigate(`/trainers/${trainer.id}`)}
              className="press flex w-full flex-col gap-3 rounded-panel border border-border bg-card p-5 text-left edge-light-soft transition-colors hover:bg-accent active:bg-accent"
            >
              <span className="flex items-start gap-3">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-secondary font-display text-sm font-black">
                  {initials(trainer.fullName)}
                </span>
                <span className="min-w-0 flex-1">
                  <span className="flex items-center gap-2">
                    <span className="truncate font-medium">{trainer.fullName}</span>
                    {!trainer.isActive && <Badge variant="secondary">Inactive</Badge>}
                  </span>
                  <span className="block truncate text-sm text-muted-foreground">{trainer.specialties}</span>
                </span>
              </span>

              <span className="mt-auto flex items-center justify-between gap-2 text-sm">
                <span className="text-muted-foreground tabular-nums">
                  {trainer.activeClientCount} active {trainer.activeClientCount === 1 ? 'client' : 'clients'}
                </span>
                {/* An unrated trainer shows no star rather than a 0.0 — and the null check has to be
                    explicit, because `averageRating && …` would print a literal 0 for a real 0.0. */}
                {trainer.averageRating !== null && (
                  <span className="flex items-center gap-1 text-warning tabular-nums">
                    <Star className="size-3.5 fill-current" /> {trainer.averageRating.toFixed(1)}
                  </span>
                )}
              </span>

              <span>
                <Badge variant="outline" className="tabular-nums">
                  {trainer.commissionRate}% commission
                </Badge>
              </span>
            </button>
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
      <span className="text-xs text-muted-foreground tabular-nums">{memberCode}</span>
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
    <section className="rounded-panel border border-border bg-card p-5 edge-light-soft">
      <PanelHeading title="Plateaus" count={data?.length} />
      <div className="mt-4">
        {isLoading ? (
          <Skeleton className="h-32 w-full rounded-2xl" />
        ) : data && data.length > 0 ? (
          <ul className="space-y-3">
            {data.map((row) => (
              <li
                key={`${row.memberId}-${row.exerciseId}`}
                className="flex items-center justify-between gap-3 border-b border-border pb-3 last:border-0 last:pb-0"
              >
                <div className="min-w-0">
                  <MemberLink memberId={row.memberId} memberName={row.memberName} memberCode={row.memberCode} />
                  <p className="text-sm text-muted-foreground tabular-nums">
                    {row.exerciseName} — held {row.lastWeightKg ?? '—'}kg for two sessions
                  </p>
                </div>
                {row.suggestedNextWeightKg && (
                  <Badge variant="outline" className="shrink-0 tabular-nums">
                    Try {row.suggestedNextWeightKg}kg
                  </Badge>
                )}
              </li>
            ))}
          </ul>
        ) : (
          <p className="py-6 text-center text-sm text-muted-foreground">No one's plateaued right now.</p>
        )}
      </div>
    </section>
  )
}

function ComplianceCard() {
  const { data, isLoading } = useCoachingCompliance()

  return (
    <section className="rounded-panel border border-border bg-card p-5 edge-light-soft">
      {/*
        No count beside this heading, unlike the two panels either side of it. Their length is the
        finding — how many people have plateaued, how many are at risk. This one's length is just how
        many members /api/coaching/compliance reported on, which is not a number anybody acts on.
      */}
      <PanelHeading title="Compliance" />
      <div className="mt-4">
        {isLoading ? (
          <Skeleton className="h-32 w-full rounded-2xl" />
        ) : data && data.length > 0 ? (
          // Three columns, two of them badges, so this keeps a scroll container rather than the
          // console's usual card-list-under-md: the cards would be taller and say no more.
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border text-left">
                  <th className="py-2 pr-4 text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                    Member
                  </th>
                  <th className="py-2 pr-4 text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                    Workouts
                  </th>
                  <th className="py-2 pr-4 text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                    Nutrition
                  </th>
                </tr>
              </thead>
              <tbody>
                {data.map((row) => (
                  <tr key={row.memberId} className="border-b border-border last:border-0">
                    <td className="py-2.5 pr-4">
                      <MemberLink memberId={row.memberId} memberName={row.memberName} memberCode={row.memberCode} />
                    </td>
                    <td className="py-2.5 pr-4">
                      <Badge variant={adherenceBadgeVariant(row.workoutAdherencePercent)} className="tabular-nums">
                        {row.workoutAdherencePercent}%
                      </Badge>
                    </td>
                    <td className="py-2.5 pr-4">
                      {/* "No plan" rather than 0% — a member with no nutrition plan has not failed
                          to follow one, and the endpoint says so by sending null. */}
                      {row.nutritionAdherencePercent === null ? (
                        <Badge variant="outline">No plan</Badge>
                      ) : (
                        <Badge variant={adherenceBadgeVariant(row.nutritionAdherencePercent)} className="tabular-nums">
                          {row.nutritionAdherencePercent}%
                        </Badge>
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
      </div>
    </section>
  )
}

function RisksCard() {
  const { data, isLoading } = useCoachingRisks()

  return (
    <section className="rounded-panel border border-border bg-card p-5 edge-light-soft">
      <PanelHeading title="Risks" count={data?.length} />
      <div className="mt-4">
        {isLoading ? (
          <Skeleton className="h-32 w-full rounded-2xl" />
        ) : data && data.length > 0 ? (
          <ul className="space-y-3">
            {data.map((row, i) => (
              <li
                key={`${row.memberId}-${row.riskType}-${i}`}
                className="flex items-start justify-between gap-3 border-b border-border pb-3 last:border-0 last:pb-0"
              >
                <div className="min-w-0">
                  <MemberLink memberId={row.memberId} memberName={row.memberName} memberCode={row.memberCode} />
                  <p className="text-sm text-muted-foreground">{row.reason}</p>
                </div>
                <Badge variant={row.riskType === 'OvertrainingRisk' ? 'destructive' : 'warning'} className="shrink-0">
                  {row.riskType === 'OvertrainingRisk' ? 'Overtraining' : 'Streak at risk'}
                </Badge>
              </li>
            ))}
          </ul>
        ) : (
          <p className="py-6 text-center text-sm text-muted-foreground">Nobody needs a check-in right now.</p>
        )}
      </div>
    </section>
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
    <div className="space-y-6">
      <PageHeader title="Trainers" description="Roster, and the coaching dashboard for who needs attention." />

      {/* These stay Radix tabs rather than the console's FilterTabs: FilterTabs narrows one list that
          stays mounted, and these swap two unrelated panels — they only borrow the new radii. */}
      <Tabs defaultValue="trainers" className="gap-4">
        <TabsList className="rounded-xl">
          <TabsTrigger value="trainers" className="rounded-lg">
            Trainers
          </TabsTrigger>
          <TabsTrigger value="coaching" className="rounded-lg">
            Coaching
          </TabsTrigger>
        </TabsList>

        <TabsContent value="trainers">
          <TrainersTab />
        </TabsContent>
        <TabsContent value="coaching">
          <CoachingTab />
        </TabsContent>
      </Tabs>
    </div>
  )
}
