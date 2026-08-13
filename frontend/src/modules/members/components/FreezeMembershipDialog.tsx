import { useState } from 'react'
import { Loader2, Snowflake } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { useFreezeMembership } from '@/modules/members/api/membersApi'

export function FreezeMembershipDialog({
  memberId, memberMembershipId, freezeDaysUsed, planMaxFreezeDays,
}: {
  memberId: string
  memberMembershipId: string
  /** Both come off the membership row itself, so the sentence below is real data, not a guess. */
  freezeDaysUsed: number
  planMaxFreezeDays: number | null
}) {
  const [open, setOpen] = useState(false)
  const today = new Date().toISOString().slice(0, 10)
  const [freezeStartDate, setFreezeStartDate] = useState(today)
  const [freezeEndDate, setFreezeEndDate] = useState(today)

  const freeze = useFreezeMembership(memberId)

  const remaining = planMaxFreezeDays !== null ? Math.max(0, planMaxFreezeDays - freezeDaysUsed) : null

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    freeze.mutate(
      { memberMembershipId, freezeStartDate, freezeEndDate },
      {
        onSuccess: () => {
          toast.success('Membership frozen.')
          setOpen(false)
        },
        // The server's refusal names the actual rule that fired — "has N of its M freeze day(s)
        // left", "already frozen", "starts before this membership does". The old hardcoded text
        // told staff to check the plan when the plan was often not the problem.
        onError: (err) =>
          toast.error(
            (err as { response?: { data?: { title?: string } } })?.response?.data?.title
              ?? 'Could not freeze membership.'
          ),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="ghost">
          <Snowflake />
          Freeze
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Freeze membership</DialogTitle>
          {/* The remaining allowance, stated BEFORE the attempt — otherwise the only carrier of
              this number is the 400 a too-long request comes back with. */}
          {remaining !== null && planMaxFreezeDays !== null && planMaxFreezeDays > 0 && (
            <DialogDescription>
              {freezeDaysUsed > 0
                ? `This membership has ${remaining} of its ${planMaxFreezeDays} freeze days left.`
                : `This plan allows up to ${planMaxFreezeDays} freeze days.`}
            </DialogDescription>
          )}
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="freezeStart">Freeze start</Label>
            <Input id="freezeStart" type="date" value={freezeStartDate} onChange={(e) => setFreezeStartDate(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="freezeEnd">Freeze end</Label>
            <Input id="freezeEnd" type="date" value={freezeEndDate} onChange={(e) => setFreezeEndDate(e.target.value)} />
          </div>
          <DialogFooter>
            <Button type="submit" disabled={freeze.isPending}>
              {freeze.isPending && <Loader2 className="size-4 animate-spin" />}
              Freeze
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
