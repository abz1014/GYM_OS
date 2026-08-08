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
import { useMembersList } from '@/modules/members/api/membersApi'
import { useCreateInvoice } from '@/modules/billing/api/billingApi'
import { useUiStore } from '@/stores/uiStore'

export function CreateInvoiceDialog() {
  const [open, setOpen] = useState(false)
  const [memberSearch, setMemberSearch] = useState('')
  const [memberId, setMemberId] = useState<string | null>(null)
  const [memberName, setMemberName] = useState('')
  const [description, setDescription] = useState('Membership fee')
  const [amount, setAmount] = useState(0)

  const branchId = useUiStore((s) => s.selectedBranchId)
  const { data: members } = useMembersList({ searchTerm: memberSearch || undefined, page: 1, pageSize: 5 })
  const createInvoice = useCreateInvoice()

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!memberId || !branchId) {
      toast.error('Select a member first.')
      return
    }

    const today = new Date().toISOString().slice(0, 10)
    const dueDate = new Date(Date.now() + 7 * 86_400_000).toISOString().slice(0, 10)

    createInvoice.mutate(
      {
        memberId,
        branchId,
        issueDate: today,
        dueDate,
        taxAmount: 0,
        discountAmount: 0,
        currency: 'USD',
        lines: [{ itemType: 'MembershipFee', description, quantity: 1, unitPrice: amount }],
      },
      {
        onSuccess: () => {
          toast.success('Invoice created.')
          setOpen(false)
          setMemberId(null)
          setMemberSearch('')
        },
        onError: () => toast.error('Could not create invoice.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <Plus />
          New Invoice
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create invoice</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label>Member</Label>
            {memberId ? (
              <div className="flex items-center justify-between rounded-xl border border-border px-3 py-2 text-sm">
                {memberName}
                <button type="button" className="text-xs text-muted-foreground hover:underline" onClick={() => setMemberId(null)}>
                  Change
                </button>
              </div>
            ) : (
              <>
                <Input placeholder="Search member..." value={memberSearch} onChange={(e) => setMemberSearch(e.target.value)} />
                {memberSearch && (
                  <div className="divide-y divide-border rounded-xl border border-border">
                    {members?.items.map((m) => (
                      <button
                        key={m.id}
                        type="button"
                        className="flex w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-accent"
                        onClick={() => {
                          setMemberId(m.id)
                          setMemberName(m.fullName)
                        }}
                      >
                        {m.fullName}
                        <span className="text-xs text-muted-foreground">{m.memberCode}</span>
                      </button>
                    ))}
                  </div>
                )}
              </>
            )}
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="description">Line description</Label>
            <Input id="description" required value={description} onChange={(e) => setDescription(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="amount">Amount (USD)</Label>
            <Input
              id="amount"
              type="number"
              min={0}
              step="0.01"
              required
              value={amount}
              onChange={(e) => setAmount(Number(e.target.value))}
            />
          </div>
          <DialogFooter>
            <Button type="submit" disabled={createInvoice.isPending}>
              {createInvoice.isPending && <Loader2 className="size-4 animate-spin" />}
              Create
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
