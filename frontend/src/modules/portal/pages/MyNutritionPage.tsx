import { Link } from 'react-router-dom'
import { Apple, Droplets, NotebookPen } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { MacroBar, MemberEmptyState, MemberLoadError, SectionCard, dateFormat, dateTimeFormat } from '@/modules/portal/components/portalShared'
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
          {/*
            `summary.data?.activeDietPlanName` is undefined for a member with no plan and undefined
            for a member whose request failed, and the else branch used to tell both of them to ask
            their trainer for one. The three queries on this page fail independently, so the error
            state is per card: a member whose macros didn't arrive can still read their plans below.
          */}
          {summary.isLoading ? (
            <Skeleton className="h-32 w-full" />
          ) : summary.isError ? (
            <MemberLoadError
              title="We couldn't load today's nutrition"
              hint="Anything you've logged today is still recorded — we just can't read it back right now."
              onRetry={() => void summary.refetch()}
              isRetrying={summary.isFetching}
            />
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
            <MemberEmptyState
              icon={Apple}
              title="No meal plan running"
              hint="Ask your trainer for one, or just log what you eat — that counts too."
              action={{ label: 'Log a meal', to: '/log-activity' }}
            />
          )}
        </CardContent>
      </Card>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <SectionCard title="Diet Plans">
          {/*
            "Any plan your trainer builds for you will appear here" is a promise about a trainer who
            may well have already built one — on a dropped request this card reported the absence of
            work that exists, in the trainer's name.
          */}
          {dietPlans.isLoading ? (
            <Skeleton className="h-40 w-full" />
          ) : dietPlans.isError ? (
            <MemberLoadError
              title="We couldn't load your plans"
              hint="Any plan you've been given is still there — we just can't reach it right now."
              onRetry={() => void dietPlans.refetch()}
              isRetrying={dietPlans.isFetching}
            />
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
            <MemberEmptyState
              icon={Apple}
              title="No plans yet"
              hint="Any plan your trainer builds for you will appear here."
            />
          )}
        </SectionCard>

        <SectionCard title="Water Intake">
          {/*
            This one gets acted on. "No water logged today" with a Log water button under it is an
            instruction, and a member who has already drunk and logged four glasses will follow it
            and log them again — the failure writes itself into the data. The empty state keeps its
            action; the error state deliberately doesn't offer one.
          */}
          {waterLogs.isLoading ? (
            <Skeleton className="h-40 w-full" />
          ) : waterLogs.isError ? (
            <MemberLoadError
              title="We couldn't load your water log"
              hint="You may already have logged today — check back in a moment rather than logging it twice."
              onRetry={() => void waterLogs.refetch()}
              isRetrying={waterLogs.isFetching}
            />
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
            <MemberEmptyState
              icon={Droplets}
              title="No water logged today"
              hint="One tap per glass — it adds up faster than you'd think."
              action={{ label: 'Log water', to: '/log-activity' }}
            />
          )}
        </SectionCard>
      </div>
    </div>
  )
}
