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
import { useAddEmergencyContact } from '@/modules/members/api/membersApi'

export function AddEmergencyContactDialog({ memberId }: { memberId: string }) {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [relationship, setRelationship] = useState('')
  const [phone, setPhone] = useState('')
  const [email, setEmail] = useState('')

  const addContact = useAddEmergencyContact(memberId)

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    addContact.mutate(
      { name, relationship, phone, email: email || undefined },
      {
        onSuccess: () => {
          toast.success('Emergency contact added.')
          setOpen(false)
          setName('')
          setRelationship('')
          setPhone('')
          setEmail('')
        },
        onError: () => toast.error('Could not add emergency contact.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          <Plus />
          Add Contact
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add emergency contact</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="contactName">Name</Label>
            <Input id="contactName" required value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="contactRelationship">Relationship</Label>
            <Input
              id="contactRelationship"
              required
              placeholder="Spouse, Parent, Sibling..."
              value={relationship}
              onChange={(e) => setRelationship(e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="contactPhone">Phone</Label>
            <Input id="contactPhone" required value={phone} onChange={(e) => setPhone(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="contactEmail">Email (optional)</Label>
            <Input id="contactEmail" type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
          </div>
          <DialogFooter>
            <Button type="submit" disabled={addContact.isPending}>
              {addContact.isPending && <Loader2 className="size-4 animate-spin" />}
              Add
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
