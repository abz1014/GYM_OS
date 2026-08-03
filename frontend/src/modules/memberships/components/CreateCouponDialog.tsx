import { useState } from 'react'
import { Loader2, Ticket } from 'lucide-react'
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
import { useCreateCoupon, useDiscounts } from '@/modules/memberships/api/membershipsApi'

export function CreateCouponDialog() {
  const [open, setOpen] = useState(false)
  const [code, setCode] = useState('')
  const [discountId, setDiscountId] = useState('')
  const [maxRedemptions, setMaxRedemptions] = useState('')
  const [validFrom, setValidFrom] = useState('')
  const [validTo, setValidTo] = useState('')

  const { data: discounts } = useDiscounts()
  const createCoupon = useCreateCoupon()

  const reset = () => {
    setCode('')
    setDiscountId('')
    setMaxRedemptions('')
    setValidFrom('')
    setValidTo('')
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!discountId) {
      toast.error('Select a discount.')
      return
    }

    createCoupon.mutate(
      {
        code,
        discountId,
        maxRedemptions: maxRedemptions ? Number(maxRedemptions) : undefined,
        validFrom: validFrom || undefined,
        validTo: validTo || undefined,
      },
      {
        onSuccess: () => {
          toast.success('Coupon created.')
          setOpen(false)
          reset()
        },
        onError: (err: unknown) =>
          toast.error((err as { response?: { data?: { title?: string } } })?.response?.data?.title ?? 'Could not create coupon.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          <Ticket />
          New Coupon
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create coupon</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="couponCode">Code</Label>
            <Input
              id="couponCode"
              required
              value={code}
              onChange={(e) => setCode(e.target.value.toUpperCase())}
              placeholder="e.g. SUMMER20"
            />
          </div>
          <div className="space-y-1.5">
            <Label>Discount</Label>
            <Select value={discountId} onValueChange={setDiscountId}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Select discount" />
              </SelectTrigger>
              <SelectContent>
                {discounts?.map((d) => (
                  <SelectItem key={d.id} value={d.id}>
                    {d.name} ({d.type === 'Percentage' ? `${d.value}%` : `$${d.value}`})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="maxRedemptions">Max redemptions (optional)</Label>
            <Input
              id="maxRedemptions"
              type="number"
              min={1}
              value={maxRedemptions}
              onChange={(e) => setMaxRedemptions(e.target.value)}
              placeholder="Unlimited"
            />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="couponValidFrom">Valid from (optional)</Label>
              <Input id="couponValidFrom" type="date" value={validFrom} onChange={(e) => setValidFrom(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="couponValidTo">Valid to (optional)</Label>
              <Input id="couponValidTo" type="date" value={validTo} onChange={(e) => setValidTo(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button type="submit" disabled={createCoupon.isPending}>
              {createCoupon.isPending && <Loader2 className="size-4 animate-spin" />}
              Create
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
