import { useState } from 'react'
import { HeartPulse, Loader2, Pencil, Phone, Plus, Stethoscope, Trash2, UserRound } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import type { MemberDetail } from '@/modules/members/api/membersApi'
import {
  serverReason,
  useAddMyEmergencyContact,
  useDeleteMyEmergencyContact,
  useMyProfile,
  useUpdateMyEmergencyContact,
  useUpdateMyProfile,
  type MyEmergencyContactInput,
} from '@/modules/portal/api/portalApi'
import { MemberLoadError, dateFormat } from '@/modules/portal/components/portalShared'
import { isStale } from '@/shared/lib/queryTrust'

type EmergencyContact = MemberDetail['emergencyContacts'][number]

/**
 * The number the gym rings.
 *
 * Held on the member's record since they joined, editable by staff, and invisible to the member —
 * so a phone number changed two years ago stayed wrong until someone tried to use it, which is
 * always the moment it matters (a cancelled class, an injury, an expiring card).
 */
function PhoneCard({ phone }: { phone: string | null }) {
  const [editing, setEditing] = useState(false)
  const [value, setValue] = useState(phone ?? '')
  const update = useUpdateMyProfile()

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    update.mutate(
      { phone: value.trim() },
      {
        onSuccess: () => {
          toast.success('Phone number updated.')
          setEditing(false)
        },
        onError: (err) => toast.error(serverReason(err, 'Could not update your phone number.')),
      },
    )
  }

  if (!editing) {
    return (
      <div className="flex items-center justify-between gap-3">
        <div className="min-w-0">
          <p className="flex items-center gap-1.5 text-sm font-medium">
            <Phone className="size-3.5 shrink-0 text-muted-foreground" />
            Phone
          </p>
          {/* No number on file is a real state and says so — an empty line reads as a loading bug. */}
          <p className="mt-0.5 truncate text-sm text-muted-foreground">{phone || 'No number on file.'}</p>
        </div>
        <Button
          variant="outline"
          className="h-11 shrink-0 rounded-xl"
          onClick={() => {
            setValue(phone ?? '')
            setEditing(true)
          }}
        >
          {phone ? 'Change' : 'Add'}
        </Button>
      </div>
    )
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-3">
      <div className="space-y-1.5">
        <Label htmlFor="myPhone">Phone</Label>
        <Input
          id="myPhone"
          type="tel"
          required
          className="h-11"
          value={value}
          onChange={(e) => setValue(e.target.value)}
        />
      </div>
      <div className="flex gap-2">
        <Button type="submit" className="h-11 rounded-xl" disabled={update.isPending}>
          {update.isPending && <Loader2 className="size-4 animate-spin" />}
          Save
        </Button>
        <Button type="button" variant="ghost" className="h-11 rounded-xl" onClick={() => setEditing(false)}>
          Cancel
        </Button>
      </div>
    </form>
  )
}

/** One dialog for both adding and editing — the fields and the rules are identical, and two copies
 *  of the same form is how they drift apart. `existing` is null when adding. */
function EmergencyContactDialog({
  existing,
  open,
  onOpenChange,
}: {
  existing: EmergencyContact | null
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const [form, setForm] = useState<MyEmergencyContactInput>({
    name: existing?.name ?? '',
    phone: existing?.phone ?? '',
    relationship: existing?.relationship ?? '',
  })

  const add = useAddMyEmergencyContact()
  const update = useUpdateMyEmergencyContact()
  const pending = add.isPending || update.isPending

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    const payload: MyEmergencyContactInput = {
      name: form.name.trim(),
      phone: form.phone.trim(),
      relationship: form.relationship.trim(),
    }
    const done = {
      onSuccess: () => {
        toast.success(existing ? 'Contact updated.' : 'Contact added.')
        onOpenChange(false)
      },
      onError: (err: unknown) => toast.error(serverReason(err, 'Could not save this contact.')),
    }

    if (existing) update.mutate({ id: existing.id, ...payload }, done)
    else add.mutate(payload, done)
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{existing ? 'Edit contact' : 'Add an emergency contact'}</DialogTitle>
          <DialogDescription>
            Who the gym should call if something happens to you while you're here.
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="contactName">Name</Label>
            <Input
              id="contactName"
              required
              className="h-11"
              value={form.name}
              onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="contactPhone">Phone</Label>
            <Input
              id="contactPhone"
              type="tel"
              required
              className="h-11"
              value={form.phone}
              onChange={(e) => setForm((f) => ({ ...f, phone: e.target.value }))}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="contactRelationship">Relationship</Label>
            <Input
              id="contactRelationship"
              required
              className="h-11"
              placeholder="Partner, parent, friend…"
              value={form.relationship}
              onChange={(e) => setForm((f) => ({ ...f, relationship: e.target.value }))}
            />
          </div>
          <DialogFooter>
            <Button type="submit" className="h-11 w-full rounded-xl sm:w-auto" disabled={pending}>
              {pending && <Loader2 className="size-4 animate-spin" />}
              {existing ? 'Save changes' : 'Add contact'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function EmergencyContactRow({ contact }: { contact: EmergencyContact }) {
  const [editing, setEditing] = useState(false)
  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const remove = useDeleteMyEmergencyContact()

  const handleDelete = () =>
    remove.mutate(contact.id, {
      onSuccess: () => {
        toast.success('Contact removed.')
        setConfirmingDelete(false)
      },
      onError: (err) => toast.error(serverReason(err, 'Could not remove this contact.')),
    })

  return (
    <li className="flex items-start justify-between gap-2 border-b py-3 last:border-0">
      <div className="min-w-0">
        <p className="truncate text-sm font-medium">{contact.name}</p>
        <p className="truncate text-xs text-muted-foreground">
          {/* The email is shown because it is on the record and a member should be able to see what
              the gym holds — it is absent from the edit form because the API takes only name, phone
              and relationship, and offering a field that silently discards what is typed into it is
              worse than not offering it. */}
          {[contact.relationship, contact.phone, contact.email].filter(Boolean).join(' · ')}
        </p>
      </div>
      <div className="flex shrink-0 items-center">
        <Button variant="ghost" size="icon" className="size-11" aria-label={`Edit ${contact.name}`} onClick={() => setEditing(true)}>
          <Pencil className="size-4" />
        </Button>
        <Button
          variant="ghost"
          size="icon"
          className="size-11 text-muted-foreground"
          aria-label={`Remove ${contact.name}`}
          onClick={() => setConfirmingDelete(true)}
        >
          <Trash2 className="size-4" />
        </Button>
      </div>

      {/* Mounted only while open, so the form is built fresh from the current row each time rather
          than holding whatever was typed into a previous, abandoned edit. */}
      {editing && <EmergencyContactDialog existing={contact} open onOpenChange={setEditing} />}

      <Dialog open={confirmingDelete} onOpenChange={setConfirmingDelete}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Remove {contact.name}?</DialogTitle>
            <DialogDescription>
              The gym will no longer have them as a contact for you. You can add them again at any
              time.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter className="gap-2">
            <Button variant="outline" className="h-11 rounded-xl" onClick={() => setConfirmingDelete(false)}>
              Keep
            </Button>
            <Button variant="destructive" className="h-11 rounded-xl" disabled={remove.isPending} onClick={handleDelete}>
              {remove.isPending && <Loader2 className="size-4 animate-spin" />}
              Remove
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </li>
  )
}

function EmergencyContactsCard({ contacts }: { contacts: EmergencyContact[] }) {
  const [adding, setAdding] = useState(false)

  return (
    <div className="space-y-3">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="flex items-center gap-1.5 text-sm font-medium">
            <HeartPulse className="size-3.5 shrink-0 text-muted-foreground" />
            Emergency contacts
          </h2>
          <p className="text-sm text-muted-foreground">Who we call if something happens while you're training.</p>
        </div>
        <Button variant="outline" className="h-11 shrink-0 rounded-xl" onClick={() => setAdding(true)}>
          <Plus className="size-4" />
          Add
        </Button>
      </div>

      {contacts.length > 0 ? (
        <ul>
          {contacts.map((c) => (
            <EmergencyContactRow key={c.id} contact={c} />
          ))}
        </ul>
      ) : (
        <p className="py-2 text-sm text-muted-foreground">
          Nobody on file. If you're hurt at the gym, staff have no one to call.
        </p>
      )}

      {adding && <EmergencyContactDialog existing={null} open onOpenChange={setAdding} />}
    </div>
  )
}

/**
 * Read-only, and that is the whole point.
 *
 * These notes are written by staff — an injury declared at sign-up, a condition a trainer was told
 * about — and they are the member's own health information, held about them somewhere they could
 * not see it. Showing them is not the same as letting them be edited: a member silently deleting a
 * note a trainer relies on would be a safety regression, and the endpoints to change them are
 * staff-side for that reason. Anything wrong here is corrected by talking to the gym.
 */
function MedicalNotesCard({ notes }: { notes: MemberDetail['medicalNotes'] }) {
  return (
    <div className="space-y-3">
      <div>
        <h2 className="flex items-center gap-1.5 text-sm font-medium">
          <Stethoscope className="size-3.5 shrink-0 text-muted-foreground" />
          Medical notes on file
        </h2>
        <p className="text-sm text-muted-foreground">
          What your gym has recorded about your health. Ask a member of staff to change any of it.
        </p>
      </div>
      {notes.length > 0 ? (
        <ul className="space-y-2">
          {notes.map((n) => (
            <li key={n.id} className="rounded-xl border p-3">
              <p className="text-sm">{n.note}</p>
              <p className="mt-1 text-xs text-muted-foreground">
                Recorded {dateFormat.format(new Date(n.recordedAt))}
              </p>
            </li>
          ))}
        </ul>
      ) : (
        <p className="py-2 text-sm text-muted-foreground">None on file.</p>
      )}
    </div>
  )
}

/**
 * The member's own record: the number the gym rings, the people it rings instead, and what it has
 * written down about their health.
 *
 * Every field here already existed on the member record and was reachable only by staff, which made
 * the two safety-critical ones — the emergency contact and the phone number — the fields a member
 * could neither check nor correct. Rendered above the password card on the account screen because a
 * wrong emergency contact is a bigger problem than a weak password.
 */
export function MemberProfileSection() {
  const profile = useMyProfile()

  if (profile.isLoading) {
    return <Skeleton className="h-64 w-full" />
  }

  if (isStale(profile)) {
    return (
      <Card>
        <CardContent>
          <MemberLoadError
            title="We couldn't load your profile"
            hint="Your details are safe — we just can't reach the gym right now."
            onRetry={() => void profile.refetch()}
            isRetrying={profile.isFetching}
          />
        </CardContent>
      </Card>
    )
  }

  if (!profile.data) return null

  return (
    <Card>
      <CardContent className="space-y-6">
        <div className="flex items-center gap-2">
          <UserRound className="size-4 text-muted-foreground" />
          <h2 className="text-sm font-medium">Profile</h2>
        </div>
        <PhoneCard phone={profile.data.phone} />
        <EmergencyContactsCard contacts={profile.data.emergencyContacts} />
        <MedicalNotesCard notes={profile.data.medicalNotes} />
      </CardContent>
    </Card>
  )
}
