import { useState } from 'react'
import { Loader2, Plus } from 'lucide-react'
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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useCreateMembershipPlan, type MembershipPlanType } from '@/modules/memberships/api/membershipsApi'

const PLAN_TYPES: MembershipPlanType[] = ['Monthly', 'Quarterly', 'Annual', 'Family', 'Corporate', 'Custom']

export function CreatePlanDialog() {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [type, setType] = useState<MembershipPlanType>('Monthly')
  const [durationDays, setDurationDays] = useState(30)
  const [price, setPrice] = useState(0)
  const [maxFreezeDays, setMaxFreezeDays] = useState(0)

  const createPlan = useCreateMembershipPlan()

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    createPlan.mutate(
      { name, type, durationDays, price, currency: 'USD', maxFreezeDays },
      {
        onSuccess: () => {
          toast.success('Plan created.')
          setOpen(false)
          setName('')
        },
        onError: () => toast.error('Could not create plan.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <Plus />
          New Plan
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create membership plan</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="name">Name</Label>
            <Input id="name" required value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Type</Label>
              <Select value={type} onValueChange={(v) => setType(v as MembershipPlanType)}>
                <SelectTrigger className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {PLAN_TYPES.map((t) => (
                    <SelectItem key={t} value={t}>
                      {t}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="durationDays">Duration (days)</Label>
              <Input
                id="durationDays"
                type="number"
                min={1}
                required
                value={durationDays}
                onChange={(e) => setDurationDays(Number(e.target.value))}
              />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="price">Price (USD)</Label>
              <Input
                id="price"
                type="number"
                min={0}
                step="0.01"
                required
                value={price}
                onChange={(e) => setPrice(Number(e.target.value))}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="maxFreezeDays">Max freeze days</Label>
              <Input
                id="maxFreezeDays"
                type="number"
                min={0}
                value={maxFreezeDays}
                onChange={(e) => setMaxFreezeDays(Number(e.target.value))}
              />
            </div>
          </div>
          <DialogFooter>
            <Button type="submit" disabled={createPlan.isPending}>
              {createPlan.isPending && <Loader2 className="size-4 animate-spin" />}
              Create
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
