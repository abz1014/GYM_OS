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
import { useAddProgressPhoto } from '@/modules/members/api/membersApi'

export function AddProgressPhotoDialog({ memberId }: { memberId: string }) {
  const [open, setOpen] = useState(false)
  const [photoUrl, setPhotoUrl] = useState('')
  const [notes, setNotes] = useState('')

  const addPhoto = useAddProgressPhoto(memberId)

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    addPhoto.mutate(
      { photoUrl, notes: notes || undefined },
      {
        onSuccess: () => {
          toast.success('Progress photo added.')
          setOpen(false)
          setPhotoUrl('')
          setNotes('')
        },
        onError: () => toast.error('Could not add progress photo.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          <Plus />
          Add Photo
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add progress photo</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="photoUrl">Photo URL</Label>
            <Input
              id="photoUrl"
              type="url"
              required
              placeholder="https://..."
              value={photoUrl}
              onChange={(e) => setPhotoUrl(e.target.value)}
            />
            <p className="text-xs text-muted-foreground">
              Paste a link to an already-hosted image — direct file upload isn't wired up yet.
            </p>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="photoNotes">Notes (optional)</Label>
            <Input id="photoNotes" value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>
          <DialogFooter>
            <Button type="submit" disabled={addPhoto.isPending}>
              {addPhoto.isPending && <Loader2 className="size-4 animate-spin" />}
              Add
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
