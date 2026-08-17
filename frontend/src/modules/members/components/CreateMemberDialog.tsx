import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Loader2, UserPlus, X } from 'lucide-react'
import { toast } from 'sonner'

import { apiClient } from '@/lib/apiClient'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useCreateMember, type MemberListItem } from '@/modules/members/api/membersApi'
import { announceTemporaryPassword, serverReason } from '@/modules/members/lib/accountToast'
import type { PagedList } from '@/types/paging'

interface Branch {
  id: string
  name: string
}

export function CreateMemberDialog() {
  const [open, setOpen] = useState(false)
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [email, setEmail] = useState('')
  const [phone, setPhone] = useState('')
  const [branchId, setBranchId] = useState<string>('')
  const [referrerSearch, setReferrerSearch] = useState('')
  const [referrer, setReferrer] = useState<{ id: string; name: string } | null>(null)

  const { data: branches } = useQuery({
    queryKey: ['branches'],
    queryFn: async () => (await apiClient.get<Branch[]>('/api/branches')).data,
  })

  // Referred-by lookup: search kicks in from 2 characters and pauses once a referrer is chosen.
  const { data: referrerMatches } = useQuery({
    queryKey: ['members', 'referrer-search', referrerSearch],
    queryFn: async () =>
      (await apiClient.get<PagedList<MemberListItem>>('/api/members', {
        params: { searchTerm: referrerSearch, page: 1, pageSize: 5 },
      })).data,
    enabled: open && referrer === null && referrerSearch.trim().length >= 2,
  })

  const createMember = useCreateMember()

  const reset = () => {
    setFirstName('')
    setLastName('')
    setEmail('')
    setPhone('')
    setBranchId('')
    setReferrerSearch('')
    setReferrer(null)
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!branchId) {
      toast.error('Please select a branch.')
      return
    }

    createMember.mutate(
      { firstName, lastName, email, phone: phone || undefined, branchId, referredByMemberId: referrer?.id ?? null },
      {
        onSuccess: (result) => {
          announceTemporaryPassword(result.temporaryPassword, `${firstName} ${lastName} registered`)
          reset()
          setOpen(false)
        },
        onError: (err) => toast.error(serverReason(err, 'Could not register member.')),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <UserPlus />
          Register Member
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Register a new member</DialogTitle>
          <DialogDescription>They get a temporary password for the member portal, to change on first sign-in.</DialogDescription>
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
            <Label htmlFor="phone">Phone (optional)</Label>
            <Input id="phone" value={phone} onChange={(e) => setPhone(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label>Branch</Label>
            <Select value={branchId} onValueChange={setBranchId}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Select a branch" />
              </SelectTrigger>
              <SelectContent>
                {branches?.map((b) => (
                  <SelectItem key={b.id} value={b.id}>
                    {b.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="referrer">Referred by (optional)</Label>
            {referrer ? (
              <Badge variant="secondary" className="gap-1">
                {referrer.name}
                <button type="button" aria-label="Clear referrer" onClick={() => setReferrer(null)}>
                  <X className="size-3" />
                </button>
              </Badge>
            ) : (
              <>
                <Input
                  id="referrer"
                  placeholder="Search members by name or code"
                  value={referrerSearch}
                  onChange={(e) => setReferrerSearch(e.target.value)}
                />
                {referrerMatches && referrerSearch.trim().length >= 2 && (
                  <div className="divide-y divide-border rounded-xl border border-border">
                    {referrerMatches.items.length === 0 && (
                      <p className="p-2 text-xs text-muted-foreground">No members match.</p>
                    )}
                    {referrerMatches.items.map((m) => (
                      <button
                        key={m.id}
                        type="button"
                        className="flex w-full items-center justify-between gap-2 p-2 text-left text-sm hover:bg-accent"
                        onClick={() => setReferrer({ id: m.id, name: m.fullName })}
                      >
                        <span className="truncate">{m.fullName}</span>
                        <span className="shrink-0 text-xs text-muted-foreground">{m.memberCode}</span>
                      </button>
                    ))}
                  </div>
                )}
              </>
            )}
          </div>
          <DialogFooter>
            <Button type="submit" disabled={createMember.isPending}>
              {createMember.isPending && <Loader2 className="size-4 animate-spin" />}
              Register
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
