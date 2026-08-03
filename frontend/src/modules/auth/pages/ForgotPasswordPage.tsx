import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Dumbbell, Loader2 } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useForgotPassword } from '@/modules/auth/api/authApi'

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [sent, setSent] = useState(false)
  const forgotPassword = useForgotPassword()

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    forgotPassword.mutate(email, {
      onSuccess: () => {
        setSent(true)
        toast.info('If that email exists, a reset link was written to the Dev Mailbox.')
      },
    })
  }

  return (
    <div className="flex min-h-svh items-center justify-center bg-muted/30 p-4">
      <Card className="w-full max-w-sm">
        <CardHeader className="items-center text-center">
          <div className="mb-2 flex size-10 items-center justify-center rounded-lg bg-primary text-primary-foreground">
            <Dumbbell className="size-5" />
          </div>
          <CardTitle className="text-xl">Reset your password</CardTitle>
          <CardDescription>We'll log a reset link to the demo Dev Mailbox (no real email is sent).</CardDescription>
        </CardHeader>
        <CardContent>
          {sent ? (
            <p className="text-sm text-muted-foreground">
              Check the Dev Mailbox (Settings → Notifications, once available) for your reset token.
            </p>
          ) : (
            <form onSubmit={handleSubmit} className="space-y-4">
              <div className="space-y-1.5">
                <Label htmlFor="email">Email</Label>
                <Input id="email" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
              </div>
              <Button type="submit" className="w-full" disabled={forgotPassword.isPending}>
                {forgotPassword.isPending && <Loader2 className="size-4 animate-spin" />}
                Send reset link
              </Button>
            </form>
          )}
          <Link to="/login" className="mt-4 block text-center text-xs text-muted-foreground hover:underline">
            Back to sign in
          </Link>
        </CardContent>
      </Card>
    </div>
  )
}
