import { useState } from 'react'
import { Loader2, UserPlus } from 'lucide-react'
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
import { useCreateTrainer } from '@/modules/trainers/api/trainersApi'
import { useUiStore } from '@/stores/uiStore'

export function CreateTrainerDialog() {
  const [open, setOpen] = useState(false)
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [email, setEmail] = useState('')
  const [specialties, setSpecialties] = useState('')
  const [commissionRate, setCommissionRate] = useState(10)

  const branchId = useUiStore((s) => s.selectedBranchId)
  const createTrainer = useCreateTrainer()

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!branchId) {
      toast.error('Select a branch first.')
      return
    }

    createTrainer.mutate(
      { firstName, lastName, email, branchId, specialties, commissionRate },
      {
        onSuccess: (result) => {
          toast.success(`Trainer added. Temporary password: ${result.temporaryPassword}`, { duration: 15000 });
          setOpen(false)
          setFirstName('')
          setLastName('')
          setEmail('')
          setSpecialties('')
        },
        onError: () => toast.error('Could not add trainer.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <UserPlus />
          Add Trainer
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add a trainer</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="firstName">First name</Label>
              <Input id="firstName" required value={firstName} onChange={(e) => setFirstName(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="lastName">Last name</Label>
              <Input id="lastName" required value={lastName} onChange={(e) => setLastName(e.target.value)} />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="email">Email</Label>
            <Input id="email" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="specialties">Specialties</Label>
            <Input
              id="specialties"
              placeholder="Strength Training, Yoga"
              required
              value={specialties}
              onChange={(e) => setSpecialties(e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="commissionRate">Commission rate (%)</Label>
            <Input
              id="commissionRate"
              type="number"
              min={0}
              max={100}
              step="0.1"
              required
              value={commissionRate}
              onChange={(e) => setCommissionRate(Number(e.target.value))}
            />
          </div>
          <DialogFooter>
            <Button type="submit" disabled={createTrainer.isPending}>
              {createTrainer.isPending && <Loader2 className="size-4 animate-spin" />}
              Add Trainer
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
