import { Dumbbell, X } from 'lucide-react'

import { cn } from '@/lib/utils'

/** Mirrors CoachMessageSessionDto. Null on a message that is about nothing, which is most of them. */
export interface CoachMessageSession {
  id: string
  /** The trainer's own template name when there is one, otherwise the session's character. */
  label: string
  loggedAt: string
  exerciseCount: number
}

/** A session a message can be attached to, as either side's recent-workout list returns it. */
export interface AttachableSession {
  id: string
  label: string
  loggedAt: string
}

const dayFormat = new Intl.DateTimeFormat('en-US', { weekday: 'short', month: 'short', day: 'numeric' })

/**
 * Turns a workout log into something attachable, by the SAME rule the server uses to label a message
 * that already references one (CoachMessageSessions): the trainer's own template name wins, and the
 * derived character is the fallback. Written once here because the picker and the chip must agree —
 * choosing a session called "Week 3 — Upper A" and then seeing it come back as "Push day" would make
 * a coach doubt they attached the right one.
 */
export function toAttachable(log: {
  id: string
  workoutTemplateName: string | null
  character: string
  loggedAt: string
}): AttachableSession {
  return {
    id: log.id,
    label: log.workoutTemplateName?.trim() ? log.workoutTemplateName : log.character,
    loggedAt: log.loggedAt,
  }
}

export const sessionDay = (loggedAt: string) => dayFormat.format(new Date(loggedAt))

/**
 * The session a message is about, shown on the message itself.
 *
 * This is the thing that makes a coaching thread worth more than a chat app. "Drop to 55kg" read
 * against the session it refers to is coaching; the same words floating in a list of messages are a
 * text nobody can act on three days later. Every CoachMessage has carried a WorkoutLogId since the
 * feature was built and neither UI ever showed it.
 *
 * Rendered inside the bubble rather than beside it, so it travels with the message it belongs to and
 * cannot be misread as attached to the one below.
 */
export function SessionChip({ session, onOwnMessage }: { session: CoachMessageSession; onOwnMessage: boolean }) {
  return (
    <span
      className={cn(
        'mb-1.5 flex items-center gap-1.5 rounded-xl px-2.5 py-1.5 text-xs',
        // On your own bubble the surrounding text is already inverted, so the chip borrows that
        // contrast instead of introducing a third colour.
        onOwnMessage ? 'bg-primary-foreground/15 text-primary-foreground' : 'bg-background/60 text-muted-foreground',
      )}
    >
      <Dumbbell className="size-3.5 shrink-0" />
      <span className="min-w-0 truncate font-medium">{session.label}</span>
      <span className="shrink-0 tabular-nums opacity-80">
        · {sessionDay(session.loggedAt)} · {session.exerciseCount} exercise{session.exerciseCount === 1 ? '' : 's'}
      </span>
    </span>
  )
}

/**
 * The composer's picker: which session is this message about?
 *
 * A short list of recent sessions rather than a search, because the useful answer is almost always
 * "the one they just did" — a coach replies to a session while it is still the last one, and a
 * member asks about the one that hurt. Anything older is reachable from the member's own history,
 * and a picker that could reach it would be a worse control for the common case.
 */
export function SessionPicker({
  sessions,
  selectedId,
  onSelect,
  label = 'About a session',
}: {
  sessions: AttachableSession[]
  selectedId: string | null
  onSelect: (id: string | null) => void
  label?: string
}) {
  const selected = sessions.find((s) => s.id === selectedId) ?? null

  // Nothing to attach yet. Silent rather than an empty control that implies the member has trained
  // and the app has lost it.
  if (sessions.length === 0) {
    return null
  }

  if (selected) {
    return (
      <div className="mb-2 flex items-center gap-2 rounded-xl border border-border bg-muted px-3 py-2 text-xs">
        <Dumbbell className="size-3.5 shrink-0 text-muted-foreground" />
        <span className="min-w-0 flex-1 truncate">
          <span className="font-medium">{selected.label}</span>
          <span className="text-muted-foreground tabular-nums"> · {sessionDay(selected.loggedAt)}</span>
        </span>
        <button
          type="button"
          onClick={() => onSelect(null)}
          aria-label="Don't attach a session"
          className="shrink-0 rounded-lg p-1 text-muted-foreground hover:text-foreground"
        >
          <X className="size-3.5" />
        </button>
      </div>
    )
  }

  return (
    <div className="mb-2 flex flex-wrap items-center gap-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      {sessions.slice(0, 3).map((s) => (
        <button
          key={s.id}
          type="button"
          onClick={() => onSelect(s.id)}
          className="rounded-xl border border-border px-2.5 py-1 text-xs transition-colors hover:bg-accent"
        >
          <span className="font-medium">{s.label}</span>
          <span className="text-muted-foreground tabular-nums"> · {sessionDay(s.loggedAt)}</span>
        </button>
      ))}
    </div>
  )
}
