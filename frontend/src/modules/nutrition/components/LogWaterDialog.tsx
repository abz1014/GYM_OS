import { useState } from 'react'
import { Droplet, Loader2 } from 'lucide-react'
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
import { useLogWater } from '@/modules/nutrition/api/nutritionApi'

export function LogWaterDialog({ memberId }: { memberId: string }) {
  const [open, setOpen] = useState(false)
  const [amountMl, setAmountMl] = useState('250')

  const logWater = useLogWater()

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    logWater.mutate(
      { memberId, amountMl: Number(amountMl) },
      {
        onSuccess: () => {
          toast.success('Water logged.')
          setOpen(false)
          setAmountMl('250')
        },
        onError: () => toast.error('Could not log water.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          <Droplet />
          Log Water
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Log water intake</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="amountMl">Amount (ml)</Label>
            <Input
              id="amountMl"
              type="number"
              min={1}
              required
              value={amountMl}
              onChange={(e) => setAmountMl(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button type="submit" disabled={logWater.isPending}>
              {logWater.isPending && <Loader2 className="size-4 animate-spin" />}
              Log
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
