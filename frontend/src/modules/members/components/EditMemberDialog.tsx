import { useState } from 'react'
import { Loader2, Pencil } from 'lucide-react'
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
import { useUpdateMember, type MemberDetail } from '@/modules/members/api/membersApi'

export function EditMemberDialog({ member }: { member: MemberDetail }) {
  const [open, setOpen] = useState(false)
  const [firstName, setFirstName] = useState(member.firstName)
  const [lastName, setLastName] = useState(member.lastName)
  const [email, setEmail] = useState(member.email)
  const [phone, setPhone] = useState(member.phone ?? '')
  const [dateOfBirth, setDateOfBirth] = useState(member.dateOfBirth?.slice(0, 10) ?? '')
  const [gender, setGender] = useState(member.gender ?? '')
  const [address, setAddress] = useState(member.address ?? '')
  const [profilePhotoUrl, setProfilePhotoUrl] = useState(member.profilePhotoUrl ?? '')

  const updateMember = useUpdateMember(member.id)

  const reset = () => {
    setFirstName(member.firstName)
    setLastName(member.lastName)
    setEmail(member.email)
    setPhone(member.phone ?? '')
    setDateOfBirth(member.dateOfBirth?.slice(0, 10) ?? '')
    setGender(member.gender ?? '')
    setAddress(member.address ?? '')
    setProfilePhotoUrl(member.profilePhotoUrl ?? '')
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    updateMember.mutate(
      {
        firstName,
        lastName,
        email,
        phone: phone || null,
        dateOfBirth: dateOfBirth || null,
        gender: gender || null,
        address: address || null,
        profilePhotoUrl: profilePhotoUrl || null,
      },
      {
        onSuccess: () => {
          toast.success('Member profile updated.')
          setOpen(false)
        },
        onError: () => toast.error('Could not update member profile.'),
      }
    )
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) {
          reset()
        }
      }}
    >
      <DialogTrigger asChild>
        <Button variant="outline" size="sm">
          <Pencil />
          Edit
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit member profile</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="editFirstName">First name</Label>
              <Input id="editFirstName" required value={firstName} onChange={(e) => setFirstName(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="editLastName">Last name</Label>
              <Input id="editLastName" required value={lastName} onChange={(e) => setLastName(e.target.value)} />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="editEmail">Email</Label>
            <Input id="editEmail" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="editPhone">Phone</Label>
              <Input id="editPhone" value={phone} onChange={(e) => setPhone(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="editDob">Date of birth</Label>
              <Input id="editDob" type="date" value={dateOfBirth} onChange={(e) => setDateOfBirth(e.target.value)} />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="editGender">Gender</Label>
              <Input id="editGender" value={gender} onChange={(e) => setGender(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="editAddress">Address</Label>
              <Input id="editAddress" value={address} onChange={(e) => setAddress(e.target.value)} />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="editPhotoUrl">Profile photo URL</Label>
            <Input id="editPhotoUrl" value={profilePhotoUrl} onChange={(e) => setProfilePhotoUrl(e.target.value)} />
          </div>
          <DialogFooter>
            <Button type="submit" disabled={updateMember.isPending}>
              {updateMember.isPending && <Loader2 className="size-4 animate-spin" />}
              Save changes
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
