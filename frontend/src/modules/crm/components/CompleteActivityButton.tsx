import { Check, Loader2 } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { useCompleteLeadActivity } from '@/modules/crm/api/crmApi'

export function CompleteActivityButton({ leadId, activityId }: { leadId: string; activityId: string }) {
  const completeActivity = useCompleteLeadActivity(leadId)

  return (
    <Button
      size="sm"
      variant="outline"
      className="rounded-xl"
      disabled={completeActivity.isPending}
      onClick={() =>
        completeActivity.mutate(activityId, {
          onSuccess: () => toast.success('Activity marked done.'),
          onError: () => toast.error('Could not update activity.'),
        })
      }
    >
      {completeActivity.isPending ? <Loader2 className="size-4 animate-spin" /> : <Check />}
      Mark Done
    </Button>
  )
}
