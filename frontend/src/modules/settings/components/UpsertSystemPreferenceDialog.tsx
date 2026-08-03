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
import { useUpsertSystemPreference, type SystemPreference } from '@/modules/settings/api/settingsApi'

export function UpsertSystemPreferenceDialog({ existing }: { existing?: SystemPreference }) {
  const [open, setOpen] = useState(false)
  const [key, setKey] = useState(existing?.key ?? '')
  const [value, setValue] = useState(existing?.value ?? '')
  const [description, setDescription] = useState(existing?.description ?? '')

  const upsertPreference = useUpsertSystemPreference()

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    upsertPreference.mutate(
      { key, value, description: description || undefined },
      {
        onSuccess: () => {
          toast.success(existing ? 'Preference updated.' : 'Preference added.')
          setOpen(false)
          if (!existing) {
            setKey('')
            setValue('')
            setDescription('')
          }
        },
        onError: () => toast.error('Could not save preference.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        {existing ? (
          <Button size="sm" variant="outline">
            Edit
          </Button>
        ) : (
          <Button size="sm">
            <Plus />
            New Preference
          </Button>
        )}
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{existing ? 'Edit preference' : 'Add preference'}</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="prefKey">Key</Label>
            <Input id="prefKey" required disabled={!!existing} value={key} onChange={(e) => setKey(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="prefValue">Value</Label>
            <Input id="prefValue" required value={value} onChange={(e) => setValue(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="prefDescription">Description (optional)</Label>
            <Input id="prefDescription" value={description} onChange={(e) => setDescription(e.target.value)} />
          </div>
          <DialogFooter>
            <Button type="submit" disabled={upsertPreference.isPending}>
              {upsertPreference.isPending && <Loader2 className="size-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
