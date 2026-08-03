import { useState } from 'react'
import { Loader2, Pencil } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
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
import { useUpdateBranch, type Branch } from '@/modules/settings/api/settingsApi'

export function EditBranchDialog({ branch }: { branch: Branch }) {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState(branch.name)
  const [addressLine, setAddressLine] = useState(branch.addressLine)
  const [city, setCity] = useState(branch.city)
  const [country, setCountry] = useState(branch.country)
  const [timeZone, setTimeZone] = useState(branch.timeZone)
  const [currency, setCurrency] = useState(branch.currency)
  const [isActive, setIsActive] = useState(branch.isActive)

  const updateBranch = useUpdateBranch()

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    updateBranch.mutate(
      { id: branch.id, name, addressLine, city, country, timeZone, currency, isActive },
      {
        onSuccess: () => {
          toast.success('Branch updated.')
          setOpen(false)
        },
        onError: () => toast.error('Could not update branch.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="icon" variant="outline" className="size-7">
          <Pencil className="size-3.5" />
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit branch</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="editBranchName">Name</Label>
            <Input id="editBranchName" required value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="editBranchAddress">Address</Label>
            <Input id="editBranchAddress" required value={addressLine} onChange={(e) => setAddressLine(e.target.value)} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="editBranchCity">City</Label>
              <Input id="editBranchCity" required value={city} onChange={(e) => setCity(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="editBranchCountry">Country</Label>
              <Input id="editBranchCountry" required value={country} onChange={(e) => setCountry(e.target.value)} />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="editBranchTimeZone">Time zone</Label>
              <Input id="editBranchTimeZone" required value={timeZone} onChange={(e) => setTimeZone(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="editBranchCurrency">Currency</Label>
              <Input
                id="editBranchCurrency"
                required
                value={currency}
                onChange={(e) => setCurrency(e.target.value.toUpperCase())}
              />
            </div>
          </div>
          <div className="flex items-center gap-2">
            <Checkbox id="branchIsActive" checked={isActive} onCheckedChange={(v) => setIsActive(v === true)} />
            <Label htmlFor="branchIsActive" className="font-normal">
              Active
            </Label>
          </div>
          <DialogFooter>
            <Button type="submit" disabled={updateBranch.isPending}>
              {updateBranch.isPending && <Loader2 className="size-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
