import { useState } from 'react'
import { Loader2, PlayCircle, RefreshCw, Snowflake, XCircle } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import type { MemberMembership } from '@/modules/members/api/membersApi'
import {
  serverReason,
  useFreezeMyMembership,
  useRequestMyCancellation,
  useResumeMyMembership,
  useSetMyAutoRenew,
} from '@/modules/portal/api/portalApi'
import { dateFormat } from '@/modules/portal/components/portalShared'

const MS_PER_DAY = 86_400_000

/**
 * A calendar date as the member's own device reckons it.
 *
 * `toISOString().slice(0, 10)` rolls over at UTC midnight, so a member opening this at 8pm in New
 * York would be handed tomorrow's date as "today" — and then told by the server that their freeze
 * cannot start when they asked. The date inputs below are calendar dates, so they are built from
 * local components.
 */
function isoDay(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

/** DateOnly wire values ("2026-08-27") are calendar dates — parse at local midnight, not UTC. */
function parseDateOnly(value: string): Date {
  return new Date(`${value.slice(0, 10)}T00:00:00`)
}

/**
 * Pausing a membership, by the person whose membership it is.
 *
 * The allowance is stated BEFORE the attempt, from the membership row itself, so the member knows
 * what they have to spend rather than discovering it in a rejection. When the server does refuse,
 * its `title` is the sentence shown: it names the rule that fired ("has 3 of its 30 freeze days
 * left", "already frozen", "starts before this membership does") and a generic "Could not freeze"
 * would throw away the only part of the answer that tells them what to ask for instead.
 */
function FreezeDialog({ membership }: { membership: MemberMembership }) {
  const [open, setOpen] = useState(false)
  const today = isoDay(new Date())
  const [freezeStartDate, setFreezeStartDate] = useState(today)
  const [freezeEndDate, setFreezeEndDate] = useState(isoDay(new Date(Date.now() + 7 * MS_PER_DAY)))

  const freeze = useFreezeMyMembership()

  const max = membership.planMaxFreezeDays
  const remaining = max !== null ? Math.max(0, max - membership.freezeDaysUsed) : null

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    freeze.mutate(
      { freezeStartDate, freezeEndDate },
      {
        onSuccess: () => {
          toast.success('Your membership is frozen.')
          setOpen(false)
        },
        onError: (err) => toast.error(serverReason(err, 'Could not freeze your membership.')),
      },
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button variant="outline" className="h-11 w-full justify-start rounded-xl sm:w-auto">
          <Snowflake className="size-4" />
          Freeze membership
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Freeze your membership</DialogTitle>
          <DialogDescription>
            {remaining !== null && max !== null && max > 0
              ? membership.freezeDaysUsed > 0
                ? `You have ${remaining} of your ${max} freeze days left.`
                : `Your plan allows up to ${max} freeze days.`
              : // No allowance figure on the row means we do not know one. The server still decides,
                // and its answer is what the member will be shown.
                'Your gym decides how long a membership can be paused.'}
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="myFreezeStart">First day frozen</Label>
            <Input
              id="myFreezeStart"
              type="date"
              required
              className="h-11"
              value={freezeStartDate}
              onChange={(e) => setFreezeStartDate(e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="myFreezeEnd">Last day frozen</Label>
            <Input
              id="myFreezeEnd"
              type="date"
              required
              className="h-11"
              value={freezeEndDate}
              onChange={(e) => setFreezeEndDate(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button type="submit" className="h-11 w-full rounded-xl sm:w-auto" disabled={freeze.isPending}>
              {freeze.isPending && <Loader2 className="size-4 animate-spin" />}
              Freeze
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/** Only ever rendered for a Frozen membership — there is nothing to resume otherwise. */
function ResumeButton() {
  const resume = useResumeMyMembership()

  const handleResume = () =>
    resume.mutate(undefined, {
      onSuccess: () => toast.success('Your membership is active again.'),
      onError: (err) => toast.error(serverReason(err, 'Could not resume your membership.')),
    })

  return (
    <Button className="h-11 w-full justify-start rounded-xl sm:w-auto" disabled={resume.isPending} onClick={handleResume}>
      {resume.isPending ? <Loader2 className="size-4 animate-spin" /> : <PlayCircle className="size-4" />}
      Resume now
    </Button>
  )
}

/**
 * The renewal switch, with the consequence of switching it off spelled out against a real date.
 *
 * Turning it ON is a one-tap, reversible commitment to keep training and needs no ceremony. Turning
 * it OFF is the tap that ends a membership on a specific day, so it confirms — and the confirmation
 * names that day rather than saying "are you sure".
 */
function AutoRenewControl({ membership }: { membership: MemberMembership }) {
  const [confirmOpen, setConfirmOpen] = useState(false)
  const setAutoRenew = useSetMyAutoRenew()
  const endsOn = dateFormat.format(parseDateOnly(membership.endDate))

  const apply = (enabled: boolean) =>
    setAutoRenew.mutate(enabled, {
      onSuccess: () => {
        toast.success(enabled ? 'Auto-renew is on.' : 'Auto-renew is off.')
        setConfirmOpen(false)
      },
      onError: (err) => toast.error(serverReason(err, 'Could not change auto-renew.')),
    })

  return (
    <>
      <div className="flex items-center justify-between gap-3 rounded-xl border p-3">
        <div className="min-w-0">
          <p className="flex items-center gap-1.5 text-sm font-medium">
            <RefreshCw className="size-3.5 shrink-0 text-muted-foreground" />
            Auto-renew
          </p>
          <p className="mt-0.5 text-xs text-muted-foreground">
            {membership.autoRenew
              ? `Renews automatically on ${endsOn}.`
              : `Ends on ${endsOn} unless you renew it.`}
          </p>
        </div>
        <Button
          variant={membership.autoRenew ? 'default' : 'outline'}
          className="h-11 shrink-0 rounded-xl px-5"
          disabled={setAutoRenew.isPending}
          onClick={() => (membership.autoRenew ? setConfirmOpen(true) : apply(true))}
        >
          {setAutoRenew.isPending && <Loader2 className="size-4 animate-spin" />}
          {membership.autoRenew ? 'On' : 'Off'}
        </Button>
      </div>

      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Turn off auto-renew?</DialogTitle>
            <DialogDescription>
              Your membership will not renew after {endsOn}. You can turn this back on any time before
              then.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter className="gap-2">
            <Button variant="outline" className="h-11 rounded-xl" onClick={() => setConfirmOpen(false)}>
              Keep it on
            </Button>
            <Button
              variant="destructive"
              className="h-11 rounded-xl"
              disabled={setAutoRenew.isPending}
              onClick={() => apply(false)}
            >
              {setAutoRenew.isPending && <Loader2 className="size-4 animate-spin" />}
              Turn off
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}

/**
 * A request, and the dialog says so in the first sentence.
 *
 * This endpoint files a message for the front desk; it does not cancel anything, and nothing about
 * the membership changes when it succeeds. A member who taps a button labelled "Request
 * cancellation", sees a success toast, and stops going to the gym on the belief they have cancelled
 * will be billed again — so the honest sentence is on the screen they read before they act, not
 * only in the toast afterwards.
 */
function CancelRequestDialog() {
  const [open, setOpen] = useState(false)
  const [reason, setReason] = useState('')
  const request = useRequestMyCancellation()

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    request.mutate(reason.trim(), {
      onSuccess: () => {
        toast.success('Request sent — the front desk will be in touch.')
        setOpen(false)
        setReason('')
      },
      onError: (err) => toast.error(serverReason(err, 'Could not send your request.')),
    })
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button variant="ghost" className="h-11 w-full justify-start rounded-xl text-muted-foreground sm:w-auto">
          <XCircle className="size-4" />
          Request cancellation
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Request cancellation</DialogTitle>
          <DialogDescription>
            This does not cancel your membership. It sends your request to the front desk, and someone
            will contact you to go through it. Until then your membership carries on as normal.
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="cancelReason">Why are you cancelling?</Label>
            <Textarea
              id="cancelReason"
              required
              rows={4}
              placeholder="Moving away, too expensive, not using it…"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button
              type="submit"
              className="h-11 w-full rounded-xl sm:w-auto"
              disabled={request.isPending || reason.trim().length === 0}
            >
              {request.isPending && <Loader2 className="size-4 animate-spin" />}
              Send request
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/**
 * Everything a member can DO about their membership, in one place.
 *
 * Which controls appear is decided by the membership's own status, not by hiding failures: Resume is
 * offered only for a Frozen membership because there is nothing to resume otherwise, and Freeze is
 * withheld from one that is already frozen for the same reason. The server is still the authority on
 * every one of these — the point of the status check is to avoid offering an action whose only
 * possible outcome is a refusal.
 */
export function ManageMembership({ membership }: { membership: MemberMembership }) {
  const frozen = membership.status === 'Frozen'

  /*
   * A membership that has ENDED has nothing here to manage.
   *
   * Every control below acts on a live agreement: freezing a cancelled membership is refused by the
   * server ("Only an active membership can be frozen"), requesting cancellation of something already
   * cancelled is nonsense, and an auto-renew switch on an expired row promises a renewal that will
   * never run. Rendering them anyway would be the dead-button pattern this component's own rule
   * exists to avoid — so the honest answer is the status and where to go with it.
   */
  const live = membership.status === 'Active' || frozen || membership.status === 'PendingActivation'
  if (!live) {
    return (
      <p className="py-2 text-sm text-muted-foreground">
        This membership {membership.status === 'Expired' ? 'has expired' : 'has ended'}. The front
        desk can restart it for you — see Your gym below for how to reach them.
      </p>
    )
  }

  return (
    <div className="space-y-3">
      <AutoRenewControl membership={membership} />
      <div className="flex flex-col gap-2 sm:flex-row sm:flex-wrap">
        {frozen ? <ResumeButton /> : <FreezeDialog membership={membership} />}
        <CancelRequestDialog />
      </div>
    </div>
  )
}
