import { Ban, Clock, MapPin, Users } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import {
  useCancelClassSession,
  useClassSchedules,
  useClassSessions,
  useClassTypes,
  useSetClassScheduleActive,
  type ClassSession,
} from '@/modules/classes/api/classesApi'
import { CreateClassScheduleDialog } from '@/modules/classes/components/CreateClassScheduleDialog'
import { CreateClassTypeDialog } from '@/modules/classes/components/CreateClassTypeDialog'
import { useUiStore } from '@/stores/uiStore'

const DAY_ORDER = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']

// Sessions store their start as wall-clock-in-UTC, so format in UTC to show the intended local
// class time (e.g. an 18:00 slot always reads "6:00 PM") regardless of the viewer's own time zone.
const timeFmt = new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit', timeZone: 'UTC' })
const dayHeadingFmt = new Intl.DateTimeFormat('en-US', { weekday: 'long', month: 'short', day: 'numeric', timeZone: 'UTC' })

function formatScheduleTime(startTime: string) {
  return timeFmt.format(new Date(`1970-01-01T${startTime}Z`))
}

function ColorDot({ color }: { color: string | null }) {
  return <span className="size-2.5 shrink-0 rounded-full" style={{ backgroundColor: color ?? 'var(--muted-foreground)' }} />
}

function ScheduleTab() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const { data: schedules, isLoading } = useClassSchedules({ branchId })
  const setActive = useSetClassScheduleActive()

  const toggle = (id: string, isActive: boolean) =>
    setActive.mutate(
      { id, isActive: !isActive },
      { onError: () => toast.error('Could not update the class slot.') }
    )

  return (
    <div className="space-y-3">
      <div className="flex justify-end">
        <CreateClassScheduleDialog />
      </div>

      {isLoading && (
        <div className="space-y-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <Skeleton key={i} className="h-20 w-full" />
          ))}
        </div>
      )}

      {!isLoading && schedules?.length === 0 && (
        <p className="py-8 text-center text-sm text-muted-foreground">
          No class slots yet. Add one to start building the weekly timetable.
        </p>
      )}

      {!isLoading &&
        schedules &&
        schedules.length > 0 &&
        DAY_ORDER.filter((day) => schedules.some((s) => s.dayOfWeek === day)).map((day) => (
          <div key={day} className="space-y-2">
            <h3 className="text-sm font-semibold text-muted-foreground">{day}</h3>
            {schedules
              .filter((s) => s.dayOfWeek === day)
              .map((s) => (
                <Card key={s.id} className={s.isActive ? undefined : 'opacity-60'}>
                  <CardContent className="flex items-center justify-between gap-3 p-3">
                    <div className="min-w-0 space-y-1">
                      <div className="flex items-center gap-2">
                        <ColorDot color={s.colorHex} />
                        <span className="truncate font-medium">{s.classTypeName}</span>
                        {!s.isActive && <Badge variant="secondary">Inactive</Badge>}
                      </div>
                      <p className="text-sm text-muted-foreground">
                        {formatScheduleTime(s.startTime)} · {s.durationMinutes} min · {s.capacity} spots
                      </p>
                      <p className="text-xs text-muted-foreground">
                        {s.trainerName ?? 'No instructor'}
                        {s.location ? ` · ${s.location}` : ''}
                      </p>
                    </div>
                    <Button
                      size="sm"
                      variant="outline"
                      className="shrink-0"
                      disabled={setActive.isPending}
                      onClick={() => toggle(s.id, s.isActive)}
                    >
                      {s.isActive ? 'Deactivate' : 'Reactivate'}
                    </Button>
                  </CardContent>
                </Card>
              ))}
          </div>
        ))}
    </div>
  )
}

function ClassTypesTab() {
  const { data: types, isLoading } = useClassTypes()

  return (
    <div className="space-y-3">
      <div className="flex justify-end">
        <CreateClassTypeDialog />
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-24 w-full" />
          ))}
        </div>
      ) : types?.length === 0 ? (
        <p className="py-8 text-center text-sm text-muted-foreground">No class types yet.</p>
      ) : (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {types?.map((t) => (
            <Card key={t.id} className={t.isActive ? undefined : 'opacity-60'}>
              <CardContent className="space-y-1.5 p-3">
                <div className="flex items-center gap-2">
                  <ColorDot color={t.colorHex} />
                  <span className="font-medium">{t.name}</span>
                  {!t.isActive && <Badge variant="secondary">Inactive</Badge>}
                </div>
                {t.description && <p className="text-sm text-muted-foreground">{t.description}</p>}
                <div className="flex flex-wrap gap-1">
                  <Badge variant="outline">{t.defaultDurationMinutes} min</Badge>
                  <Badge variant="outline">{t.defaultCapacity} spots</Badge>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}

function SessionsTab() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const { data: sessions, isLoading } = useClassSessions({ branchId })
  const cancelSession = useCancelClassSession()

  const cancel = (id: string) =>
    cancelSession.mutate(id, {
      onSuccess: () => toast.success('Session cancelled.'),
      onError: () => toast.error('Could not cancel the session.'),
    })

  const groupedByDate = (sessions ?? []).reduce<Record<string, ClassSession[]>>((acc, session) => {
    const key = session.startsAt.slice(0, 10)
    ;(acc[key] ??= []).push(session)
    return acc
  }, {})

  return (
    <div className="space-y-3">
      {isLoading && (
        <div className="space-y-2">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-16 w-full" />
          ))}
        </div>
      )}

      {!isLoading && sessions?.length === 0 && (
        <p className="py-8 text-center text-sm text-muted-foreground">
          No upcoming sessions. Add a class slot and sessions will be generated automatically.
        </p>
      )}

      {!isLoading &&
        Object.entries(groupedByDate).map(([date, daySessions]) => (
          <div key={date} className="space-y-2">
            <h3 className="text-sm font-semibold text-muted-foreground">
              {dayHeadingFmt.format(new Date(`${date}T00:00:00Z`))}
            </h3>
            {daySessions.map((s) => (
              <Card key={s.id}>
                <CardContent className="flex items-center justify-between gap-3 p-3">
                  <div className="min-w-0 space-y-1">
                    <div className="flex items-center gap-2">
                      <ColorDot color={s.colorHex} />
                      <span className="truncate font-medium">{s.classTypeName}</span>
                    </div>
                    <div className="flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-muted-foreground">
                      <span className="flex items-center gap-1">
                        <Clock className="size-3" />
                        {timeFmt.format(new Date(s.startsAt))} · {s.durationMinutes} min
                      </span>
                      <span className="flex items-center gap-1">
                        <Users className="size-3" />
                        {s.capacity} spots
                      </span>
                      {s.location && (
                        <span className="flex items-center gap-1">
                          <MapPin className="size-3" />
                          {s.location}
                        </span>
                      )}
                    </div>
                    <p className="text-xs text-muted-foreground">{s.trainerName ?? 'No instructor'}</p>
                  </div>
                  <Button
                    size="sm"
                    variant="ghost"
                    className="shrink-0 text-muted-foreground hover:text-destructive"
                    disabled={cancelSession.isPending}
                    onClick={() => cancel(s.id)}
                  >
                    <Ban className="size-4" />
                    Cancel
                  </Button>
                </CardContent>
              </Card>
            ))}
          </div>
        ))}
    </div>
  )
}

export default function ClassesPage() {
  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Classes</h1>
        <p className="text-sm text-muted-foreground">Group class timetable, class types, and scheduled sessions.</p>
      </div>

      <Tabs defaultValue="schedule">
        <TabsList className="h-auto flex-wrap">
          <TabsTrigger value="schedule">Weekly Schedule</TabsTrigger>
          <TabsTrigger value="sessions">Upcoming Sessions</TabsTrigger>
          <TabsTrigger value="types">Class Types</TabsTrigger>
        </TabsList>

        <TabsContent value="schedule">
          <ScheduleTab />
        </TabsContent>
        <TabsContent value="sessions">
          <SessionsTab />
        </TabsContent>
        <TabsContent value="types">
          <ClassTypesTab />
        </TabsContent>
      </Tabs>
    </div>
  )
}
