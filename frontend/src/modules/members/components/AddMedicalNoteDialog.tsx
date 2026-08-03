import { useState } from 'react'
import { Loader2, Plus } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
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
import { useAddMedicalNote } from '@/modules/members/api/membersApi'
import { useAuthStore } from '@/stores/authStore'

export function AddMedicalNoteDialog({ memberId }: { memberId: string }) {
  const [open, setOpen] = useState(false)
  const [note, setNote] = useState('')

  const currentUserId = useAuthStore((s) => s.user?.id)
  const addNote = useAddMedicalNote(memberId)

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    addNote.mutate(
      { note, recordedByUserId: currentUserId ?? null },
      {
        onSuccess: () => {
          toast.success('Medical note added.')
          setOpen(false)
          setNote('')
        },
        onError: () => toast.error('Could not add medical note.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          <Plus />
          Add Note
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add medical note</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="medicalNote">Note</Label>
            <Textarea id="medicalNote" required rows={4} value={note} onChange={(e) => setNote(e.target.value)} />
          </div>
          <DialogFooter>
            <Button type="submit" disabled={addNote.isPending}>
              {addNote.isPending && <Loader2 className="size-4 animate-spin" />}
              Add
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
