import { useState } from 'react'
import { Loader2, UserPlus } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog'
import { useMembersList } from '@/modules/members/api/membersApi'
import { useAssignClient } from '@/modules/trainers/api/trainersApi'

export function AssignClientDialog({ trainerId }: { trainerId: string }) {
  const [open, setOpen] = useState(false)
  const [searchTerm, setSearchTerm] = useState('')

  const { data: members } = useMembersList({ searchTerm: searchTerm || undefined, status: 'Active', page: 1, pageSize: 6 })
  const assignClient = useAssignClient(trainerId)

  const handleAssign = (memberId: string, memberName: string) => {
    assignClient.mutate(memberId, {
      onSuccess: () => {
        toast.success(`${memberName} assigned.`)
        setOpen(false)
        setSearchTerm('')
      },
      onError: () => toast.error('Could not assign client.'),
    })
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          <UserPlus />
          Assign Client
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Assign a client</DialogTitle>
        </DialogHeader>
        <Input placeholder="Search member..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} />
        {searchTerm && (
          <div className="divide-y rounded-md border">
            {members?.items.length === 0 && <p className="p-3 text-sm text-muted-foreground">No members match.</p>}
            {members?.items.map((m) => (
              <button
                key={m.id}
                type="button"
                disabled={assignClient.isPending}
                onClick={() => handleAssign(m.id, m.fullName)}
                className="flex w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-accent"
              >
                {m.fullName}
                <span className="text-xs text-muted-foreground">{m.memberCode}</span>
              </button>
            ))}
          </div>
        )}
        <DialogFooter>
          {assignClient.isPending && <Loader2 className="size-4 animate-spin" />}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
