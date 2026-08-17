import { useState } from 'react'
import { KeyRound, Loader2, UserPlus } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useBranchesQuery } from '@/shared/hooks/useBranches'
import { useAuthStore } from '@/stores/authStore'
import {
  useCreateStaff, useResetStaffPassword, useSetStaffActive, useStaffDirectory, useUpdateStaff,
  type StaffMember, type StaffRole,
} from '@/modules/settings/api/staffApi'

/** The server's reason, never a guess — the refusals here are rules, and staff need to read them. */
function serverReason(err: unknown, fallback: string): string {
  return (err as { response?: { data?: { title?: string } } })?.response?.data?.title ?? fallback
}

/**
 * A temporary password exists exactly once, in this response. Fifteen seconds is the convention the
 * trainer dialog already set; a copy button matters more than the duration, because reading a
 * generated password off a screen and retyping it is where it gets mistranscribed.
 */
function announceTemporaryPassword(password: string, who: string) {
  toast.success(`${who} — temporary password: ${password}`, {
    duration: 20000,
    action: {
      label: 'Copy',
      onClick: () => void navigator.clipboard?.writeText(password),
    },
  })
}

function StaffFormDialog({
  open, onOpenChange, roles, editing,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  roles: StaffRole[]
  /** Null when hiring, the row when editing — the email is immutable, so it only shows on create. */
  editing: StaffMember | null
}) {
  const branches = useBranchesQuery()
  const create = useCreateStaff()
  const update = useUpdateStaff()

  const [email, setEmail] = useState('')
  const [firstName, setFirstName] = useState(editing?.firstName ?? '')
  const [lastName, setLastName] = useState(editing?.lastName ?? '')
  const [phone, setPhone] = useState(editing?.phone ?? '')
  const [roleName, setRoleName] = useState(editing?.roleName ?? '')
  const [branchIds, setBranchIds] = useState<string[]>(editing?.branchIds ?? [])

  const pending = create.isPending || update.isPending

  const submit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!roleName) {
      toast.error('Pick a role.')
      return
    }
    if (branchIds.length === 0) {
      // Not a nicety: branch access is what every scoped query filters on, so an account with none
      // can sign in and then find every screen empty. Better to refuse than to hand someone that.
      toast.error('Give them access to at least one branch, or they will see nothing.')
      return
    }

    const payload = { firstName, lastName, phone: phone.trim() || null, roleName, branchIds }

    if (editing) {
      update.mutate({ id: editing.id, ...payload }, {
        onSuccess: () => {
          toast.success('Staff member updated.')
          onOpenChange(false)
        },
        onError: (err) => toast.error(serverReason(err, "Couldn't update that staff member.")),
      })
      return
    }

    create.mutate({ email: email.trim(), ...payload }, {
      onSuccess: (result) => {
        announceTemporaryPassword(result.temporaryPassword, `${firstName} ${lastName} added`)
        onOpenChange(false)
      },
      onError: (err) => toast.error(serverReason(err, "Couldn't add that staff member.")),
    })
  }

  const toggleBranch = (id: string, checked: boolean) =>
    setBranchIds((ids) => (checked ? [...new Set([...ids, id])] : ids.filter((b) => b !== id)))

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{editing ? `Edit ${editing.firstName} ${editing.lastName}` : 'Add a staff member'}</DialogTitle>
          <DialogDescription>
            {editing
              ? 'Changes to role or branch access apply the next time they load a screen.'
              : 'They get a temporary password to change on first sign-in. Their email is their username and cannot be changed later.'}
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={submit} className="space-y-4">
          {!editing && (
            <div className="space-y-1.5">
              <Label htmlFor="staff-email">Email</Label>
              <Input
                id="staff-email" type="email" required autoComplete="off"
                value={email} onChange={(e) => setEmail(e.target.value)}
              />
            </div>
          )}

          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="staff-first">First name</Label>
              <Input id="staff-first" required value={firstName} onChange={(e) => setFirstName(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="staff-last">Last name</Label>
              <Input id="staff-last" required value={lastName} onChange={(e) => setLastName(e.target.value)} />
            </div>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="staff-phone">Phone (optional)</Label>
            <Input id="staff-phone" value={phone} onChange={(e) => setPhone(e.target.value)} />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="staff-role">Role</Label>
            <select
              id="staff-role"
              required
              className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
              value={roleName}
              onChange={(e) => setRoleName(e.target.value)}
            >
              <option value="">Select a role…</option>
              {roles.map((r) => (
                <option key={r.id} value={r.name}>{r.name}</option>
              ))}
            </select>
            <p className="text-xs text-muted-foreground">
              What the role can do is set on the Permission Matrix tab.
            </p>
          </div>

          <div className="space-y-1.5">
            <Label>Branch access</Label>
            {branches.isLoading && <Skeleton className="h-16 w-full" />}
            {branches.data?.map((b) => (
              <label key={b.id} className="flex items-center gap-2 py-1 text-sm">
                <Checkbox
                  checked={branchIds.includes(b.id)}
                  onCheckedChange={(checked) => toggleBranch(b.id, checked === true)}
                />
                {b.name}
              </label>
            ))}
          </div>

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={() => onOpenChange(false)}>Cancel</Button>
            <Button type="submit" disabled={pending} className="press">
              {pending && <Loader2 className="size-4 animate-spin" />}
              {editing ? 'Save changes' : 'Add staff member'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function StaffActions({ member, onEdit }: { member: StaffMember; onEdit: () => void }) {
  const setActive = useSetStaffActive()
  const resetPassword = useResetStaffPassword()
  const myUserId = useAuthStore((s) => s.user?.id)

  // Deactivating your own account signs you out of a console you then cannot get back into to undo
  // it — and with no other super-admin screen, that is unrecoverable. Refused at the source too.
  const isSelf = member.id === myUserId

  return (
    <div className="flex flex-wrap items-center gap-1">
      <Button size="sm" variant="ghost" className="press" onClick={onEdit}>Edit</Button>
      <Button
        size="sm"
        variant="ghost"
        className="press"
        disabled={resetPassword.isPending}
        onClick={() =>
          resetPassword.mutate(member.id, {
            onSuccess: (result) =>
              announceTemporaryPassword(result.temporaryPassword, `Password reset for ${member.firstName}`),
            onError: (err) => toast.error(serverReason(err, "Couldn't reset that password.")),
          })
        }
      >
        <KeyRound className="size-4" aria-hidden />
        Reset password
      </Button>
      <Button
        size="sm"
        variant={member.isActive ? 'ghost' : 'outline'}
        className="press"
        disabled={setActive.isPending || isSelf}
        title={isSelf ? 'You cannot deactivate your own account.' : undefined}
        onClick={() =>
          setActive.mutate(
            { id: member.id, isActive: !member.isActive },
            {
              onSuccess: () =>
                toast.success(member.isActive
                  ? `${member.firstName} can no longer sign in.`
                  : `${member.firstName} can sign in again.`),
              onError: (err) => toast.error(serverReason(err, "Couldn't change that account.")),
            },
          )
        }
      >
        {member.isActive ? 'Deactivate' : 'Reactivate'}
      </Button>
    </div>
  )
}

/**
 * Who works here, and what they can reach.
 *
 * THE GAP. Nothing in the product could create, edit or deactivate a staff account: every account
 * that existed had been made by the demo seeder, and hiring a receptionist required a developer.
 * Deactivating is the load-bearing half — LoginCommand already refuses an inactive user, so
 * switching this off is what actually ends someone's access on their last day.
 */
export function StaffTab() {
  const directory = useStaffDirectory()
  const [dialogOpen, setDialogOpen] = useState(false)
  const [editing, setEditing] = useState<StaffMember | null>(null)
  const branches = useBranchesQuery()

  const branchNames = (ids: string[]) =>
    ids
      .map((id) => branches.data?.find((b) => b.id === id)?.name)
      .filter((n): n is string => !!n)

  const openCreate = () => { setEditing(null); setDialogOpen(true) }
  const openEdit = (member: StaffMember) => { setEditing(member); setDialogOpen(true) }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-muted-foreground">
          Everyone who can sign in to the staff console. Deactivating ends their access immediately.
        </p>
        <Button size="sm" className="press" onClick={openCreate}>
          <UserPlus className="size-4" aria-hidden />
          Add staff member
        </Button>
      </div>

      {directory.isLoading && <Skeleton className="h-64 w-full" />}
      {directory.isError && (
        <p className="py-8 text-center text-sm text-muted-foreground">
          Couldn't load the staff list.{' '}
          <button className="underline" onClick={() => void directory.refetch()}>Try again</button>
        </p>
      )}

      {directory.data && (
        <>
          {/* Mobile: card list, matching the other Settings tabs. */}
          <div className="space-y-2 md:hidden">
            {directory.data.staff.map((m) => (
              <div key={m.id} className="space-y-2 rounded-panel border border-border bg-card p-3 edge-light-soft">
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="truncate font-medium">{m.firstName} {m.lastName}</p>
                    <p className="truncate text-xs text-muted-foreground">{m.email}</p>
                  </div>
                  <Badge variant={m.isActive ? 'success' : 'outline'} className="shrink-0">
                    {m.isActive ? 'Active' : 'Deactivated'}
                  </Badge>
                </div>
                <p className="text-xs text-muted-foreground">
                  {m.roleName} · {branchNames(m.branchIds).join(', ') || 'No branch access'}
                </p>
                <StaffActions member={m} onEdit={() => openEdit(m)} />
              </div>
            ))}
          </div>

          <div className="hidden overflow-hidden rounded-panel border border-border bg-card md:block edge-light-soft">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Role</TableHead>
                  <TableHead>Branches</TableHead>
                  <TableHead>Last signed in</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {directory.data.staff.map((m) => (
                  <TableRow key={m.id}>
                    <TableCell>
                      <div className="font-medium">{m.firstName} {m.lastName}</div>
                      <div className="text-xs text-muted-foreground">{m.email}</div>
                    </TableCell>
                    <TableCell><Badge variant="outline">{m.roleName}</Badge></TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {/* An account with no branches sees empty screens everywhere — say so rather
                          than rendering a blank cell that reads as "fine". */}
                      {branchNames(m.branchIds).join(', ') || (
                        <span className="text-destructive">No branch access</span>
                      )}
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground tabular-nums">
                      {m.lastLoginAt ? new Date(m.lastLoginAt).toLocaleDateString() : 'Never'}
                    </TableCell>
                    <TableCell>
                      <Badge variant={m.isActive ? 'success' : 'outline'}>
                        {m.isActive ? 'Active' : 'Deactivated'}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      <StaffActions member={m} onEdit={() => openEdit(m)} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          {/* Keyed so the form resets to the row being edited rather than keeping the last one's
              values — an edit dialog pre-filled with somebody else's name is a real way to save
              the wrong change. */}
          <StaffFormDialog
            key={editing?.id ?? 'new'}
            open={dialogOpen}
            onOpenChange={setDialogOpen}
            roles={directory.data.roles}
            editing={editing}
          />
        </>
      )}
    </div>
  )
}
