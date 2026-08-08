import { useEffect, useRef, useState } from 'react'
import { Loader2, MessageCircle, Send, UserCircle } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { useCoachingHub } from '@/shared/hooks/useCoachingHub'
import {
  useMessageMyCoach,
  useMyCoach,
  useReadMyCoachMessages,
  type MyCoachMessage,
} from '@/modules/portal/api/portalApi'

function sentWhen(iso: string): string {
  return new Date(iso).toLocaleString(undefined, { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' })
}

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
  const coach = useMyCoach()
  const send = useMessageMyCoach()
  const markRead = useReadMyCoachMessages()
  const [body, setBody] = useState('')
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
      { body },
      {
        onSuccess: () => setBody(''),
        onError: () => toast.error("Couldn't send that message."),
      },
    )
  }

  if (coach.isLoading) return <Skeleton className="mx-auto h-72 w-full max-w-2xl" />

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
              <ul className="space-y-3">
                {data!.messages.map((m) => (
                  <Bubble key={m.id} message={m} />
                ))}
              </ul>
            )}

            {/* A pairing that has ended keeps its history — there is just nobody on the other end. */}
            {data!.canSend ? (
              <div className="space-y-2 border-t pt-4">
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
