import { Loader2, Play } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { useResumeMembership } from '@/modules/members/api/membersApi'

export function ResumeMembershipButton({ memberId, memberMembershipId }: { memberId: string; memberMembershipId: string }) {
  const resumeMembership = useResumeMembership(memberId)

  const handleResume = () => {
    resumeMembership.mutate(memberMembershipId, {
      onSuccess: () => toast.success('Membership resumed.'),
      onError: () => toast.error('Could not resume membership.'),
    })
  }

  return (
    <Button size="sm" variant="outline" disabled={resumeMembership.isPending} onClick={handleResume}>
      {resumeMembership.isPending ? <Loader2 className="size-4 animate-spin" /> : <Play />}
      Resume
    </Button>
  )
}
