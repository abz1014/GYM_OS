import { useState } from 'react'
import { Loader2, Snowflake } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { useFreezeMembership } from '@/modules/members/api/membersApi'

export function FreezeMembershipDialog({ memberId, memberMembershipId }: { memberId: string; memberMembershipId: string }) {
  const [open, setOpen] = useState(false)
  const today = new Date().toISOString().slice(0, 10)
  const [freezeStartDate, setFreezeStartDate] = useState(today)
  const [freezeEndDate, setFreezeEndDate] = useState(today)

  const freeze = useFreezeMembership(memberId)

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    freeze.mutate(
      { memberMembershipId, freezeStartDate, freezeEndDate },
      {
        onSuccess: () => {
          toast.success('Membership frozen.')
          setOpen(false)
        },
        onError: () => toast.error('Could not freeze membership — check the plan allows freezing.'),
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
