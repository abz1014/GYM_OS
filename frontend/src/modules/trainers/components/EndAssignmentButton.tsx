import { Loader2, UserX } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { useEndAssignment } from '@/modules/trainers/api/trainersApi'

export function EndAssignmentButton({ trainerId, assignmentId }: { trainerId: string; assignmentId: string }) {
  const endAssignment = useEndAssignment(trainerId)

  return (
    <Button
      size="sm"
      variant="ghost"
      className="rounded-xl"
      disabled={endAssignment.isPending}
      onClick={() =>
        endAssignment.mutate(assignmentId, {
          onSuccess: () => toast.success('Assignment ended.'),
          onError: () => toast.error('Could not end assignment.'),
        })
      }
    >
      {endAssignment.isPending ? <Loader2 className="size-4 animate-spin" /> : <UserX />}
      End
    </Button>
  )
}
