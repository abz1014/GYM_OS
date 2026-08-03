import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useExercisesList, useWorkoutTemplatesList } from '@/modules/workouts/api/workoutsApi'
import { CreateExerciseDialog } from '@/modules/workouts/components/CreateExerciseDialog'
import { CreateWorkoutTemplateDialog } from '@/modules/workouts/components/CreateWorkoutTemplateDialog'

export default function WorkoutsPage() {
  const { data: exercises, isLoading: exercisesLoading } = useExercisesList()
  const { data: templates, isLoading: templatesLoading } = useWorkoutTemplatesList()

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Workouts</h1>
        <p className="text-sm text-muted-foreground">Exercise library and workout templates.</p>
      </div>

      <Tabs defaultValue="exercises">
        <TabsList>
          <TabsTrigger value="exercises">Exercise Library</TabsTrigger>
          <TabsTrigger value="templates">Workout Templates</TabsTrigger>
        </TabsList>

        <TabsContent value="exercises" className="space-y-3">
          <div className="flex justify-end">
            <CreateExerciseDialog />
          </div>
          {exercisesLoading ? (
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {Array.from({ length: 6 }).map((_, i) => (
                <Skeleton key={i} className="h-20 w-full" />
              ))}
            </div>
          ) : (
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {exercises?.map((ex) => (
                <Card key={ex.id}>
                  <CardContent className="space-y-1 p-3">
                    <p className="font-medium">{ex.name}</p>
                    <div className="flex flex-wrap gap-1">
                      {ex.muscleGroup && <Badge variant="outline">{ex.muscleGroup}</Badge>}
                      {ex.equipment && <Badge variant="secondary">{ex.equipment}</Badge>}
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </TabsContent>

        <TabsContent value="templates" className="space-y-3">
          <div className="flex justify-end">
            <CreateWorkoutTemplateDialog />
          </div>
          {templatesLoading ? (
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {Array.from({ length: 4 }).map((_, i) => (
                <Skeleton key={i} className="h-24 w-full" />
              ))}
            </div>
          ) : templates?.length === 0 ? (
            <p className="text-sm text-muted-foreground">No workout templates yet.</p>
          ) : (
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {templates?.map((t) => (
                <Card key={t.id}>
                  <CardContent className="space-y-1 p-3">
                    <p className="font-medium">{t.name}</p>
                    {t.description && <p className="text-sm text-muted-foreground">{t.description}</p>}
                    <Badge variant="outline">{t.exerciseCount} exercises</Badge>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </TabsContent>
      </Tabs>
    </div>
  )
}
