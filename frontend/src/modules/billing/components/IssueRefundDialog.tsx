import { useState } from 'react'
import { Loader2, Undo2 } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
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
import { useIssueRefund } from '@/modules/billing/api/billingApi'

export function IssueRefundDialog({
  invoiceId,
  paymentId,
  maxAmount,
}: {
  invoiceId: string
  paymentId: string
  maxAmount: number
}) {
  const [open, setOpen] = useState(false)
  const [amount, setAmount] = useState(maxAmount)
  const [reason, setReason] = useState('')

  const issueRefund = useIssueRefund(invoiceId, paymentId)

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!reason.trim()) {
      toast.error('A reason is required.')
      return
    }

    issueRefund.mutate(
      { amount, reason },
      {
        onSuccess: () => {
          toast.success('Refund issued.')
          setOpen(false)
          setReason('')
        },
        onError: (err: unknown) =>
          toast.error((err as { response?: { data?: { title?: string } } })?.response?.data?.title ?? 'Could not issue refund.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          <Undo2 />
          Refund
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Issue refund</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="refundAmount">Amount</Label>
            <Input
              id="refundAmount"
              type="number"
              min={0}
              max={maxAmount}
              step="0.01"
              required
              value={amount}
              onChange={(e) => setAmount(Number(e.target.value))}
            />
            <p className="text-xs text-muted-foreground">Up to the original payment amount ({maxAmount}).</p>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="refundReason">Reason</Label>
            <Textarea id="refundReason" required value={reason} onChange={(e) => setReason(e.target.value)} />
          </div>
          <DialogFooter>
            <Button type="submit" variant="destructive" disabled={issueRefund.isPending}>
              {issueRefund.isPending && <Loader2 className="size-4 animate-spin" />}
              Issue Refund
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
