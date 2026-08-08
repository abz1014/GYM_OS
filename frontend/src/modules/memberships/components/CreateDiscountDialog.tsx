import { useState } from 'react'
import { Loader2, Percent } from 'lucide-react'
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
import { useCreateDiscount, useMembershipPlans, type DiscountType } from '@/modules/memberships/api/membershipsApi'

const DISCOUNT_TYPES: DiscountType[] = ['Percentage', 'FixedAmount']

export function CreateDiscountDialog() {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [type, setType] = useState<DiscountType>('Percentage')
  const [value, setValue] = useState('')
  const [membershipPlanId, setMembershipPlanId] = useState('')
  const [validFrom, setValidFrom] = useState('')
  const [validTo, setValidTo] = useState('')

  const { data: plans } = useMembershipPlans(true)
  const createDiscount = useCreateDiscount()

  const reset = () => {
    setName('')
    setValue('')
    setMembershipPlanId('')
    setValidFrom('')
    setValidTo('')
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    createDiscount.mutate(
      {
        name,
        type,
        value: Number(value),
        membershipPlanId: membershipPlanId || undefined,
        validFrom: validFrom || undefined,
        validTo: validTo || undefined,
      },
      {
        onSuccess: () => {
          toast.success('Discount created.')
          setOpen(false)
          reset()
        },
        onError: () => toast.error('Could not create discount.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline" className="rounded-xl">
          <Percent />
          New discount
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create discount</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="discountName">Name</Label>
            <Input id="discountName" required value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Type</Label>
              <Select value={type} onValueChange={(v) => setType(v as DiscountType)}>
                <SelectTrigger className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {DISCOUNT_TYPES.map((t) => (
                    <SelectItem key={t} value={t}>
                      {t === 'Percentage' ? 'Percentage' : 'Fixed amount'}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="discountValue">Value{type === 'Percentage' ? ' (%)' : ''}</Label>
              <Input
                id="discountValue"
                type="number"
                min={0}
                max={type === 'Percentage' ? 100 : undefined}
                step="0.01"
                required
                value={value}
                onChange={(e) => setValue(e.target.value)}
              />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label>Applies to plan (optional)</Label>
            <Select value={membershipPlanId} onValueChange={setMembershipPlanId}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="All plans" />
              </SelectTrigger>
              <SelectContent>
                {plans?.map((p) => (
                  <SelectItem key={p.id} value={p.id}>
                    {p.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="validFrom">Valid from (optional)</Label>
              <Input id="validFrom" type="date" value={validFrom} onChange={(e) => setValidFrom(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="validTo">Valid to (optional)</Label>
              <Input id="validTo" type="date" value={validTo} onChange={(e) => setValidTo(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button type="submit" className="rounded-xl" disabled={createDiscount.isPending}>
              {createDiscount.isPending && <Loader2 className="size-4 animate-spin" />}
              Create
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
