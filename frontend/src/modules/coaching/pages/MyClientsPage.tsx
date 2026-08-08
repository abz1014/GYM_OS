import { useEffect, useRef, useState } from 'react'
import { AxiosError } from 'axios'
import { Loader2, Send } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { cn } from '@/lib/utils'
import { ListEmpty, ListError, ListSkeleton, PageHeader } from '@/shared/components/console'
import { useCoachingHub } from '@/shared/hooks/useCoachingHub'
import { SessionChip, SessionPicker, toAttachable } from '@/shared/components/coaching/SessionAttachment'
import { useMemberWorkoutLogs } from '@/modules/workouts/api/workoutsApi'
import {
  useClientConversation,
  useMarkClientMessagesRead,
  useMessageClient,
  useMyClients,
  type MyClientRow,
} from '@/modules/coaching/api/coachingApi'

const timeFormat = new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit' })
const dayFormat = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' })

/** How many more messages each "Load older" press reveals. */
const WINDOW_STEP = 50

/** Today shows a clock; anything older shows the day, because "2:14 PM" on its own is a lie by omission. */
function sentLabel(sentAt: string) {
  const at = new Date(sentAt)
  const isToday = new Date().toDateString() === at.toDateString()
  return isToday ? timeFormat.format(at) : `${dayFormat.format(at)}, ${timeFormat.format(at)}`
}

function ClientRow({
  client,
  isSelected,
  onSelect,
}: {
  client: MyClientRow
  isSelected: boolean
  onSelect: () => void
}) {
  return (
    <button
      type="button"
      onClick={onSelect}
      className={cn(
        'flex w-full items-start gap-3 rounded-2xl border p-3 text-left transition-colors',
        isSelected ? 'border-foreground bg-accent' : 'border-border bg-card hover:bg-accent/50',
      )}
    >
      <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-secondary font-display text-sm font-black">
        {client.memberName.charAt(0).toUpperCase()}
      </span>
      <span className="min-w-0 flex-1">
        <span className="flex items-center justify-between gap-2">
          <span className="truncate font-medium">{client.memberName}</span>
          {/* Only the member's own unread messages light this up. A trainer's unsent-read outbound
              message is the member's business, not a task on the trainer's list. */}
          {client.unreadFromMember > 0 && (
            <span className="flex size-5 shrink-0 items-center justify-center rounded-full bg-primary text-[11px] font-bold text-primary-foreground tabular-nums">
              {client.unreadFromMember}
            </span>
          )}
        </span>
        <span className="block truncate text-sm text-muted-foreground">
          {client.lastMessagePreview ?? 'No messages yet'}
        </span>
        <span className="mt-0.5 flex items-center gap-2 text-xs text-muted-foreground">
          <span className="tabular-nums">{client.memberCode}</span>
          {!client.isActivePairing && <span className="text-warning">Ended</span>}
          {client.lastMessageAt && <span className="tabular-nums">{sentLabel(client.lastMessageAt)}</span>}
        </span>
      </span>
    </button>
  )
}

function Thread({ memberId }: { memberId: string }) {
  // How much of the thread is on screen. Widened by "Load older", reset by switching client — the
  // key on <Thread> remounts this, so a deep scroll into one conversation doesn't carry into the next.
  const [windowSize, setWindowSize] = useState(WINDOW_STEP)
  const conversation = useClientConversation(memberId, windowSize)
  const send = useMessageClient(memberId)
  const markRead = useMarkClientMessagesRead()
  // The client's own recent sessions, so a note can be attached to the one it is about.
  const logs = useMemberWorkoutLogs(memberId)
  const [draft, setDraft] = useState('')
  const [attachedLogId, setAttachedLogId] = useState<string | null>(null)
  const endRef = useRef<HTMLDivElement>(null)

  const unread = conversation.data?.unreadCount ?? 0

  /*
   * Opening a thread marks it read, which is the only moment that means anything — a trainer who has
   * the messages on screen has read them. Guarded on a non-zero count so switching between clients
   * doesn't fire a write per click, and keyed on memberId so it fires again for the next one.
   */
  useEffect(() => {
    if (unread > 0) {
      markRead.mutate(memberId)
    }
    // markRead is a stable mutation object; including it would re-fire on every render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [memberId, unread])

  useEffect(() => {
    endRef.current?.scrollIntoView({ block: 'end' })
  }, [conversation.data?.messages.length])

  if (conversation.isError) {
    return (
      <ListError
        message="We couldn't load this conversation"
        onRetry={() => conversation.refetch()}
        isRetrying={conversation.isFetching}
      />
    )
  }

  if (conversation.isLoading || !conversation.data) {
    return <ListSkeleton rows={5} className="h-16 w-full rounded-2xl" />
  }

  const { messages, canSend, memberName, hasOlder } = conversation.data

  const submit = () => {
    const body = draft.trim()
    if (!body) return
    send.mutate(
      { body, workoutLogId: attachedLogId },
      {
        onSuccess: () => {
          setDraft('')
          // Cleared with the draft: the next message is rarely about the same session, and a sticky
          // attachment would quietly mislabel it.
          setAttachedLogId(null)
        },
        onError: () => toast.error('Could not send that message.'),
      },
    )
  }

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="border-b border-border px-5 py-4">
        <h2 className="font-display text-xl font-bold tracking-tight">{memberName}</h2>
        {!canSend && (
          <p className="mt-1 text-sm text-warning">
            This pairing has ended. You can read the history but not send anything new.
          </p>
        )}
      </div>

      <div className="min-h-0 flex-1 space-y-3 overflow-y-auto p-5">
        {/* Was a dead end: the flag was set and displayed, and there was no way to act on it. */}
        {hasOlder && (
          <div className="text-center">
            <Button
              variant="outline"
              size="sm"
              className="rounded-xl"
              onClick={() => setWindowSize((n) => n + WINDOW_STEP)}
              disabled={conversation.isFetching}
            >
              {conversation.isFetching ? <Loader2 className="size-4 animate-spin" /> : null}
              Load older messages
            </Button>
          </div>
        )}

        {messages.length === 0 ? (
          <ListEmpty
            message="Nothing here yet."
            hint={canSend ? 'Say something about their last session.' : undefined}
          />
        ) : (
          messages.map((m) => {
            const mine = m.author === 'Trainer'
            return (
              <div key={m.id} className={cn('flex', mine ? 'justify-end' : 'justify-start')}>
                <div
                  className={cn(
                    'max-w-[75%] rounded-2xl px-4 py-2.5',
                    mine ? 'bg-primary text-primary-foreground' : 'bg-muted',
                  )}
                >
                  {m.session && <SessionChip session={m.session} onOwnMessage={mine} />}
                  <p className="text-sm whitespace-pre-wrap">{m.body}</p>
                  <p
                    className={cn(
                      'mt-1 text-[11px] tabular-nums',
                      mine ? 'text-primary-foreground/70' : 'text-muted-foreground',
                    )}
                  >
                    {sentLabel(m.sentAt)}
                    {/* Read state is only shown on the trainer's own messages. On the member's, the
                        trainer reading it is what set the flag, so it would always say "Read". */}
                    {mine && m.read && ' · Read'}
                  </p>
                </div>
              </div>
            )
          })
        )}
        <div ref={endRef} />
      </div>

      {canSend && (
        <div className="border-t border-border p-4">
          <SessionPicker
            sessions={(logs.data ?? []).slice(0, 5).map(toAttachable)}
            selectedId={attachedLogId}
            onSelect={setAttachedLogId}
            label="About:"
          />
          <div className="flex items-end gap-2">
            <Textarea
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              placeholder={`Message ${memberName.split(' ')[0]}…`}
              rows={2}
              className="min-h-11 resize-none rounded-2xl"
              onKeyDown={(e) => {
                // Enter sends, Shift+Enter breaks the line — the convention every messaging surface
                // this replaces already uses.
                if (e.key === 'Enter' && !e.shiftKey) {
                  e.preventDefault()
                  submit()
                }
              }}
            />
            <Button className="h-11 rounded-xl" onClick={submit} disabled={!draft.trim() || send.isPending}>
              {send.isPending ? <Loader2 className="size-4 animate-spin" /> : <Send className="size-4" />}
              Send
            </Button>
          </div>
        </div>
      )}
    </div>
  )
}

/**
 * The trainer's half of the member conversation, and the thing that made the messaging work unusable
 * until now. Both endpoints have existed for a while, but nothing told a trainer which of their
 * clients had written to them, so the only way to reach a thread was to know a member id and call
 * the API by hand. A conversation you cannot find is not a feature.
 *
 * Master-detail, matching the members screen: roster on the left ordered by who is waiting, thread on
 * the right. On a phone the roster gives way to the thread once a client is picked, because two
 * panes do not fit and the thread is what was being reached for.
 *
 * The sidebar entry is gated on holding the Trainer role, not merely on trainers.view — an owner has
 * that permission to manage the roster but is nobody's coach, and this endpoint would refuse them.
 * A manager who reaches the URL anyway gets the explanation below rather than a raw 403.
 */
export default function MyClientsPage() {
  // Live delivery: a client writing in reorders the roster and lands in the open thread without a
  // refresh. Failure is non-fatal — see useCoachingHub.
  useCoachingHub()
  const clients = useMyClients()
  const [selectedId, setSelectedId] = useState<string | null>(null)

  const notATrainer =
    clients.isError && (clients.error as AxiosError | null)?.response?.status === 403

  const selected = clients.data?.find((c) => c.memberId === selectedId) ?? null
  const waiting = clients.data?.filter((c) => c.unreadFromMember > 0).length ?? 0

  return (
    <div className="flex h-full min-h-0 flex-col gap-4">
      <PageHeader
        title="My clients"
        description={
          waiting > 0
            ? `${waiting} ${waiting === 1 ? 'client is' : 'clients are'} waiting on a reply`
            : clients.data && clients.data.length > 0
              ? 'Nobody is waiting on a reply.'
              : undefined
        }
      />

      {notATrainer && (
        <div className="rounded-panel border border-border bg-card p-5 shadow-sm">
          <p className="font-medium">This account isn't a trainer</p>
          <p className="mt-1 text-sm text-muted-foreground">
            Coaching threads belong to the coach in them, so this screen only works for an account
            linked to a trainer profile. Managing the trainer roster is under Trainers.
          </p>
        </div>
      )}

      {clients.isError && !notATrainer && (
        <ListError
          message="We couldn't load your clients"
          onRetry={() => clients.refetch()}
          isRetrying={clients.isFetching}
        />
      )}

      {clients.isLoading && <ListSkeleton rows={6} className="h-20 w-full rounded-2xl" />}

      {clients.data?.length === 0 && (
        <ListEmpty
          message="You have no clients assigned."
          hint="Assignments are made from a trainer's page under Trainers."
        />
      )}

      {clients.data && clients.data.length > 0 && (
        <div className="flex min-h-0 flex-1 gap-4">
          {/* Roster hides on a phone once a thread is open — see the component remarks. */}
          <div
            className={cn(
              'min-h-0 w-full shrink-0 space-y-2 overflow-y-auto md:w-80 md:max-w-sm',
              selectedId && 'hidden md:block',
            )}
          >
            {clients.data.map((c) => (
              <ClientRow
                key={c.memberId}
                client={c}
                isSelected={c.memberId === selectedId}
                onSelect={() => setSelectedId(c.memberId)}
              />
            ))}
          </div>

          <div
            className={cn(
              'min-h-0 flex-1 overflow-hidden rounded-panel border border-border bg-card shadow-sm',
              !selectedId && 'hidden md:block',
            )}
          >
            {selected ? (
              <>
                <button
                  type="button"
                  className="border-b border-border px-5 py-2 text-sm text-muted-foreground md:hidden"
                  onClick={() => setSelectedId(null)}
                >
                  ← All clients
                </button>
                <Thread key={selected.memberId} memberId={selected.memberId} />
              </>
            ) : (
              <div className="flex h-full items-center justify-center p-10 text-center">
                <p className="text-sm text-muted-foreground">Pick a client to see your conversation.</p>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
