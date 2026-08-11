import { useEffect, useRef, useState } from 'react'
import { CloudOff, Loader2, MessageCircle, RotateCw, Send, UserCircle } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { SessionChip, SessionPicker, toAttachable } from '@/shared/components/coaching/SessionAttachment'
import { useCoachingHub } from '@/shared/hooks/useCoachingHub'
import {
  useMessageMyCoach,
  useMyCoach,
  useMyWorkoutLogs,
  useReadMyCoachMessages,
  type MyCoachMessage,
} from '@/modules/portal/api/portalApi'
import { isStale } from '@/shared/lib/queryTrust'

function sentWhen(iso: string): string {
  return new Date(iso).toLocaleString(undefined, { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' })
}

/** How many more messages each "Load older" press reveals. */
const WINDOW_STEP = 30

function Bubble({ message }: { message: MyCoachMessage }) {
  const mine = message.author === 'Member'
  return (
    <li className={mine ? 'flex justify-end' : 'flex justify-start'}>
      <div className="max-w-[85%] space-y-1">
        <div
          className={
            mine
              ? 'rounded-2xl rounded-br-sm bg-primary px-4 py-2 text-sm text-primary-foreground'
              : 'rounded-2xl rounded-bl-sm bg-muted px-4 py-2 text-sm'
          }
        >
          {message.session && <SessionChip session={message.session} onOwnMessage={mine} />}
          {message.body}
        </div>
        <p className={mine ? 'text-right text-xs text-muted-foreground' : 'text-xs text-muted-foreground'}>
          {sentWhen(message.sentAt)}
        </p>
      </div>
    </li>
  )
}

/**
 * The member's side of the conversation with their trainer.
 *
 * Deliberately not an inbox. There is exactly one thread, because a member has one coach, and it
 * lives under More rather than on the tab bar — the app should not put an unread badge in front of
 * someone whose reason for opening it is to record a session in half a minute.
 *
 * Opening the page marks the trainer's messages as read, which is the only sensible moment for it:
 * there is nothing else here to do first, and leaving a badge up after someone has plainly read the
 * message is how an app teaches people to ignore its badges.
 */
export default function MyCoachPage() {
  // Live delivery: the coach's reply lands in the open thread without a refresh. Non-fatal on
  // failure — see useCoachingHub.
  useCoachingHub()
  const [windowSize, setWindowSize] = useState(WINDOW_STEP)
  const coach = useMyCoach(windowSize)
  const send = useMessageMyCoach()
  const markRead = useReadMyCoachMessages()
  const [body, setBody] = useState('')
  // Their own recent sessions — "this is the one my shoulder went on" is the member's half of the
  // same idea the trainer uses when replying about a workout.
  const logs = useMyWorkoutLogs()
  const [attachedLogId, setAttachedLogId] = useState<string | null>(null)
  const marked = useRef(false)

  useEffect(() => {
    if (!marked.current && (coach.data?.unreadCount ?? 0) > 0) {
      marked.current = true
      markRead.mutate()
    }
  }, [coach.data?.unreadCount, markRead])

  const submit = () => {
    if (!body.trim()) return
    send.mutate(
      { body, workoutLogId: attachedLogId },
      {
        onSuccess: () => {
          setBody('')
          // Cleared with the draft — the next message is rarely about the same session.
          setAttachedLogId(null)
        },
        onError: () => toast.error("Couldn't send that message."),
      },
    )
  }

  if (coach.isLoading) return <Skeleton className="mx-auto h-72 w-full max-w-2xl" />

  /*
   * The failure state has to come before anything reads `data`, because the shape of this page is
   * `trainerId == null ? "no trainer" : the thread`, and a failed request produces the same
   * `undefined` as a member who genuinely has no coach. So a dropped connection told someone paying
   * for personal training that they have no trainer, hid the whole conversation, and pointed them at
   * the front desk to ask about buying PT they already own. There is no version of that which is
   * better than saying the request failed.
   *
   * The query sets `retry: false`, so this fires on the first dropped request — which makes the
   * retry button the only way back short of a page reload.
   */
  if (isStale(coach)) {
    return (
      <div className="mx-auto max-w-2xl space-y-5">
        <h1 className="text-2xl font-semibold tracking-tight">My Coach</h1>
        <Card>
          <CardContent className="flex flex-col items-center gap-3 py-10 text-center">
            <CloudOff className="size-10 text-muted-foreground" />
            <p className="font-medium">We couldn't load your coach</p>
            <p className="max-w-xs text-sm text-muted-foreground">
              This is a connection problem, not a change to your training.
            </p>
            <Button
              variant="outline"
              className="rounded-xl"
              onClick={() => void coach.refetch()}
              disabled={coach.isFetching}
            >
              {coach.isFetching ? <Loader2 className="size-4 animate-spin" /> : <RotateCw className="size-4" />}
              {coach.isFetching ? 'Trying…' : 'Try again'}
            </Button>
          </CardContent>
        </Card>
      </div>
    )
  }

  const data = coach.data
  const hasCoach = data?.trainerId != null

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">My Coach</h1>
        <p className="text-sm text-muted-foreground">
          {hasCoach ? data!.trainerName : 'You don’t have a trainer assigned yet.'}
        </p>
      </div>

      {!hasCoach ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-3 py-10 text-center">
            <UserCircle className="size-10 text-muted-foreground" />
            <p className="font-medium">No trainer yet</p>
            <p className="max-w-xs text-sm text-muted-foreground">
              Ask at the front desk about personal training — everything you log already counts either way.
            </p>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardContent className="space-y-4 py-5">
            {data!.messages.length === 0 ? (
              <div className="flex flex-col items-center gap-2 py-8 text-center">
                <MessageCircle className="size-8 text-muted-foreground" />
                <p className="text-sm text-muted-foreground">
                  Nothing here yet. Ask {data!.trainerName?.split(' ')[0]} anything about your training.
                </p>
              </div>
            ) : (
              <>
                {/* The window is bounded server-side, and until now the member had no way past it —
                    their own history simply stopped with no explanation. */}
                {data!.hasOlder && (
                  <div className="text-center">
                    <Button
                      variant="outline"
                      size="sm"
                      className="rounded-xl"
                      onClick={() => setWindowSize((n) => n + WINDOW_STEP)}
                      disabled={coach.isFetching}
                    >
                      {coach.isFetching ? <Loader2 className="size-4 animate-spin" /> : null}
                      Load older messages
                    </Button>
                  </div>
                )}
                <ul className="space-y-3">
                  {data!.messages.map((m) => (
                    <Bubble key={m.id} message={m} />
                  ))}
                </ul>
              </>
            )}

            {/* A pairing that has ended keeps its history — there is just nobody on the other end. */}
            {data!.canSend ? (
              <div className="space-y-2 border-t pt-4">
                <SessionPicker
                  sessions={(logs.data ?? []).slice(0, 5).map(toAttachable)}
                  selectedId={attachedLogId}
                  onSelect={setAttachedLogId}
                  label="About:"
                />
                <Textarea
                  value={body}
                  onChange={(e) => setBody(e.target.value)}
                  placeholder="Ask about your programme, a lift, or how a session felt…"
                  rows={3}
                  maxLength={2000}
                />
                <Button className="h-11 w-full" disabled={!body.trim() || send.isPending} onClick={submit}>
                  {send.isPending ? <Loader2 className="size-4 animate-spin" /> : <Send className="size-4" />}
                  {send.isPending ? 'Sending…' : 'Send'}
                </Button>
              </div>
            ) : (
              <p className="border-t pt-4 text-center text-sm text-muted-foreground">
                This coaching has ended. You can still read everything here.
              </p>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  )
}
