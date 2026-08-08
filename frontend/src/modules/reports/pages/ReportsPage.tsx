import { useState } from 'react'
import { Download } from 'lucide-react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useAssetsList } from '@/modules/equipment/api/equipmentApi'
import { useInventoryItemsList } from '@/modules/inventory/api/inventoryApi'
import { useWorkOrdersList } from '@/modules/maintenance/api/maintenanceApi'
import {
  exportAtRiskMembersReport,
  exportAttendanceReport,
  exportCohortRetentionReport,
  exportCrmPipelineReport,
  exportEquipmentDowntimeReport,
  exportInventoryStockMovementReport,
  exportLtvBySourceReport,
  exportMembershipReport,
  exportNutritionReport,
  exportRevenueReport,
  exportTrainerCommissionReport,
  exportWorkoutActivityReport,
  useAtRiskMembersReport,
  useLoggingCaptureReport,
  useReturnRateReport,
  useAttendanceReport,
  useCohortRetentionReport,
  useCrmPipelineConversionReport,
  useEngagementSummary,
  useEquipmentDowntimeReport,
  useInventoryStockMovementReport,
  useLtvBySourceReport,
  useMembershipBreakdownReport,
  useNutritionReport,
  useRevenueReport,
  useTrainerCommissionReport,
  useWorkoutActivityReport,
} from '@/modules/reports/api/reportsApi'
import { SimpleBarChart } from '@/modules/reports/components/SimpleBarChart'
import { useTrainersList } from '@/modules/trainers/api/trainersApi'

function ExportButton({ onExport }: { onExport: () => Promise<void> }) {
  const [isExporting, setIsExporting] = useState(false)

  return (
    <Button
      variant="outline"
      size="sm"
      disabled={isExporting}
      onClick={async () => {
        setIsExporting(true)
        try {
          await onExport()
        } catch {
          toast.error('Failed to export report')
        } finally {
          setIsExporting(false)
        }
      }}
    >
      <Download />
      {isExporting ? 'Exporting...' : 'Export to Excel'}
    </Button>
  )
}

function ReportCard({
  title,
  action,
  children,
}: {
  title: string
  action?: React.ReactNode
  children: React.ReactNode
}) {
  return (
    <section className="rounded-panel border border-border bg-card p-5 edge-light-soft">
      <div className="flex flex-row items-center justify-between gap-3">
        <h2 className="font-display text-xl font-bold tracking-tight">{title}</h2>
        {action}
      </div>
      <div className="mt-4">{children}</div>
    </section>
  )
}

/**
 * One figure in a report's summary row. Takes the value already formatted, and takes it as a string
 * rather than a number so the caller has to have decided what to do about a missing one — see the
 * capture-rate and engagement rows, where a failed request used to render "0%".
 */
function ReportFigure({ value, label }: { value: string; label: string }) {
  return (
    <div>
      <p className="font-display text-3xl leading-none font-black tracking-tight tabular-nums">{value}</p>
      <p className="mt-1 text-muted-foreground">{label}</p>
    </div>
  )
}

function RevenueTab() {
  const { data, isLoading } = useRevenueReport(6)

  return (
    <ReportCard title="Revenue (last 6 months)" action={<ExportButton onExport={() => exportRevenueReport(6)} />}>
      {isLoading ? (
        <Skeleton className="h-48 w-full" />
      ) : (
        <SimpleBarChart
          data={(data ?? []).map((p) => ({ label: p.period, value: p.revenue }))}
          valueFormatter={(v) => `$${v.toLocaleString()}`}
        />
      )}
    </ReportCard>
  )
}

function AttendanceTab() {
  const { data, isLoading } = useAttendanceReport(30)

  return (
    <ReportCard title="Attendance (last 30 days)" action={<ExportButton onExport={() => exportAttendanceReport(30)} />}>
      {isLoading ? (
        <Skeleton className="h-48 w-full" />
      ) : (
        <SimpleBarChart
          data={(data ?? []).map((p) => ({ label: p.date.slice(5), value: p.checkIns }))}
        />
      )}
    </ReportCard>
  )
}

function MembershipTab() {
  const { data, isLoading } = useMembershipBreakdownReport()

  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
      <ReportCard title="Members by Status" action={<ExportButton onExport={exportMembershipReport} />}>
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <SimpleBarChart data={Object.entries(data?.byStatus ?? {}).map(([label, value]) => ({ label, value }))} />
        )}
      </ReportCard>
      <ReportCard title="Active Memberships by Plan Type">
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <SimpleBarChart data={Object.entries(data?.byPlanType ?? {}).map(([label, value]) => ({ label, value }))} />
        )}
      </ReportCard>
    </div>
  )
}

function DataTable<T>({
  columns,
  rows,
  keyFor,
}: {
  columns: { header: string; render: (row: T) => React.ReactNode }[]
  rows: T[]
  keyFor: (row: T) => string
}) {
  if (rows.length === 0) {
    return <p className="py-6 text-center text-sm text-muted-foreground">No data for this period.</p>
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-left text-muted-foreground">
            {columns.map((c) => (
              <th key={c.header} className="py-2 pr-4">{c.header}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={keyFor(row)} className="border-b last:border-0">
              {columns.map((c) => (
                <td key={c.header} className="py-2 pr-4">{c.render(row)}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function TrainersTab() {
  const { data, isLoading } = useTrainersList()
  const { data: commissions, isLoading: isLoadingCommissions } = useTrainerCommissionReport(6)

  return (
    <div className="space-y-4">
      <ReportCard title="Trainer Performance">
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left text-muted-foreground">
                  <th className="py-2 pr-4">Trainer</th>
                  <th className="py-2 pr-4">Active Clients</th>
                  <th className="py-2 pr-4">Avg Rating</th>
                  <th className="py-2 pr-4">Commission Rate</th>
                  <th className="py-2 pr-4">Status</th>
                </tr>
              </thead>
              <tbody>
                {data?.map((t) => (
                  <tr key={t.id} className="border-b last:border-0">
                    <td className="py-2 pr-4">{t.fullName}</td>
                    <td className="py-2 pr-4">{t.activeClientCount}</td>
                    <td className="py-2 pr-4">{t.averageRating?.toFixed(1) ?? '—'}</td>
                    <td className="py-2 pr-4">{t.commissionRate}%</td>
                    <td className="py-2 pr-4">
                      <Badge variant={t.isActive ? 'default' : 'outline'}>{t.isActive ? 'Active' : 'Inactive'}</Badge>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </ReportCard>

      <ReportCard
        title="Commissions (last 6 months)"
        action={<ExportButton onExport={() => exportTrainerCommissionReport(6)} />}
      >
        {isLoadingCommissions ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <DataTable
            rows={commissions ?? []}
            keyFor={(r) => r.trainerName}
            columns={[
              { header: 'Trainer', render: (r) => r.trainerName },
              { header: 'Pending', render: (r) => `$${r.totalPending.toLocaleString()}` },
              { header: 'Paid', render: (r) => `$${r.totalPaid.toLocaleString()}` },
              { header: 'Records', render: (r) => r.recordCount },
            ]}
          />
        )}
      </ReportCard>
    </div>
  )
}

function InventoryTab() {
  const { data: inventoryPage, isLoading } = useInventoryItemsList({ page: 1, pageSize: 100 })
  const data = inventoryPage?.items
  const lowStockCount = data?.filter((i) => i.isLowStock).length ?? 0
  const { data: movements, isLoading: isLoadingMovements } = useInventoryStockMovementReport(30)

  return (
    <div className="space-y-4">
      <ReportCard title={`Inventory Levels${data ? ` — ${lowStockCount} low stock` : ''}`}>
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <SimpleBarChart
            data={(data ?? []).map((i) => ({ label: i.name, value: i.quantityOnHand }))}
          />
        )}
      </ReportCard>

      <ReportCard
        title="Stock Movement (last 30 days)"
        action={<ExportButton onExport={() => exportInventoryStockMovementReport(30)} />}
      >
        {isLoadingMovements ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <DataTable
            rows={movements ?? []}
            keyFor={(r) => r.sku}
            columns={[
              { header: 'Item', render: (r) => r.itemName },
              { header: 'SKU', render: (r) => r.sku },
              { header: 'In', render: (r) => r.totalIn },
              { header: 'Out', render: (r) => r.totalOut },
              { header: 'Net', render: (r) => r.netChange },
              { header: 'On Hand', render: (r) => r.currentQuantityOnHand },
            ]}
          />
        )}
      </ReportCard>
    </div>
  )
}

function EquipmentTab() {
  const { data: assetsPage, isLoading } = useAssetsList({ page: 1, pageSize: 100 })
  const data = assetsPage?.items
  const counts = (data ?? []).reduce<Record<string, number>>((acc, a) => {
    acc[a.status] = (acc[a.status] ?? 0) + 1
    return acc
  }, {})
  const { data: downtime, isLoading: isLoadingDowntime } = useEquipmentDowntimeReport(6)

  return (
    <div className="space-y-4">
      <ReportCard title="Equipment Status Breakdown">
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <SimpleBarChart data={Object.entries(counts).map(([label, value]) => ({ label, value }))} />
        )}
      </ReportCard>

      <ReportCard
        title="Downtime & Maintenance Cost (last 6 months)"
        action={<ExportButton onExport={() => exportEquipmentDowntimeReport(6)} />}
      >
        {isLoadingDowntime ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <DataTable
            rows={downtime ?? []}
            keyFor={(r) => r.assetTag}
            columns={[
              { header: 'Asset', render: (r) => r.assetName },
              { header: 'Tag', render: (r) => r.assetTag },
              { header: 'Incidents', render: (r) => r.incidents },
              { header: 'Downtime (hrs)', render: (r) => r.totalDowntimeHours.toFixed(1) },
              { header: 'Maintenance Cost', render: (r) => `$${r.totalMaintenanceCost.toLocaleString()}` },
            ]}
          />
        )}
      </ReportCard>
    </div>
  )
}

function CrmTab() {
  const { data, isLoading } = useCrmPipelineConversionReport()

  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
      <ReportCard title="Pipeline by Stage" action={<ExportButton onExport={exportCrmPipelineReport} />}>
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <SimpleBarChart data={Object.entries(data?.byStage ?? {}).map(([label, value]) => ({ label, value }))} />
        )}
      </ReportCard>
      <ReportCard title="Conversion">
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          !data ? (
            <p className="text-sm text-muted-foreground">Conversion figures aren't available right now.</p>
          ) : (
            <div className="flex flex-col gap-2 text-sm">
              <p>Total leads: <span className="font-medium tabular-nums">{data.totalLeads.toLocaleString()}</span></p>
              <p>Converted to members: <span className="font-medium tabular-nums">{data.convertedCount.toLocaleString()}</span></p>
              <p>Conversion rate: <span className="font-medium tabular-nums">{data.conversionRatePercent}%</span></p>
            </div>
          )
        )}
      </ReportCard>
    </div>
  )
}

function MaintenanceTab() {
  const { data: workOrdersPage, isLoading } = useWorkOrdersList({ page: 1, pageSize: 100 })
  const data = workOrdersPage?.items
  const overdueCount = data?.filter((w) => w.isOverdue).length ?? 0
  const counts = (data ?? []).reduce<Record<string, number>>((acc, w) => {
    acc[w.status] = (acc[w.status] ?? 0) + 1
    return acc
  }, {})

  return (
    <ReportCard title={`Work Orders by Status${data ? ` — ${overdueCount} overdue` : ''}`}>
      {isLoading ? (
        <Skeleton className="h-48 w-full" />
      ) : (
        <SimpleBarChart data={Object.entries(counts).map(([label, value]) => ({ label, value }))} />
      )}
    </ReportCard>
  )
}

function WorkoutsTab() {
  const { data, isLoading } = useWorkoutActivityReport(30)

  return (
    <div className="space-y-4">
      <ReportCard title="Most Logged Exercises (last 30 days)">
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <SimpleBarChart data={(data ?? []).slice(0, 10).map((r) => ({ label: r.exerciseName, value: r.timesLogged }))} />
        )}
      </ReportCard>

      <ReportCard
        title="Workout Activity (last 30 days)"
        action={<ExportButton onExport={() => exportWorkoutActivityReport(30)} />}
      >
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <DataTable
            rows={data ?? []}
            keyFor={(r) => r.exerciseName}
            columns={[
              { header: 'Exercise', render: (r) => r.exerciseName },
              { header: 'Muscle Group', render: (r) => r.muscleGroup ?? '—' },
              { header: 'Times Logged', render: (r) => r.timesLogged },
              { header: 'Total Sets', render: (r) => r.totalSets },
              { header: 'Total Reps', render: (r) => r.totalReps },
              { header: 'Avg Weight (kg)', render: (r) => r.avgWeightKg?.toFixed(1) ?? '—' },
            ]}
          />
        )}
      </ReportCard>
    </div>
  )
}

function NutritionTab() {
  const { data, isLoading } = useNutritionReport(30)

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <ReportCard title="Most Logged Food Items (last 30 days)">
          {isLoading ? (
            <Skeleton className="h-48 w-full" />
          ) : (
            <SimpleBarChart
              data={(data?.topFoodItems ?? []).slice(0, 10).map((r) => ({ label: r.foodItemName, value: r.timesLogged }))}
            />
          )}
        </ReportCard>
        <ReportCard title="Logging Summary">
          {isLoading ? (
            <Skeleton className="h-48 w-full" />
          ) : (
            <div className="flex flex-col gap-2 text-sm">
              <p>Meal entries logged: <span className="font-medium tabular-nums">{(data?.totalMealEntriesLogged ?? 0).toLocaleString()}</span></p>
              <p>Total calories logged: <span className="font-medium tabular-nums">{(data?.totalCaloriesLogged ?? 0).toLocaleString()}</span></p>
              <p>Water logs: <span className="font-medium tabular-nums">{(data?.totalWaterLogsLogged ?? 0).toLocaleString()}</span></p>
              <p>Total water logged: <span className="font-medium tabular-nums">{((data?.totalWaterMlLogged ?? 0) / 1000).toFixed(1)} L</span></p>
            </div>
          )}
        </ReportCard>
      </div>

      <ReportCard title="Food Item Breakdown" action={<ExportButton onExport={() => exportNutritionReport(30)} />}>
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <DataTable
            rows={data?.topFoodItems ?? []}
            keyFor={(r) => r.foodItemName}
            columns={[
              { header: 'Food Item', render: (r) => r.foodItemName },
              { header: 'Times Logged', render: (r) => r.timesLogged },
              { header: 'Total Calories', render: (r) => r.totalCaloriesLogged.toLocaleString() },
            ]}
          />
        )}
      </ReportCard>
    </div>
  )
}

function AnalyticsTab() {
  const { data: atRisk, isLoading: isLoadingAtRisk } = useAtRiskMembersReport()
  const { data: cohorts, isLoading: isLoadingCohorts } = useCohortRetentionReport(12)
  const { data: ltv, isLoading: isLoadingLtv } = useLtvBySourceReport()

  return (
    <div className="space-y-4">
      <ReportCard
        title={`At-Risk Members${atRisk ? ` — ${atRisk.length} quiet ${atRisk.length === 1 ? 'member' : 'members'}` : ''}`}
        action={<ExportButton onExport={exportAtRiskMembersReport} />}
      >
        {isLoadingAtRisk ? (
          <Skeleton className="h-48 w-full" />
        ) : (atRisk ?? []).length === 0 ? (
          <p className="py-6 text-center text-sm text-muted-foreground">No active members have gone quiet right now.</p>
        ) : (
          <DataTable
            rows={atRisk ?? []}
            keyFor={(r) => r.memberId}
            columns={[
              {
                header: 'Member',
                render: (r) => (
                  <Link to={`/members/${r.memberId}`} className="hover:underline">
                    {r.fullName}
                  </Link>
                ),
              },
              { header: 'Code', render: (r) => r.memberCode },
              { header: 'Last Check-in', render: (r) => r.lastCheckInDate },
              {
                header: 'Days Quiet',
                render: (r) => <Badge variant={r.daysSinceLastVisit >= 30 ? 'destructive' : 'secondary'}>{r.daysSinceLastVisit}</Badge>,
              },
            ]}
          />
        )}
      </ReportCard>

      <ReportCard
        title="Cohort Retention (last 12 months)"
        action={<ExportButton onExport={() => exportCohortRetentionReport(12)} />}
      >
        {isLoadingCohorts ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <SimpleBarChart
            data={(cohorts ?? []).map((c) => ({ label: c.cohortMonth, value: c.retentionRatePercent }))}
            valueFormatter={(v) => `${v}%`}
          />
        )}
      </ReportCard>

      <ReportCard title="Lifetime Value by Acquisition Source" action={<ExportButton onExport={exportLtvBySourceReport} />}>
        {isLoadingLtv ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <DataTable
            rows={ltv ?? []}
            keyFor={(r) => r.source}
            columns={[
              { header: 'Source', render: (r) => r.source },
              { header: 'Members', render: (r) => r.memberCount },
              { header: 'Total Revenue', render: (r) => `$${r.totalRevenue.toLocaleString()}` },
              { header: 'Avg LTV / Member', render: (r) => `$${r.averageLtv.toLocaleString()}` },
            ]}
          />
        )}
      </ReportCard>
    </div>
  )
}

const LATENCY_LABELS: Record<string, string> = {
  WithinTheHour: 'Before leaving',
  SameDay: 'Later that day',
  NextDay: 'The next day',
  Later: 'Two days or more',
}

/**
 * The four numbers every roadmap gate is judged on, in one place: capture rate, time-to-log,
 * week-N return and sessions per member per week.
 *
 * They are shown together deliberately. Capture rate alone is gameable — make confirmation
 * frictionless enough and it climbs while training falls — so the volume figure beside it is the
 * control, and a rising rate next to a falling volume is the signal the ratio has stopped meaning
 * what it says.
 */
function GateMetricsCard() {
  const capture = useLoggingCaptureReport(12)
  const returns = useReturnRateReport()

  return (
    <ReportCard title="Gate metrics">
      {capture.isLoading || returns.isLoading ? (
        <Skeleton className="h-40 w-full rounded-2xl" />
      ) : (
        <div className="space-y-5">
          <div className="grid grid-cols-2 gap-4 text-sm sm:grid-cols-4">
            <ReportFigure
              value={capture.data ? `${capture.data.captureRatePercent}%` : '—'}
              label="Capture rate"
            />
            {/* Null median renders an em dash rather than "0 min", which would claim instant logging
                on a gym where nothing could be timed at all. */}
            <ReportFigure
              value={
                capture.data?.medianMinutesToLog !== null && capture.data?.medianMinutesToLog !== undefined
                  ? `${capture.data.medianMinutesToLog} min`
                  : '—'
              }
              label="Median time to log"
            />
            <ReportFigure
              value={
                capture.data?.sessionsPerMemberPerWeek !== null && capture.data?.sessionsPerMemberPerWeek !== undefined
                  ? capture.data.sessionsPerMemberPerWeek.toFixed(2)
                  : '—'
              }
              label="Sessions / member / week"
            />
            <ReportFigure
              value={
                returns.data?.weeks.find((w) => w.weekNumber === 4)?.eligibleMembers
                  ? `${returns.data.weeks.find((w) => w.weekNumber === 4)!.ratePercent}%`
                  : '—'
              }
              label="Week 4 return"
            />
          </div>

          {/*
            Time-to-log measures arrival to record, not the tap itself. The roadmap's "under five
            seconds" is an interaction latency and nothing in this system observes one — there is no
            app-open event and no client telemetry — so the honest version is the lag from the door.
          */}
          {capture.data && (
            <div>
              <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                When sessions get recorded
              </p>
              <div className="mt-2 flex flex-wrap gap-2">
                {capture.data.latencyBuckets.map((b) => (
                  <span
                    key={b.bucket}
                    className="rounded-xl border border-border bg-background px-3 py-1.5 text-sm tabular-nums"
                  >
                    {LATENCY_LABELS[b.bucket] ?? b.bucket}: <span className="font-medium">{b.sessions.toLocaleString()}</span>
                  </span>
                ))}
              </div>
            </div>
          )}

          <div>
            <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
              Return rate after joining
            </p>
            {!returns.data ? (
              <p className="mt-2 text-sm text-muted-foreground">Return figures aren't available right now.</p>
            ) : (
              <div className="mt-2 flex flex-wrap gap-2">
                {returns.data.weeks.map((w) => (
                  <span
                    key={w.weekNumber}
                    className="rounded-xl border border-border bg-background px-3 py-1.5 text-sm tabular-nums"
                  >
                    Week {w.weekNumber}:{' '}
                    {/*
                      The count is never dropped. A percentage with no denominator cannot distinguish
                      "nobody came back" from "nobody has been a member long enough to have an answer
                      yet", and those two mean opposite things about the business.
                    */}
                    {w.eligibleMembers === 0 ? (
                      <span className="text-muted-foreground">no cohort yet</span>
                    ) : (
                      <>
                        <span className="font-medium">{w.ratePercent}%</span>{' '}
                        <span className="text-muted-foreground">
                          ({w.returnedMembers} of {w.eligibleMembers})
                        </span>
                      </>
                    )}
                  </span>
                ))}
              </div>
            )}
          </div>
        </div>
      )}
    </ReportCard>
  )
}

function LoggingCaptureCard() {
  const { data, isLoading } = useLoggingCaptureReport(12)

  return (
    <ReportCard title="Workout capture rate (last 12 weeks)">
      {isLoading ? (
        <Skeleton className="h-48 w-full" />
      ) : (
        <div className="space-y-4">
          {/* No `?? 0` here on purpose. A request that failed used to render "0% of visits were
              logged", which is not a neutral placeholder — it is a specific, alarming, false claim
              about the gym. If the figures are missing the row says so instead. */}
          {!data ? (
            <p className="text-sm text-muted-foreground">Capture figures aren't available right now.</p>
          ) : (
            <div className="grid grid-cols-2 gap-4 text-sm sm:grid-cols-4">
              <ReportFigure value={`${data.captureRatePercent}%`} label="Visits that were logged" />
              <ReportFigure value={data.totalVisitDays.toLocaleString()} label="Gym visits" />
              <ReportFigure value={data.totalLoggedVisitDays.toLocaleString()} label="Recorded sessions" />
              <ReportFigure value={data.membersVisitingWithoutLogging.toLocaleString()} label="Members who never log" />
            </div>
          )}

          {/* The rate is gameable — workouts logged on days with no visit are the tell. Say so on the
              report rather than letting a climbing number be read as progress. */}
          {data && !data.isReliable && (
            <p className="rounded-xl border border-warning/40 bg-warning/10 p-3 text-sm">
              {data.totalOrphanLogDays.toLocaleString()} sessions were logged on days with no recorded
              visit, so this rate may not reflect what happens in the gym.
            </p>
          )}

          <SimpleBarChart
            data={(data?.weekly ?? []).map((w) => ({ label: w.weekStart.slice(5), value: w.captureRatePercent }))}
            valueFormatter={(v) => `${v}%`}
          />

          <p className="text-sm text-muted-foreground">
            Members can only be shown progress from sessions that were recorded. Every point below
            100% is training this gym did that the app never saw.
          </p>
        </div>
      )}
    </ReportCard>
  )
}

function EngagementTab() {
  const { data, isLoading } = useEngagementSummary()

  return (
    <div className="space-y-4">
      <GateMetricsCard />
      <LoggingCaptureCard />
      <ReportCard title="Engagement Overview">
        {isLoading ? (
          <Skeleton className="h-24 w-full" />
        ) : (
          !data ? (
            <p className="text-sm text-muted-foreground">Engagement figures aren't available right now.</p>
          ) : (
            <div className="grid grid-cols-2 gap-4 text-sm sm:grid-cols-4">
              <ReportFigure value={data.totalActiveMembers.toLocaleString()} label="Active members" />
              <ReportFigure value={data.xpEarnedLast30Days.toLocaleString()} label="XP earned (30d)" />
              <ReportFigure value={data.membersWithActiveStreak.toLocaleString()} label="Members mid-streak" />
              <ReportFigure
                value={`${data.challengeCompletions.toLocaleString()} / ${data.challengeParticipants.toLocaleString()}`}
                label="Challenges completed / joined"
              />
            </div>
          )
        )}
      </ReportCard>

      <ReportCard title="Level Distribution">
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (data?.levelDistribution ?? []).length === 0 ? (
          <p className="py-6 text-center text-sm text-muted-foreground">No members have earned XP yet.</p>
        ) : (
          <SimpleBarChart data={(data?.levelDistribution ?? []).map((r) => ({ label: `Lvl ${r.level}`, value: r.memberCount }))} />
        )}
      </ReportCard>

      <ReportCard title="Retention Correlation">
        {isLoading ? (
          <Skeleton className="h-24 w-full" />
        ) : (
          <div className="flex flex-col gap-2 text-sm">
            <p>
              At-risk members (<span className="tabular-nums">{data?.retention.atRiskMemberCount ?? 0}</span>): average level{' '}
              <span className="font-medium tabular-nums">{(data?.retention.atRiskAverageLevel ?? 0).toFixed(1)}</span>
            </p>
            <p>
              Active members (<span className="tabular-nums">{data?.retention.activeMemberCount ?? 0}</span>): average level{' '}
              <span className="font-medium tabular-nums">{(data?.retention.activeAverageLevel ?? 0).toFixed(1)}</span>
            </p>
            <p className="text-muted-foreground">
              {data && data.retention.atRiskMemberCount > 0 && data.retention.activeAverageLevel > data.retention.atRiskAverageLevel
                ? 'At-risk members are engaging with the experience system less than active ones — game-layer engagement tracks with retention.'
                : 'Not enough at-risk members yet to draw a correlation.'}
            </p>
          </div>
        )}
      </ReportCard>
    </div>
  )
}

export default function ReportsPage() {
  return (
    <div className="space-y-4">
      <div>
        <h1 className="font-display text-3xl leading-tight font-black tracking-tight">Reports</h1>
        <p className="text-sm text-muted-foreground">Operational insights across the gym, with Excel export.</p>
      </div>

      <Tabs defaultValue="revenue">
        <TabsList className="h-auto flex-wrap">
          <TabsTrigger value="revenue">Revenue</TabsTrigger>
          <TabsTrigger value="attendance">Attendance</TabsTrigger>
          <TabsTrigger value="membership">Membership</TabsTrigger>
          <TabsTrigger value="trainers">Trainers</TabsTrigger>
          <TabsTrigger value="inventory">Inventory</TabsTrigger>
          <TabsTrigger value="equipment">Equipment</TabsTrigger>
          <TabsTrigger value="maintenance">Maintenance</TabsTrigger>
          <TabsTrigger value="crm">CRM</TabsTrigger>
          <TabsTrigger value="workouts">Workouts</TabsTrigger>
          <TabsTrigger value="nutrition">Nutrition</TabsTrigger>
          <TabsTrigger value="analytics">Analytics</TabsTrigger>
          <TabsTrigger value="engagement">Engagement</TabsTrigger>
        </TabsList>

        <TabsContent value="revenue"><RevenueTab /></TabsContent>
        <TabsContent value="attendance"><AttendanceTab /></TabsContent>
        <TabsContent value="membership"><MembershipTab /></TabsContent>
        <TabsContent value="trainers"><TrainersTab /></TabsContent>
        <TabsContent value="inventory"><InventoryTab /></TabsContent>
        <TabsContent value="equipment"><EquipmentTab /></TabsContent>
        <TabsContent value="maintenance"><MaintenanceTab /></TabsContent>
        <TabsContent value="crm"><CrmTab /></TabsContent>
        <TabsContent value="workouts"><WorkoutsTab /></TabsContent>
        <TabsContent value="nutrition"><NutritionTab /></TabsContent>
        <TabsContent value="analytics"><AnalyticsTab /></TabsContent>
        <TabsContent value="engagement"><EngagementTab /></TabsContent>
      </Tabs>
    </div>
  )
}
