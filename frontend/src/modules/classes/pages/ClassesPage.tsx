import { Ban, Clock, MapPin, Users } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { cn } from '@/lib/utils'
import { ListEmpty, ListError, ListSkeleton, PageHeader } from '@/shared/components/console'
import {
  useCancelClassSession,
  useClassSchedules,
  useClassSessions,
  useClassTypes,
  useSetClassScheduleActive,
  type ClassSession,
} from '@/modules/classes/api/classesApi'
import { ClassRosterDialog } from '@/modules/classes/components/ClassRosterDialog'
import { CreateClassScheduleDialog } from '@/modules/classes/components/CreateClassScheduleDialog'
import { CreateClassTypeDialog } from '@/modules/classes/components/CreateClassTypeDialog'
import { useUiStore } from '@/stores/uiStore'

const DAY_ORDER = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']

// Sessions store their start as wall-clock-in-UTC, so format in UTC to show the intended local
// class time (e.g. an 18:00 slot always reads "6:00 PM") regardless of the viewer's own time zone.
const timeFmt = new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit', timeZone: 'UTC' })
const dayHeadingFmt = new Intl.DateTimeFormat('en-US', { weekday: 'long', month: 'short', day: 'numeric', timeZone: 'UTC' })

/**
 * The console's tab row is an underline strip; shadcn's Tabs ships a pill sitting in a grey tray.
 * These two strings flatten it into the same shape `FilterTabs` draws.
 *
 * Radix stays underneath rather than being swapped for `FilterTabs` because these tabs genuinely
 * switch panels — a timetable, a session list and a type catalogue are three different things, not
 * three filters over one list — which is the case FilterTabs' own doc says it is not for.
 */
const TAB_LIST_CLASS =
  'h-auto w-full flex-wrap justify-start gap-5 rounded-none border-b border-border bg-transparent p-0'
const TAB_TRIGGER_CLASS =
  '-mb-px h-auto flex-none rounded-none border-0 border-b-2 border-transparent px-0 py-0 pb-2.5 text-muted-foreground shadow-none transition-colors hover:text-foreground data-[state=active]:border-foreground data-[state=active]:bg-transparent data-[state=active]:text-foreground data-[state=active]:shadow-none'

function formatScheduleTime(startTime: string) {
  return timeFmt.format(new Date(`1970-01-01T${startTime}Z`))
}

function ColorDot({ color }: { color: string | null }) {
  return <span className="size-2.5 shrink-0 rounded-full" style={{ backgroundColor: color ?? 'var(--muted-foreground)' }} />
}

/** The small-caps line that heads a group of rows — a weekday, a date, or a tab's own list. */
function GroupLabel({ children, count }: { children: string; count?: number }) {
  return (
    <h3 className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
      {children}
      {count !== undefined && <span className="ml-2 tabular-nums">{count.toLocaleString()}</span>}
    </h3>
  )
}

function ScheduleTab() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const schedulesQuery = useClassSchedules({ branchId })
  const setActive = useSetClassScheduleActive()

  const schedules = schedulesQuery.data

  const toggle = (id: string, isActive: boolean) =>
    setActive.mutate(
      { id, isActive: !isActive },
      { onError: () => toast.error('Could not update the class slot.') }
    )

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        {/* The count is the whole timetable for this branch — /api/classes/schedules is unpaginated
            — but it counts slots, not classes running: an inactive slot is still a row. */}
        <GroupLabel count={schedules?.length}>Recurring slots</GroupLabel>
        <CreateClassScheduleDialog />
      </div>

      {schedulesQuery.isError && (
        <ListError
          message="We couldn't load the weekly timetable"
          onRetry={() => schedulesQuery.refetch()}
          isRetrying={schedulesQuery.isFetching}
        />
      )}

      {schedulesQuery.isLoading && <ListSkeleton rows={5} className="h-24 w-full rounded-2xl" />}

      {!schedulesQuery.isLoading && schedules?.length === 0 && (
        <ListEmpty
          message="No class slots yet."
          hint="Add one and the sessions members can book are generated from it."
        />
      )}

      {schedules &&
        schedules.length > 0 &&
        DAY_ORDER.filter((day) => schedules.some((s) => s.dayOfWeek === day)).map((day) => (
          <div key={day} className="space-y-2">
            <GroupLabel>{day}</GroupLabel>
            {schedules
              .filter((s) => s.dayOfWeek === day)
              .map((s) => (
                <article
                  key={s.id}
                  className={cn(
                    'flex items-center justify-between gap-3 rounded-panel border border-border bg-card p-4 edge-light-soft',
                    !s.isActive && 'opacity-60',
                  )}
                >
                  <div className="min-w-0 space-y-1">
                    <div className="flex items-center gap-2">
                      <ColorDot color={s.colorHex} />
                      <span className="truncate font-medium">{s.classTypeName}</span>
                      {!s.isActive && <Badge variant="secondary">Inactive</Badge>}
                    </div>
                    <p className="text-sm text-muted-foreground tabular-nums">
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
                    className="shrink-0 rounded-xl"
                    disabled={setActive.isPending}
                    onClick={() => toggle(s.id, s.isActive)}
                  >
                    {s.isActive ? 'Deactivate' : 'Reactivate'}
                  </Button>
                </article>
              ))}
          </div>
        ))}
    </div>
  )
}

function ClassTypesTab() {
  const typesQuery = useClassTypes()
  const types = typesQuery.data

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <GroupLabel count={types?.length}>Class types</GroupLabel>
        <CreateClassTypeDialog />
      </div>

      {typesQuery.isError && (
        <ListError
          message="We couldn't load the class types"
          onRetry={() => typesQuery.refetch()}
          isRetrying={typesQuery.isFetching}
        />
      )}

      {/* One ListSkeleton per grid column rather than one tall stack, so the placeholders land in
          the same three columns the cards will occupy. */}
      {typesQuery.isLoading && (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 3 }).map((_, i) => (
            <ListSkeleton key={i} rows={2} className="h-28 w-full rounded-2xl" />
          ))}
        </div>
      )}

      {!typesQuery.isLoading && types?.length === 0 && (
        <ListEmpty message="No class types yet." hint="A type is what a slot schedules — start here." />
      )}

      {/* The duration and capacity on a type are defaults for new slots, not what any class is
          actually running at, so they are labelled as such. A slot may override either. */}
      {types && types.length > 0 && (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {types.map((t) => (
            <article
              key={t.id}
              className={cn(
                'space-y-2 rounded-panel border border-border bg-card p-4 edge-light-soft',
                !t.isActive && 'opacity-60',
              )}
            >
              <div className="flex items-center gap-2">
                <ColorDot color={t.colorHex} />
                <span className="min-w-0 truncate font-medium">{t.name}</span>
                {!t.isActive && <Badge variant="secondary">Inactive</Badge>}
              </div>
              {t.description && <p className="text-sm text-muted-foreground">{t.description}</p>}
              <p className="text-xs text-muted-foreground tabular-nums">
                Defaults to {t.defaultDurationMinutes} min · {t.defaultCapacity} spots
              </p>
            </article>
          ))}
        </div>
      )}
    </div>
  )
}

function SessionsTab() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const sessionsQuery = useClassSessions({ branchId })
  const cancelSession = useCancelClassSession()

  const sessions = sessionsQuery.data

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
    <div className="space-y-4">
      {/*
        No count beside this heading, and no "sessions this week" tile above it. The list is
        whatever GET /api/classes/sessions decided to return: the endpoint takes an optional date
        range, the console sends none, and the response does not report the window the server fell
        back to. Counting it would put a number on screen whose period only the backend knows.
      */}
      <GroupLabel>Upcoming sessions</GroupLabel>

      {sessionsQuery.isError && (
        <ListError
          message="We couldn't load the upcoming sessions"
          onRetry={() => sessionsQuery.refetch()}
          isRetrying={sessionsQuery.isFetching}
        />
      )}

      {sessionsQuery.isLoading && <ListSkeleton rows={6} className="h-24 w-full rounded-2xl" />}

      {!sessionsQuery.isLoading && sessions?.length === 0 && (
        <ListEmpty
          message="No upcoming sessions."
          hint="Add a class slot and sessions are generated from it automatically."
        />
      )}

      {Object.entries(groupedByDate).map(([date, daySessions]) => (
        <div key={date} className="space-y-2">
          <GroupLabel>{dayHeadingFmt.format(new Date(`${date}T00:00:00Z`))}</GroupLabel>
          {daySessions.map((s) => (
            <article
              key={s.id}
              className="flex items-center justify-between gap-3 rounded-panel border border-border bg-card p-4 edge-light-soft"
            >
              <div className="min-w-0 space-y-1">
                <div className="flex items-center gap-2">
                  <ColorDot color={s.colorHex} />
                  <span className="truncate font-medium">{s.classTypeName}</span>
                  {/* In practice this marks a session that has already run: the list query drops
                      cancelled sessions unless it is asked for them, and the console never asks. */}
                  {s.status !== 'Scheduled' && <Badge variant="secondary">{s.status}</Badge>}
                </div>
                <div className="flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-muted-foreground">
                  <span className="flex items-center gap-1 tabular-nums">
                    <Clock className="size-3" />
                    {timeFmt.format(new Date(s.startsAt))} · {s.durationMinutes} min
                  </span>
                  {/* Booked against capacity rather than capacity alone — "20 spots" reads the same
                      whether the class is empty or full, which is the one thing staff open this
                      screen to find out. Both counts come straight off ClassSessionDto. */}
                  <span
                    className={cn(
                      'flex items-center gap-1 tabular-nums',
                      s.bookedCount >= s.capacity && 'font-medium text-warning',
                    )}
                  >
                    <Users className="size-3" />
                    {s.bookedCount}/{s.capacity} booked
                    {s.waitlistCount > 0 && ` · ${s.waitlistCount} waiting`}
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
              <div className="flex shrink-0 items-center gap-1">
                <ClassRosterDialog session={s} />
                <Button
                  size="icon"
                  variant="ghost"
                  title="Cancel session"
                  className="rounded-xl text-muted-foreground hover:text-destructive"
                  disabled={cancelSession.isPending}
                  onClick={() => cancel(s.id)}
                >
                  <Ban className="size-4" />
                </Button>
              </div>
            </article>
          ))}
        </div>
      ))}
    </div>
  )
}

/**
 * Group classes: the recurring timetable, the concrete sessions it generates, and the catalogue of
 * types both are built from.
 *
 * There is no stat row over these tabs, though the console's language invites one. Every figure a
 * classes screen would want is missing at source: an attendance or fill rate needs bookings
 * aggregated across sessions and nothing under /api/classes returns an aggregate of any kind, and
 * a "this week" headline would be labelling a window the client is never told — see the note in
 * SessionsTab. Summing the sessions on screen would produce exactly that unlabelled number.
 */
export default function ClassesPage() {
  return (
    <div className="space-y-4">
      <PageHeader title="Classes" description="The weekly timetable, the sessions it generates, and who is in them." />

      <Tabs defaultValue="schedule" className="gap-4">
        <TabsList className={TAB_LIST_CLASS}>
          <TabsTrigger className={TAB_TRIGGER_CLASS} value="schedule">
            Weekly schedule
          </TabsTrigger>
          <TabsTrigger className={TAB_TRIGGER_CLASS} value="sessions">
            Upcoming sessions
          </TabsTrigger>
          <TabsTrigger className={TAB_TRIGGER_CLASS} value="types">
            Class types
          </TabsTrigger>
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
