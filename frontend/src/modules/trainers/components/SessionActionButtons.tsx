import { CheckCircle2, Loader2, XCircle } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { useCancelSession, useCompleteSession } from '@/modules/trainers/api/trainersApi'

export function SessionActionButtons({ trainerId, sessionId }: { trainerId: string; sessionId: string }) {
  const completeSession = useCompleteSession(trainerId)
  const cancelSession = useCancelSession(trainerId)

  return (
    <div className="flex items-center gap-2">
      <Button
        size="sm"
        variant="outline"
        className="rounded-xl"
        disabled={completeSession.isPending || cancelSession.isPending}
        onClick={() =>
          completeSession.mutate(
            { sessionId },
            {
              onSuccess: () => toast.success('Session marked complete.'),
              onError: () => toast.error('Could not complete session.'),
            }
          )
        }
      >
        {completeSession.isPending ? <Loader2 className="size-4 animate-spin" /> : <CheckCircle2 />}
        Complete
      </Button>
      <Button
        size="sm"
        variant="ghost"
        className="rounded-xl"
        disabled={completeSession.isPending || cancelSession.isPending}
        onClick={() =>
          cancelSession.mutate(sessionId, {
            onSuccess: () => toast.success('Session cancelled.'),
            onError: () => toast.error('Could not cancel session.'),
          })
        }
      >
        {cancelSession.isPending ? <Loader2 className="size-4 animate-spin" /> : <XCircle />}
        Cancel
      </Button>
    </div>
  )
}
