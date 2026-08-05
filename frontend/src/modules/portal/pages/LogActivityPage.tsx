import { useMemo, useState } from 'react'
import { Dumbbell, GlassWater, Loader2, Plus, Ruler, Trash2, UtensilsCrossed } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import {
  useLogMyMeal,
  useLogMyMeasurement,
  useLogMyWater,
  useLogMyWorkout,
  useMyLoggingOptions,
  useMyNutritionSummary,
  useMyWorkoutLogs,
  type WorkoutEntryInput,
} from '@/modules/portal/api/portalApi'

const dateFmt = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' })

interface DraftSet {
  key: number
  exerciseId: string
  sets: string
  reps: string
  weight: string
}

let draftKey = 0
const newDraft = (): DraftSet => ({ key: draftKey++, exerciseId: '', sets: '3', reps: '10', weight: '' })

function LogWorkoutTab() {
  const { data: options, isLoading } = useMyLoggingOptions()
  const logWorkout = useLogMyWorkout()
  const [drafts, setDrafts] = useState<DraftSet[]>([newDraft()])

  const update = (key: number, patch: Partial<DraftSet>) =>
    setDrafts((d) => d.map((x) => (x.key === key ? { ...x, ...patch } : x)))

  const valid = drafts.filter((d) => d.exerciseId && Number(d.sets) > 0 && Number(d.reps) > 0)
  const canSubmit = valid.length > 0 && !logWorkout.isPending

  const estimatedVolume = valid.reduce(
    (sum, d) => sum + Number(d.sets) * Number(d.reps) * (Number(d.weight) || 0),
    0,
  )

  const submit = () => {
    const entries: WorkoutEntryInput[] = valid.map((d) => ({
      exerciseId: d.exerciseId,
      setsCompleted: Number(d.sets),
      repsCompleted: Number(d.reps),
      weightKg: d.weight === '' ? null : Number(d.weight),
    }))

    logWorkout.mutate(entries, {
      onSuccess: () => {
        toast.success(`Workout logged — ${entries.length} exercise${entries.length === 1 ? '' : 's'}. Nice work!`)
        setDrafts([newDraft()])
      },
      onError: () => toast.error("Couldn't log that workout."),
    })
  }

  if (isLoading) return <Skeleton className="h-64 w-full" />

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="flex flex-row items-center gap-2">
          <Dumbbell className="size-4 text-primary" />
          <CardTitle className="text-base">Log today's session</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {drafts.map((d) => (
            <div key={d.key} className="grid gap-3 rounded-lg border p-3 sm:grid-cols-[minmax(0,2fr)_repeat(3,minmax(0,1fr))_auto] sm:items-end">
              <div className="space-y-1.5">
                <Label>Exercise</Label>
                <Select value={d.exerciseId} onValueChange={(v) => update(d.key, { exerciseId: v })}>
                  <SelectTrigger className="w-full">
                    <SelectValue placeholder="Pick an exercise" />
                  </SelectTrigger>
                  <SelectContent>
                    {options?.exercises.map((e) => (
                      <SelectItem key={e.id} value={e.id}>
                        {e.name}
                        {e.muscleGroup ? ` · ${e.muscleGroup}` : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label>Sets</Label>
                <Input type="number" min={1} max={50} value={d.sets} onChange={(e) => update(d.key, { sets: e.target.value })} />
              </div>
              <div className="space-y-1.5">
                <Label>Reps</Label>
                <Input type="number" min={1} max={500} value={d.reps} onChange={(e) => update(d.key, { reps: e.target.value })} />
              </div>
              <div className="space-y-1.5">
                <Label>Weight (kg)</Label>
                <Input
                  type="number"
                  min={0}
                  max={1000}
                  step="0.5"
                  placeholder="bodyweight"
                  value={d.weight}
                  onChange={(e) => update(d.key, { weight: e.target.value })}
                />
              </div>
              <Button
                variant="ghost"
                size="icon"
                aria-label="Remove exercise"
                disabled={drafts.length === 1}
                onClick={() => setDrafts((all) => all.filter((x) => x.key !== d.key))}
              >
                <Trash2 className="size-4" />
              </Button>
            </div>
          ))}

          <div className="flex flex-wrap items-center justify-between gap-3">
            <Button variant="outline" size="sm" onClick={() => setDrafts((d) => [...d, newDraft()])}>
              <Plus className="size-4" />
              Add exercise
            </Button>
            {estimatedVolume > 0 && (
              <span className="text-sm text-muted-foreground">
                Session volume: <span className="font-medium tabular-nums">{estimatedVolume.toLocaleString()} kg</span>
              </span>
            )}
          </div>

          <Button disabled={!canSubmit} onClick={submit} className="w-full sm:w-auto">
            {logWorkout.isPending && <Loader2 className="size-4 animate-spin" />}
            Log workout
          </Button>
          <p className="text-xs text-muted-foreground">
            Logging earns XP, updates your personal records and mastery, extends your streak, and counts
            toward any challenge you've joined.
          </p>
        </CardContent>
      </Card>

      <RecentWorkouts />
    </div>
  )
}

function RecentWorkouts() {
  const { data: logs } = useMyWorkoutLogs()
  const recent = (logs ?? []).slice(0, 8)

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base">Recent sessions</CardTitle>
      </CardHeader>
      <CardContent>
        {recent.length === 0 ? (
          <p className="py-6 text-center text-sm text-muted-foreground">Nothing logged yet — your first session goes here.</p>
        ) : (
          <div className="space-y-2">
            {recent.map((log) => (
              <div key={log.id} className="flex items-center justify-between border-b pb-2 text-sm last:border-0 last:pb-0">
                <span>{log.workoutTemplateName ?? 'Custom workout'}</span>
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
  const { data: options, isLoading } = useMyLoggingOptions()
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
          {!hasPlan ? (
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
                      {options?.foods.map((f) => (
                        <SelectItem key={f.id} value={f.id}>
                          {f.name} · {f.caloriesPerServing} kcal / {f.servingSizeDescription}
                        </SelectItem>
                      ))}
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

        <TabsContent value="workout"><LogWorkoutTab /></TabsContent>
        <TabsContent value="nutrition"><LogNutritionTab /></TabsContent>
        <TabsContent value="measurements"><LogMeasurementsTab /></TabsContent>
      </Tabs>
    </div>
  )
}
