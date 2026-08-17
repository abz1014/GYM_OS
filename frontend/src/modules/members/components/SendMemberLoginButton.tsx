import { KeyRound, Loader2, Send } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { useProvisionMemberLogin } from '@/modules/members/api/membersApi'
import { announceTemporaryPassword, serverReason } from '@/modules/members/lib/accountToast'

/**
 * One button that always leaves the member able to sign in. Its label and icon are the only thing
 * that changes on whether they already have a login — the request and the result are identical,
 * because ProvisionMemberLoginCommand issues a first account or resets an existing one from the same
 * call. Exists for two real gaps: members registered before this feature (or imported through
 * Migration Center) have no login at all, and any member can lose or forget a password with no mail
 * sender in this product to recover it through.
 */
export function SendMemberLoginButton({ memberId, hasLogin, firstName }: { memberId: string; hasLogin: boolean; firstName: string }) {
  const provisionLogin = useProvisionMemberLogin()

  const handleClick = () => {
    provisionLogin.mutate(memberId, {
      onSuccess: (result) =>
        announceTemporaryPassword(
          result.temporaryPassword,
          hasLogin ? `Password reset for ${firstName}` : `${firstName}'s portal login`
        ),
      onError: (err) => toast.error(serverReason(err, 'Could not set up that login.')),
    })
  }

  return (
    <Button size="sm" variant="outline" disabled={provisionLogin.isPending} onClick={handleClick}>
      {provisionLogin.isPending ? <Loader2 className="size-4 animate-spin" /> : hasLogin ? <KeyRound /> : <Send />}
      {hasLogin ? 'Reset portal login' : 'Send portal login'}
    </Button>
  )
}
