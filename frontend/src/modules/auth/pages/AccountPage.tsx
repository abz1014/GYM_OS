import { useState } from 'react'
import { Loader2, ShieldCheck } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuthStore } from '@/stores/authStore'
import {
  useChangePassword,
  useConfirmMfaSetup,
  useDisableMfa,
  useInitiateMfaSetup,
  type MfaSetupResult,
} from '@/modules/auth/api/accountApi'

function ChangePasswordCard() {
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')

  const changePassword = useChangePassword()

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (newPassword !== confirmPassword) {
      toast.error('New passwords do not match.')
      return
    }

    changePassword.mutate(
      { currentPassword, newPassword },
      {
        onSuccess: () => {
          toast.success('Password changed.')
          setCurrentPassword('')
          setNewPassword('')
          setConfirmPassword('')
        },
        onError: () => toast.error('Could not change password — check your current password.'),
      }
    )
  }

  return (
    <Card>
      <CardContent className="space-y-4">
        <div>
          <h2 className="text-sm font-medium">Change password</h2>
          <p className="text-sm text-muted-foreground">Use a password at least 8 characters long.</p>
        </div>
        <form onSubmit={handleSubmit} className="max-w-sm space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="currentPassword">Current password</Label>
            <Input
              id="currentPassword"
              type="password"
              required
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="newPassword">New password</Label>
            <Input
              id="newPassword"
              type="password"
              required
              minLength={8}
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="confirmPassword">Confirm new password</Label>
            <Input
              id="confirmPassword"
              type="password"
              required
              minLength={8}
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
            />
          </div>
          <Button type="submit" disabled={changePassword.isPending}>
            {changePassword.isPending && <Loader2 className="size-4 animate-spin" />}
            Change Password
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}

function EnableMfaFlow({ onDone }: { onDone: () => void }) {
  const [setupResult, setSetupResult] = useState<MfaSetupResult | null>(null)
  const [code, setCode] = useState('')

  const initiate = useInitiateMfaSetup()
  const confirm = useConfirmMfaSetup()

  const handleStart = () => {
    initiate.mutate(undefined, {
      onSuccess: (data) => setSetupResult(data),
      onError: () => toast.error('Could not start MFA setup.'),
    })
  }

  const handleConfirm = (e: React.FormEvent) => {
    e.preventDefault()
    confirm.mutate(code, {
      onSuccess: () => {
        toast.success('Two-factor authentication enabled.')
        onDone()
      },
      onError: () => toast.error('Invalid code — check your authenticator app and try again.'),
    })
  }

  if (!setupResult) {
    return (
      <Button size="sm" onClick={handleStart} disabled={initiate.isPending}>
        {initiate.isPending && <Loader2 className="size-4 animate-spin" />}
        Enable 2FA
      </Button>
    )
  }

  return (
    <form onSubmit={handleConfirm} className="max-w-sm space-y-3 rounded-md border p-4">
      <div>
        <p className="text-sm font-medium">Scan with your authenticator app</p>
        <p className="mt-1 break-all rounded bg-muted p-2 font-mono text-xs">{setupResult.qrCodeUri}</p>
      </div>
      <div>
        <p className="text-sm font-medium">Or enter this key manually</p>
        <p className="mt-1 break-all rounded bg-muted p-2 font-mono text-xs">{setupResult.secret}</p>
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="mfaCode">6-digit code from your app</Label>
        <Input id="mfaCode" required value={code} onChange={(e) => setCode(e.target.value)} />
      </div>
      <Button type="submit" size="sm" disabled={confirm.isPending}>
        {confirm.isPending && <Loader2 className="size-4 animate-spin" />}
        Confirm &amp; Enable
      </Button>
    </form>
  )
}

function DisableMfaFlow({ onDone }: { onDone: () => void }) {
  const [open, setOpen] = useState(false)
  const [password, setPassword] = useState('')

  const disableMfa = useDisableMfa()

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    disableMfa.mutate(password, {
      onSuccess: () => {
        toast.success('Two-factor authentication disabled.')
        setOpen(false)
        setPassword('')
        onDone()
      },
      onError: () => toast.error('Incorrect password.'),
    })
  }

  if (!open) {
    return (
      <Button size="sm" variant="outline" onClick={() => setOpen(true)}>
        Disable 2FA
      </Button>
    )
  }

  return (
    <form onSubmit={handleSubmit} className="max-w-sm space-y-3">
      <div className="space-y-1.5">
        <Label htmlFor="disableMfaPassword">Confirm your password</Label>
        <Input
          id="disableMfaPassword"
          type="password"
          required
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
      </div>
      <Button type="submit" size="sm" variant="destructive" disabled={disableMfa.isPending}>
        {disableMfa.isPending && <Loader2 className="size-4 animate-spin" />}
        Disable 2FA
      </Button>
    </form>
  )
}

function MfaCard() {
  const user = useAuthStore((s) => s.user)
  const [refreshKey, setRefreshKey] = useState(0)

  if (!user) {
    return null
  }

  return (
    <Card key={refreshKey}>
      <CardContent className="space-y-4">
        <div className="flex items-center gap-2">
          <ShieldCheck className="size-4 text-muted-foreground" />
          <h2 className="text-sm font-medium">Two-factor authentication</h2>
        </div>
        {user.mfaEnabled ? (
          <>
            <p className="text-sm text-muted-foreground">
              Two-factor authentication is currently <span className="font-medium text-foreground">enabled</span> on
              your account.
            </p>
            <DisableMfaFlow onDone={() => setRefreshKey((k) => k + 1)} />
          </>
        ) : (
          <>
            <p className="text-sm text-muted-foreground">
              Add an extra layer of security by requiring a code from an authenticator app at login.
            </p>
            <EnableMfaFlow onDone={() => setRefreshKey((k) => k + 1)} />
          </>
        )}
      </CardContent>
    </Card>
  )
}

export default function AccountPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">My Account</h1>
        <p className="text-sm text-muted-foreground">Manage your password and security settings.</p>
      </div>
      <ChangePasswordCard />
      <MfaCard />
    </div>
  )
}
