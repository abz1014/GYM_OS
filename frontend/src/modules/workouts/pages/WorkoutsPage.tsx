import { useState } from 'react'

import { Badge } from '@/components/ui/badge'
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
        <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">Assigned plans</p>
        <AssignWorkoutTemplateDialog memberId={memberId} />
      </div>
      {isLoading && <Skeleton className="h-20 w-full rounded-2xl" />}
      {assignments?.length === 0 && <p className="text-sm text-muted-foreground">No workout plan assigned yet.</p>}
      {assignments?.map((a) => (
        <div key={a.id} className="space-y-2 rounded-panel border border-border bg-card p-3 edge-light-soft">
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
        </div>
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
        <div className="divide-y divide-border overflow-hidden rounded-panel border border-border bg-card edge-light-soft">
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
          <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">{memberName}</p>
          <MemberAssignedPlans memberId={memberId} />
          <div className="flex items-center justify-between pt-2">
            <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">Workout logs</p>
            <LogWorkoutDialog memberId={memberId} />
          </div>
          {isLoading && <Skeleton className="h-24 w-full rounded-2xl" />}
          {logs?.length === 0 && <p className="text-sm text-muted-foreground">No workouts logged yet.</p>}
          {logs?.map((log) => (
            <div key={log.id} className="space-y-2 rounded-panel border border-border bg-card p-3 edge-light-soft">
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
            </div>
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
        <h1 className="font-display text-3xl leading-tight font-black tracking-tight">Workouts</h1>
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
                <Skeleton key={i} className="h-20 w-full rounded-2xl" />
              ))}
            </div>
          ) : (
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {exercises?.map((ex) => (
                <div key={ex.id} className="space-y-1 rounded-panel border border-border bg-card p-3 edge-light-soft">
                    <p className="font-medium">{ex.name}</p>
                    <div className="flex flex-wrap gap-1">
                      {ex.muscleGroup && <Badge variant="outline">{ex.muscleGroup}</Badge>}
                      {ex.equipment && <Badge variant="secondary">{ex.equipment}</Badge>}
                    </div>
                </div>
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
                <Skeleton key={i} className="h-24 w-full rounded-2xl" />
              ))}
            </div>
          ) : templates?.length === 0 ? (
            <p className="text-sm text-muted-foreground">No workout templates yet.</p>
          ) : (
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {templates?.map((t) => (
                <div key={t.id} className="space-y-1 rounded-panel border border-border bg-card p-3 edge-light-soft">
                    <p className="font-medium">{t.name}</p>
                    {t.description && <p className="text-sm text-muted-foreground">{t.description}</p>}
                    <Badge variant="outline">{t.exerciseCount} exercises</Badge>
                </div>
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
