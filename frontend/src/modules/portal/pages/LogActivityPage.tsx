import { useMemo, useState } from 'react'
import { Dumbbell, GlassWater, Loader2, Ruler, UtensilsCrossed } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { MemberEmptyState, MemberLoadError } from '@/modules/portal/components/portalShared'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { QuickLogWorkout } from '@/modules/portal/components/QuickLogWorkout'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import {
  useLogMyMeal,
  useLogMyMeasurement,
  useLogMyWater,
  useMyLoggingOptions,
  useMyNutritionSummary,
  useMyWorkoutLogs,
} from '@/modules/portal/api/portalApi'

const dateFmt = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' })

function RecentWorkouts() {
  const logs = useMyWorkoutLogs()
  const recent = (logs.data ?? []).slice(0, 8)

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base">Recent sessions</CardTitle>
      </CardHeader>
      <CardContent>
        {/*
          `(logs.data ?? [])` flattens a failed request into the same empty array as a new member, and
          this card sits directly under the logger — so the one place the mistake gets acted on. A
          member who has just logged a session, sees "Your first session goes here", and concludes the
          save didn't take will log it again, and the second copy is real. The query is read as a
          whole here rather than destructured, so the failure has something to say and a way back.
        */}
        {logs.isError ? (
          <MemberLoadError
            title="We couldn't load your recent sessions"
            hint="Anything you just logged was saved — this list is what we can't read back."
            onRetry={() => void logs.refetch()}
            isRetrying={logs.isFetching}
          />
        ) : recent.length === 0 ? (
          <MemberEmptyState icon={Dumbbell} title="Your first session goes here" hint="Log a workout above and it shows up straight away." />
        ) : (
          <div className="space-y-2">
            {recent.map((log) => (
              <div key={log.id} className="flex items-center justify-between border-b pb-2 text-sm last:border-0 last:pb-0">
                <span>{log.workoutTemplateName ?? log.character}</span>
                <span className="text-muted-foreground">{dateFmt.format(new Date(log.loggedAt))}</span>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  )
}

const WATER_PRESETS = [250, 500, 750]

function LogNutritionTab() {
  // Kept as the query object rather than destructured straight to `data`, because down in the meal
  // card a failed load and a member with no diet plan both arrive as `undefined` and have to be told
  // apart. `options` and `isLoading` stay as they were so the rest of the tab reads unchanged.
  const optionsQuery = useMyLoggingOptions()
  const options = optionsQuery.data
  const isLoading = optionsQuery.isLoading
  const { data: summary } = useMyNutritionSummary()
  const logWater = useLogMyWater()
  const logMeal = useLogMyMeal()

  const [foodItemId, setFoodItemId] = useState('')
  const [mealType, setMealType] = useState<LogMealType>('Lunch')
  const [quantity, setQuantity] = useState('1')

  const selectedFood = useMemo(
    () => options?.foods.find((f) => f.id === foodItemId),
    [options?.foods, foodItemId],
  )

  if (isLoading) return <Skeleton className="h-64 w-full" />

  const hasPlan = !!options?.activeDietPlanName

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="flex flex-row items-center gap-2">
          <GlassWater className="size-4 text-sky-500" />
          <CardTitle className="text-base">Water</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <p className="text-sm text-muted-foreground">
            {summary ? `${summary.waterMl.toLocaleString()} ml logged today` : 'Log your intake as you go.'}
          </p>
          <div className="flex flex-wrap gap-2">
            {WATER_PRESETS.map((ml) => (
              <Button
                key={ml}
                variant="outline"
                disabled={logWater.isPending}
                onClick={() =>
                  logWater.mutate(ml, {
                    onSuccess: () => toast.success(`+${ml} ml logged.`),
                    onError: () => toast.error("Couldn't log that."),
                  })
                }
              >
                +{ml} ml
              </Button>
            ))}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-center gap-2">
          <UtensilsCrossed className="size-4 text-emerald-500" />
          <CardTitle className="text-base">Meal</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {/*
            `hasPlan` is false when the member has no plan and false when the request for it failed,
            and the branch below reacts to both by blaming the trainer and closing meal logging down
            entirely — the member is told to go and ask for something they already have, and then
            given no way to log the meal they are sitting in front of. The error branch says what
            actually happened and offers the retry that gets the form back.
          */}
          {optionsQuery.isError ? (
            <MemberLoadError
              title="We couldn't load your meal options"
              hint="Your plan and today's meals are safe — we just can't reach them right now."
              onRetry={() => void optionsQuery.refetch()}
              isRetrying={optionsQuery.isFetching}
            />
          ) : !hasPlan ? (
            <p className="py-4 text-center text-sm text-muted-foreground">
              You don't have an active diet plan yet — ask your trainer to set one up and meal logging will
              open up here.
            </p>
          ) : (
            <>
              <Badge variant="secondary">{options!.activeDietPlanName}</Badge>
              <div className="grid gap-3 sm:grid-cols-3">
                <div className="space-y-1.5 sm:col-span-2">
                  <Label>Food</Label>
                  <Select value={foodItemId} onValueChange={setFoodItemId}>
                    <SelectTrigger className="w-full">
                      <SelectValue placeholder="Pick a food" />
                    </SelectTrigger>
                    <SelectContent>
                      {/*
                        A failed request no longer reaches this far — the card above intercepts it —
                        so an empty list here means the gym has genuinely entered no foods. Left to
                        itself the dropdown opened onto blank space, which reads as a broken control
                        rather than an empty catalogue.
                      */}
                      {options?.foods.length ? (
                        options.foods.map((f) => (
                          <SelectItem key={f.id} value={f.id}>
                            {f.name} · {f.caloriesPerServing} kcal / {f.servingSizeDescription}
                          </SelectItem>
                        ))
                      ) : (
                        <p className="px-2 py-3 text-center text-sm text-muted-foreground">
                          Your gym hasn't added any foods yet.
                        </p>
                      )}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1.5">
                  <Label>Servings</Label>
                  <Input type="number" min={0.25} step="0.25" value={quantity} onChange={(e) => setQuantity(e.target.value)} />
                </div>
              </div>

              <div className="space-y-1.5">
                <Label>Meal</Label>
                <Select value={mealType} onValueChange={(v) => setMealType(v as LogMealType)}>
                  <SelectTrigger className="w-full sm:w-56">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {(['Breakfast', 'Lunch', 'Dinner', 'Snack'] as const).map((m) => (
                      <SelectItem key={m} value={m}>
                        {m}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {selectedFood && (
                <p className="text-sm text-muted-foreground">
                  ≈{' '}
                  <span className="font-medium text-foreground tabular-nums">
                    {Math.round(selectedFood.caloriesPerServing * (Number(quantity) || 0))} kcal
                  </span>{' '}
                  · {Math.round(selectedFood.proteinG * (Number(quantity) || 0))}g protein
                </p>
              )}

              <Button
                disabled={!foodItemId || Number(quantity) <= 0 || logMeal.isPending}
                onClick={() =>
                  logMeal.mutate(
                    { foodItemId, mealType, quantity: Number(quantity) },
                    {
                      onSuccess: () => {
                        toast.success('Meal logged.')
                        setFoodItemId('')
                        setQuantity('1')
                      },
                      onError: () => toast.error("Couldn't log that meal."),
                    },
                  )
                }
              >
                {logMeal.isPending && <Loader2 className="size-4 animate-spin" />}
                Log meal
              </Button>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

type LogMealType = 'Breakfast' | 'Lunch' | 'Dinner' | 'Snack'

const MEASUREMENT_FIELDS = [
  { key: 'weightKg', label: 'Weight (kg)', step: '0.1' },
  { key: 'bodyFatPercentage', label: 'Body fat (%)', step: '0.1' },
  { key: 'chestCm', label: 'Chest (cm)', step: '0.5' },
  { key: 'waistCm', label: 'Waist (cm)', step: '0.5' },
  { key: 'hipCm', label: 'Hip (cm)', step: '0.5' },
  { key: 'armCm', label: 'Arm (cm)', step: '0.5' },
  { key: 'thighCm', label: 'Thigh (cm)', step: '0.5' },
] as const

function LogMeasurementsTab() {
  const logMeasurement = useLogMyMeasurement()
  const [values, setValues] = useState<Record<string, string>>({})
  const [notes, setNotes] = useState('')

  const hasAny = MEASUREMENT_FIELDS.some((f) => values[f.key] !== undefined && values[f.key] !== '')

  const submit = () => {
    const num = (k: string) => (values[k] === undefined || values[k] === '' ? null : Number(values[k]))
    logMeasurement.mutate(
      {
        weightKg: num('weightKg'),
        bodyFatPercentage: num('bodyFatPercentage'),
        chestCm: num('chestCm'),
        waistCm: num('waistCm'),
        hipCm: num('hipCm'),
        armCm: num('armCm'),
        thighCm: num('thighCm'),
        notes: notes.trim() || null,
      },
      {
        onSuccess: () => {
          toast.success('Measurements saved — your progress chart just updated.')
          setValues({})
          setNotes('')
        },
        onError: () => toast.error("Couldn't save those measurements."),
      },
    )
  }

  return (
    <Card>
      <CardHeader className="flex flex-row items-center gap-2">
        <Ruler className="size-4 text-violet-500" />
        <CardTitle className="text-base">Today's measurements</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {MEASUREMENT_FIELDS.map((f) => (
            <div key={f.key} className="space-y-1.5">
              <Label htmlFor={f.key}>{f.label}</Label>
              <Input
                id={f.key}
                type="number"
                step={f.step}
                min={0}
                value={values[f.key] ?? ''}
                onChange={(e) => setValues((v) => ({ ...v, [f.key]: e.target.value }))}
              />
            </div>
          ))}
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="measurement-notes">Notes (optional)</Label>
          <Textarea
            id="measurement-notes"
            placeholder="How you're feeling, what changed..."
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
          />
        </div>
        <Button disabled={!hasAny || logMeasurement.isPending} onClick={submit}>
          {logMeasurement.isPending && <Loader2 className="size-4 animate-spin" />}
          Save measurements
        </Button>
        <p className="text-xs text-muted-foreground">
          Fill in only what you measured — anything left blank is skipped, not stored as zero.
        </p>
      </CardContent>
    </Card>
  )
}

export default function LogActivityPage() {
  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Log Activity</h1>
        <p className="text-sm text-muted-foreground">
          Record your training, nutrition, and measurements — everything else updates from here.
        </p>
      </div>

      <Tabs defaultValue="workout">
        <TabsList className="h-auto flex-wrap">
          <TabsTrigger value="workout">Workout</TabsTrigger>
          <TabsTrigger value="nutrition">Nutrition</TabsTrigger>
          <TabsTrigger value="measurements">Measurements</TabsTrigger>
        </TabsList>

        <TabsContent value="workout">
          <div className="space-y-4">
            <QuickLogWorkout />
            <RecentWorkouts />
          </div>
        </TabsContent>
        <TabsContent value="nutrition"><LogNutritionTab /></TabsContent>
        <TabsContent value="measurements"><LogMeasurementsTab /></TabsContent>
      </Tabs>
    </div>
  )
}
