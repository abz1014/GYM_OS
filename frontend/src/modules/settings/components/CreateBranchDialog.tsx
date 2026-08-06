import { useState } from 'react'
import { Building2, Loader2 } from 'lucide-react'
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
import { MAX_BRANCH_CAPACITY, parseCapacity, useCreateBranch } from '@/modules/settings/api/settingsApi'

export function CreateBranchDialog() {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [addressLine, setAddressLine] = useState('')
  const [city, setCity] = useState('')
  const [country, setCountry] = useState('')
  const [timeZone, setTimeZone] = useState('UTC')
  const [currency, setCurrency] = useState('USD')
  const [capacity, setCapacity] = useState('')

  const createBranch = useCreateBranch()

  const reset = () => {
    setName('')
    setAddressLine('')
    setCity('')
    setCountry('')
    setCapacity('')
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    createBranch.mutate(
      { name, addressLine, city, country, timeZone, currency, capacity: parseCapacity(capacity) },
      {
        onSuccess: () => {
          toast.success('Branch created.')
          setOpen(false)
          reset()
        },
        onError: () => toast.error('Could not create branch.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm">
          <Building2 />
          New Branch
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create branch</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="branchName">Name</Label>
            <Input id="branchName" required value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="branchAddress">Address</Label>
            <Input id="branchAddress" required value={addressLine} onChange={(e) => setAddressLine(e.target.value)} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="branchCity">City</Label>
              <Input id="branchCity" required value={city} onChange={(e) => setCity(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="branchCountry">Country</Label>
              <Input id="branchCountry" required value={country} onChange={(e) => setCountry(e.target.value)} />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="branchTimeZone">Time zone</Label>
              <Input id="branchTimeZone" required value={timeZone} onChange={(e) => setTimeZone(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="branchCurrency">Currency</Label>
              <Input
                id="branchCurrency"
                required
                value={currency}
                onChange={(e) => setCurrency(e.target.value.toUpperCase())}
              />
            </div>
          </div>
          {/*
            Optional, and left genuinely blank rather than pre-filled with a plausible number. A
            capacity is something the gym reads off its fire-safety certificate, and the occupancy
            bar on the front desk divides by it — a guessed default would be wrong on every reading
            without ever looking wrong. Empty means "not told", and the desk shows a bare count.
          */}
          <div className="space-y-1.5">
            <Label htmlFor="branchCapacity">Capacity <span className="font-normal text-muted-foreground">(optional)</span></Label>
            <Input
              id="branchCapacity"
              type="number"
              min={1}
              max={MAX_BRANCH_CAPACITY}
              inputMode="numeric"
              placeholder="Not set"
              value={capacity}
              onChange={(e) => setCapacity(e.target.value)}
            />
            <p className="text-xs text-muted-foreground">How many people the site holds. Leave blank if you don't have the figure.</p>
          </div>
          <DialogFooter>
            <Button type="submit" disabled={createBranch.isPending}>
              {createBranch.isPending && <Loader2 className="size-4 animate-spin" />}
              Create
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
