import { useState } from 'react'
import { Loader2, XCircle } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { useCancelMembership } from '@/modules/members/api/membersApi'

export function CancelMembershipDialog({ memberId, memberMembershipId }: { memberId: string; memberMembershipId: string }) {
  const [open, setOpen] = useState(false)
  const [reason, setReason] = useState('')

  const cancelMembership = useCancelMembership(memberId)

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    cancelMembership.mutate(
      { memberMembershipId, reason: reason || undefined },
      {
        onSuccess: () => {
          toast.success('Membership cancelled.')
          setOpen(false)
          setReason('')
        },
        onError: () => toast.error('Could not cancel membership.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="ghost">
          <XCircle />
          Cancel
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Cancel membership</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="reason">Reason (optional)</Label>
            <Textarea id="reason" value={reason} onChange={(e) => setReason(e.target.value)} />
          </div>
          <DialogFooter>
            <Button type="submit" variant="destructive" disabled={cancelMembership.isPending}>
              {cancelMembership.isPending && <Loader2 className="size-4 animate-spin" />}
              Cancel Membership
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
