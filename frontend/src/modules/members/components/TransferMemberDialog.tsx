import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { ArrowRightLeft, Loader2 } from 'lucide-react'
import { toast } from 'sonner'

import { apiClient } from '@/lib/apiClient'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useTransferMember } from '@/modules/members/api/membersApi'

interface Branch {
  id: string
  name: string
}

export function TransferMemberDialog({ memberId, currentBranchId }: { memberId: string; currentBranchId: string }) {
  const [open, setOpen] = useState(false)
  const [newBranchId, setNewBranchId] = useState('')

  const { data: branches } = useQuery({
    queryKey: ['branches'],
    queryFn: async () => (await apiClient.get<Branch[]>('/api/branches')).data,
  })

  const transfer = useTransferMember(memberId)

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!newBranchId) {
      toast.error('Select a destination branch.')
      return
    }

    transfer.mutate(
      { newBranchId },
      {
        onSuccess: () => {
          toast.success('Member transferred.')
          setOpen(false)
        },
        onError: () => toast.error('Could not transfer member.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          <ArrowRightLeft />
          Transfer Branch
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Transfer member to another branch</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <Select value={newBranchId} onValueChange={setNewBranchId}>
            <SelectTrigger className="w-full">
              <SelectValue placeholder="Select destination branch" />
            </SelectTrigger>
            <SelectContent>
              {branches?.filter((b) => b.id !== currentBranchId).map((b) => (
                <SelectItem key={b.id} value={b.id}>
                  {b.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <DialogFooter>
            <Button type="submit" disabled={transfer.isPending}>
              {transfer.isPending && <Loader2 className="size-4 animate-spin" />}
              Transfer
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
