import { useState } from 'react'

import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useMembersList } from '@/modules/members/api/membersApi'
import {
  useExercisesList,
  useMemberWorkoutAssignments,
  useMemberWorkoutLogs,
  useWorkoutTemplatesList,
} from '@/modules/workouts/api/workoutsApi'
import { AssignWorkoutTemplateDialog } from '@/modules/workouts/components/AssignWorkoutTemplateDialog'
import { CreateExerciseDialog } from '@/modules/workouts/components/CreateExerciseDialog'
import { CreateWorkoutTemplateDialog } from '@/modules/workouts/components/CreateWorkoutTemplateDialog'
import { LogWorkoutDialog } from '@/modules/workouts/components/LogWorkoutDialog'

function MemberAssignedPlans({ memberId }: { memberId: string }) {
  const { data: assignments, isLoading } = useMemberWorkoutAssignments(memberId)

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <p className="text-sm font-medium">Assigned plans</p>
        <AssignWorkoutTemplateDialog memberId={memberId} />
      </div>
      {isLoading && <Skeleton className="h-20 w-full" />}
      {assignments?.length === 0 && <p className="text-sm text-muted-foreground">No workout plan assigned yet.</p>}
      {assignments?.map((a) => (
        <Card key={a.id}>
          <CardContent className="space-y-2 p-3">
            <div className="flex items-center justify-between text-sm">
              <span className="font-medium">{a.workoutTemplateName}</span>
              <span className="text-muted-foreground">
                {a.startDate}
                {a.endDate ? ` → ${a.endDate}` : ' → ongoing'}
              </span>
            </div>
            {a.notes && <p className="text-sm text-muted-foreground">{a.notes}</p>}
            <div className="flex flex-wrap gap-1">
              {a.exercises.map((e) => (
                <Badge key={e.id} variant="outline">
                  {e.exerciseName}: {e.setsCount}×{e.repsCount}
                </Badge>
              ))}
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  )
}

function MemberWorkoutLogs() {
  const [searchTerm, setSearchTerm] = useState('')
  const [memberId, setMemberId] = useState('')
  const [memberName, setMemberName] = useState('')

  const { data: members } = useMembersList({ searchTerm: searchTerm || undefined, status: 'Active', page: 1, pageSize: 6 })
  const { data: logs, isLoading } = useMemberWorkoutLogs(memberId || undefined)

  return (
    <div className="space-y-3">
      <Input
        placeholder="Search member to view or log their workouts..."
        value={memberId ? memberName : searchTerm}
        onChange={(e) => {
          setMemberId('')
          setSearchTerm(e.target.value)
        }}
      />
      {!memberId && searchTerm && (
        <div className="divide-y rounded-md border">
          {members?.items.length === 0 && <p className="p-3 text-sm text-muted-foreground">No members match.</p>}
          {members?.items.map((m) => (
            <button
              key={m.id}
              type="button"
              onClick={() => {
                setMemberId(m.id)
                setMemberName(m.fullName)
                setSearchTerm('')
              }}
              className="flex w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-accent"
            >
              {m.fullName}
              <span className="text-xs text-muted-foreground">{m.memberCode}</span>
            </button>
          ))}
        </div>
      )}

      {memberId && (
        <>
          <p className="text-sm font-medium">{memberName}</p>
          <MemberAssignedPlans memberId={memberId} />
          <div className="flex items-center justify-between pt-2">
            <p className="text-sm font-medium">Workout logs</p>
            <LogWorkoutDialog memberId={memberId} />
          </div>
          {isLoading && <Skeleton className="h-24 w-full" />}
          {logs?.length === 0 && <p className="text-sm text-muted-foreground">No workouts logged yet.</p>}
          {logs?.map((log) => (
            <Card key={log.id}>
              <CardContent className="space-y-2 p-3">
                <div className="flex items-center justify-between text-sm">
                  <span className="font-medium">{log.workoutTemplateName ?? log.character}</span>
                  <span className="text-muted-foreground">{new Date(log.loggedAt).toLocaleString()}</span>
                </div>
                <div className="flex flex-wrap gap-1">
                  {log.entries.map((e) => (
                    <Badge key={e.id} variant="outline">
                      {e.exerciseName}: {e.setsCompleted}×{e.repsCompleted}
                      {e.weightKg ? ` @ ${e.weightKg}kg` : ''}
                    </Badge>
                  ))}
                </div>
              </CardContent>
            </Card>
          ))}
        </>
      )}
    </div>
  )
}

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
        <TabsList className="h-auto flex-wrap">
          <TabsTrigger value="exercises">Exercise Library</TabsTrigger>
          <TabsTrigger value="templates">Workout Templates</TabsTrigger>
          <TabsTrigger value="logs">Member Logs</TabsTrigger>
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

        <TabsContent value="logs">
          <MemberWorkoutLogs />
        </TabsContent>
      </Tabs>
    </div>
  )
}
