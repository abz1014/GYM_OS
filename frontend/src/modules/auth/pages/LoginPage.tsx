import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Dumbbell, Loader2 } from 'lucide-react'
import { toast } from 'sonner'
import type { AxiosError } from 'axios'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useAuthStore } from '@/stores/authStore'
import { useLogin } from '@/modules/auth/api/authApi'
import { resolveLandingRoute } from '@/shared/nav/landingRoute'

const DEMO_ROLES = ['owner', 'manager', 'receptionist', 'trainer', 'nutritionist', 'accountant', 'maintenance', 'member']

export default function LoginPage() {
  const [email, setEmail] = useState('owner@titanfitness.demo')
  const [password, setPassword] = useState('Demo@12345')
  const [mfaRequired, setMfaRequired] = useState(false)
  const [mfaCode, setMfaCode] = useState('')
  const navigate = useNavigate()
  const setSession = useAuthStore((s) => s.setSession)
  const login = useLogin()

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    login.mutate(
      { email, password, mfaCode: mfaRequired ? mfaCode : undefined },
      {
        onSuccess: (data) => {
          setSession(data)
          toast.success(`Welcome back, ${data.user.firstName}`)
          navigate(resolveLandingRoute((code) => data.user.permissions.includes(code)))
        },
        onError: (error) => {
          const problem = (error as AxiosError<{ title?: string }>).response?.data
          if (problem?.title === 'A valid MFA code is required.') {
            setMfaRequired(true)
            toast.error(mfaRequired ? 'Invalid code — try again.' : 'Enter your authenticator app code.')
            return
          }
          toast.error(problem?.title === 'Invalid email or password.' ? problem.title : 'Login failed. Is the API running?')
        },
      }
    )
  }

  return (
    <div className="flex min-h-svh items-center justify-center bg-muted/30 p-4">
      <Card className="w-full max-w-sm">
        <CardHeader className="items-center text-center">
          <div className="mb-2 flex size-10 items-center justify-center rounded-lg bg-primary text-primary-foreground">
            <Dumbbell className="size-5" />
          </div>
          <CardTitle className="text-xl">Sign in to GymOS</CardTitle>
          <CardDescription>Titan Fitness — demo environment</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="email">Email</Label>
              <Input id="email" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <Label htmlFor="password">Password</Label>
                <Link to="/forgot-password" className="text-xs text-muted-foreground hover:underline">
                  Forgot password?
                </Link>
              </div>
              <Input
                id="password"
                type="password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                disabled={mfaRequired}
              />
            </div>
            {mfaRequired && (
              <div className="space-y-1.5">
                <Label htmlFor="mfaCode">Authenticator code</Label>
                <Input
                  id="mfaCode"
                  required
                  autoFocus
                  placeholder="6-digit code"
                  value={mfaCode}
                  onChange={(e) => setMfaCode(e.target.value)}
                />
              </div>
            )}
            <Button type="submit" className="w-full" disabled={login.isPending}>
              {login.isPending && <Loader2 className="size-4 animate-spin" />}
              Sign in
            </Button>
          </form>

          <div className="mt-6 rounded-md border bg-muted/40 p-3 text-xs text-muted-foreground">
            <p className="mb-1 font-medium text-foreground">Demo accounts (password: Demo@12345)</p>
            <div className="flex flex-wrap gap-1">
              {DEMO_ROLES.map((role) => (
                <button
                  key={role}
                  type="button"
                  onClick={() => setEmail(`${role}@titanfitness.demo`)}
                  className="rounded border bg-background px-1.5 py-0.5 hover:bg-accent"
                >
                  {role}
                </button>
              ))}
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
