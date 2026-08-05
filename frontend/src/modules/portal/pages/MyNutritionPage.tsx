import { Link } from 'react-router-dom'
import { Apple, Droplets, NotebookPen } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { MacroBar, SectionCard, dateFormat, dateTimeFormat } from '@/modules/portal/components/portalShared'
import { useMyDietPlans, useMyNutritionSummary, useMyWaterLogs } from '@/modules/portal/api/portalApi'

/** Everything food and hydration, split out of the old all-in-one portal page. */
export default function MyNutritionPage() {
  const summary = useMyNutritionSummary()
  const dietPlans = useMyDietPlans()
  const waterLogs = useMyWaterLogs()

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">My Nutrition</h1>
          <p className="text-sm text-muted-foreground">Today's macros, your plans, and hydration.</p>
        </div>
        <Button asChild size="sm">
          <Link to="/log-activity">
            <NotebookPen className="size-4" />
            Log a meal
          </Link>
        </Button>
      </div>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="flex items-center gap-2 text-base">
            <Apple className="size-4 text-emerald-500" />
            Today's Nutrition
          </CardTitle>
        </CardHeader>
        <CardContent>
          {summary.isLoading ? (
            <Skeleton className="h-32 w-full" />
          ) : summary.data?.activeDietPlanName ? (
            <div className="space-y-2">
              <p className="text-sm text-muted-foreground">{summary.data.activeDietPlanName}</p>
              <MacroBar label="Calories" consumed={summary.data.consumedCalories} target={summary.data.targetCalories} unit="kcal" />
              <MacroBar label="Protein" consumed={summary.data.consumedProteinG} target={summary.data.targetProteinG} unit="g" />
              <MacroBar label="Carbs" consumed={summary.data.consumedCarbsG} target={summary.data.targetCarbsG} unit="g" />
              <MacroBar label="Fat" consumed={summary.data.consumedFatG} target={summary.data.targetFatG} unit="g" />
              <div className="flex items-center gap-1.5 pt-1 text-xs text-muted-foreground">
                <Droplets className="size-3.5" />
                {summary.data.waterMl} ml water today
              </div>
            </div>
          ) : (
            <div className="flex flex-col items-center gap-2 py-6 text-center text-sm text-muted-foreground">
              <Apple className="size-6" />
              No active diet plan right now.
            </div>
          )}
        </CardContent>
      </Card>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <SectionCard title="Diet Plans">
          {dietPlans.isLoading ? (
            <Skeleton className="h-40 w-full" />
          ) : dietPlans.data && dietPlans.data.length > 0 ? (
            <ul className="space-y-2 text-sm">
              {dietPlans.data.map((p) => (
                <li key={p.id} className="flex items-center justify-between gap-2 border-b pb-2 last:border-0">
                  <div className="min-w-0">
                    <p className="truncate font-medium">{p.name}</p>
                    <p className="text-xs text-muted-foreground">
                      from {dateFormat.format(new Date(p.startDate))}
                    </p>
                  </div>
                  {p.targetCalories && (
                    <span className="shrink-0 text-muted-foreground">{p.targetCalories} kcal/day</span>
                  )}
                </li>
              ))}
            </ul>
          ) : (
            <div className="flex flex-col items-center gap-2 py-6 text-center text-sm text-muted-foreground">
              <Apple className="size-6" />
              No diet plan yet.
            </div>
          )}
        </SectionCard>

        <SectionCard title="Water Intake">
          {waterLogs.isLoading ? (
            <Skeleton className="h-40 w-full" />
          ) : waterLogs.data && waterLogs.data.length > 0 ? (
            <ul className="space-y-2 text-sm">
              {waterLogs.data.slice(0, 10).map((w) => (
                <li key={w.id} className="flex items-center justify-between gap-2 border-b pb-2 last:border-0">
                  <span className="font-medium">{w.amountMl} ml</span>
                  <span className="text-muted-foreground">{dateTimeFormat.format(new Date(w.loggedAt))}</span>
                </li>
              ))}
            </ul>
          ) : (
            <div className="flex flex-col items-center gap-2 py-6 text-center text-sm text-muted-foreground">
              <Droplets className="size-6" />
              No water logged yet.
            </div>
          )}
        </SectionCard>
      </div>
    </div>
  )
}
