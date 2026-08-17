import { useState } from 'react'
import { Loader2, Pencil } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { useUpdateMembershipPlan, type MembershipPlan } from '@/modules/memberships/api/membershipsApi'

/**
 * Raising a price, renaming a plan, or taking one off sale.
 *
 * The backend has always supported this — `PUT /api/membership-plans/{id}` and
 * UpdateMembershipPlanCommand, price and IsActive included — and the console shipped only a create
 * dialog, so the most routine decision an owner makes was the one thing they could not do here.
 *
 * WHAT EDITING A PRICE DOES AND DOES NOT DO, said on the screen rather than left to be discovered:
 * MemberMembership stores PricePaid per membership, so changing a plan's price sets what the NEXT
 * signup or renewal costs and never rewrites what somebody already paid.
 *
 * Duration and type are absent because the command does not accept them: a plan whose length
 * changed underneath live memberships would silently move end dates that members are counting on.
 * A different length is a different plan.
 */
export function EditPlanDialog({ plan }: { plan: MembershipPlan }) {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState(plan.name)
  const [description, setDescription] = useState(plan.description ?? '')
  const [price, setPrice] = useState(String(plan.price))
  const [maxFreezeDays, setMaxFreezeDays] = useState(String(plan.maxFreezeDays))
  const [isActive, setIsActive] = useState(plan.isActive)

  const update = useUpdateMembershipPlan()

  const submit = (e: React.FormEvent) => {
    e.preventDefault()
    update.mutate(
      {
        id: plan.id,
        name: name.trim(),
        description: description.trim() || null,
        price: Number(price),
        maxFreezeDays: Number(maxFreezeDays),
        isActive,
      },
      {
        onSuccess: () => {
          toast.success(`"${name}" updated.`)
          setOpen(false)
        },
        onError: (err) =>
          toast.error(
            (err as { response?: { data?: { title?: string } } })?.response?.data?.title
              ?? "Couldn't update that plan.",
          ),
      },
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="ghost" className="press h-7 shrink-0 px-2 text-xs">
          <Pencil className="size-3.5" aria-hidden />
          Edit
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit {plan.name}</DialogTitle>
          <DialogDescription>
            A new price applies to the next signup or renewal. Members already on this plan keep the
            price they paid.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={submit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="plan-name">Name</Label>
            <Input id="plan-name" required value={name} onChange={(e) => setName(e.target.value)} />
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-1.5">
              {/* The plan's own currency — never a hardcoded symbol, and it is not editable here
                  because it belongs to the branch that sells the plan. */}
              <Label htmlFor="plan-price">Price ({plan.currency})</Label>
              <Input
                id="plan-price" type="number" min={0} step="0.01" required
                value={price} onChange={(e) => setPrice(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="plan-freeze">Freeze allowance (days)</Label>
              <Input
                id="plan-freeze" type="number" min={0} step="1" required
                value={maxFreezeDays} onChange={(e) => setMaxFreezeDays(e.target.value)}
              />
            </div>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="plan-description">Description (optional)</Label>
            <Textarea
              id="plan-description" rows={2}
              value={description} onChange={(e) => setDescription(e.target.value)}
            />
          </div>

          {/*
            Retiring, spelled out. "Inactive" alone reads like a display state; what it actually does
            is stop the plan being sellable while every existing membership on it carries on.
          */}
          <label className="flex items-start gap-2 rounded-xl border p-3 text-sm">
            <input
              type="checkbox"
              className="mt-0.5"
              checked={!isActive}
              onChange={(e) => setIsActive(!e.target.checked)}
            />
            <span>
              <span className="font-medium">Retire this plan</span>
              <span className="mt-0.5 block text-xs text-muted-foreground">
                It stops appearing as something new members can buy. Everyone currently on it keeps
                their membership until it ends.
              </span>
            </span>
          </label>

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={() => setOpen(false)}>Cancel</Button>
            <Button type="submit" className="press" disabled={update.isPending}>
              {update.isPending && <Loader2 className="size-4 animate-spin" />}
              Save changes
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
