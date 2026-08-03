import { Loader2, RotateCcw } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { useReactivateMembership } from '@/modules/members/api/membersApi'

export function ReactivateMembershipButton({ memberId, memberMembershipId }: { memberId: string; memberMembershipId: string }) {
  const reactivateMembership = useReactivateMembership(memberId)

  const handleReactivate = () => {
    reactivateMembership.mutate(memberMembershipId, {
      onSuccess: () => toast.success('Membership reactivated.'),
      onError: () => toast.error('Could not reactivate membership.'),
    })
  }

  return (
    <Button size="sm" variant="outline" disabled={reactivateMembership.isPending} onClick={handleReactivate}>
      {reactivateMembership.isPending ? <Loader2 className="size-4 animate-spin" /> : <RotateCcw />}
      Reactivate
    </Button>
  )
}
