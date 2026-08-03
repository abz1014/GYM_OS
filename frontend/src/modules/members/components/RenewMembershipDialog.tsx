import { useState } from 'react'
import { Loader2, RefreshCw } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useMembershipPlans } from '@/modules/memberships/api/membershipsApi'
import { useRenewMembership } from '@/modules/members/api/membersApi'

export function RenewMembershipDialog({ memberId }: { memberId: string }) {
  const [open, setOpen] = useState(false)
  const [planId, setPlanId] = useState('')
  const [startDate, setStartDate] = useState(new Date().toISOString().slice(0, 10))
  const [autoRenew, setAutoRenew] = useState(false)
  const [couponCode, setCouponCode] = useState('')

  const { data: plans } = useMembershipPlans()
  const renew = useRenewMembership(memberId)

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!planId) {
      toast.error('Please select a plan.')
      return
    }

    renew.mutate(
      { membershipPlanId: planId, startDate, autoRenew, couponCode: couponCode || null },
      {
        onSuccess: () => {
          toast.success('Membership added.')
          setOpen(false)
        },
        onError: () => toast.error('Could not add membership.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          <RefreshCw />
          Renew / Assign Plan
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Renew or assign a membership</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label>Plan</Label>
            <Select value={planId} onValueChange={setPlanId}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Select a plan" />
              </SelectTrigger>
              <SelectContent>
                {plans?.map((plan) => (
                  <SelectItem key={plan.id} value={plan.id}>
                    {plan.name} — {plan.price.toLocaleString('en-US', { style: 'currency', currency: plan.currency })}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="startDate">Start date</Label>
            <Input id="startDate" type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="couponCode">Coupon code (optional)</Label>
            <Input id="couponCode" value={couponCode} onChange={(e) => setCouponCode(e.target.value)} />
          </div>
          <div className="flex items-center gap-2">
            <Checkbox id="autoRenew" checked={autoRenew} onCheckedChange={(v) => setAutoRenew(v === true)} />
            <Label htmlFor="autoRenew" className="font-normal">
              Auto-renew
            </Label>
          </div>
          <DialogFooter>
            <Button type="submit" disabled={renew.isPending}>
              {renew.isPending && <Loader2 className="size-4 animate-spin" />}
              Confirm
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
