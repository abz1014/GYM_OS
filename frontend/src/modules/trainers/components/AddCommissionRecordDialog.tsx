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
import { useAddCommissionRecord } from '@/modules/trainers/api/trainersApi'

export function AddCommissionRecordDialog({ trainerId }: { trainerId: string }) {
  const [open, setOpen] = useState(false)
  const [amount, setAmount] = useState('')
  const [period, setPeriod] = useState('')

  const addCommission = useAddCommissionRecord(trainerId)

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!period) {
      toast.error('Select a period.')
      return
    }

    addCommission.mutate(
      { amount: Number(amount), period: `${period}-01` },
      {
        onSuccess: () => {
          toast.success('Commission record added.')
          setOpen(false)
          setAmount('')
          setPeriod('')
        },
        onError: () => toast.error('Could not add commission record.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline" className="rounded-xl">
          <Plus />
          Record Commission
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Record commission</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="period">Period</Label>
            <Input id="period" type="month" required value={period} onChange={(e) => setPeriod(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="amount">Amount</Label>
            <Input
              id="amount"
              type="number"
              min={0}
              step="0.01"
              required
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button type="submit" disabled={addCommission.isPending}>
              {addCommission.isPending && <Loader2 className="size-4 animate-spin" />}
              Record
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
