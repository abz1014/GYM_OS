import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Dumbbell, Loader2 } from 'lucide-react'
import { toast } from 'sonner'
import type { AxiosError } from 'axios'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuthStore } from '@/stores/authStore'
import { useLogin } from '@/modules/auth/api/authApi'
import { resolveLandingRoute } from '@/shared/nav/landingRoute'

const DEMO_ROLES = ['owner', 'manager', 'receptionist', 'trainer', 'nutritionist', 'accountant', 'maintenance', 'member', 'member2']

/**
 * The one door into GymOS, staff and members alike — dark ink and volt because the brand identity
 * is shared across both surfaces (see index.css's file header), before role ever splits the two apart.
 *
 * Bottom-anchored rather than centered: the redesign's rationale is thumb reach on a phone held
 * one-handed, and it costs nothing on desktop either.
 *
 * Two things the original design reference asked for and don't exist here: "Keep me signed in" (this
 * app always persists the session — zustand's persist middleware, unconditionally — so a checkbox
 * promising otherwise would be describing a toggle that does nothing) and "Use Face ID" (no WebAuthn/
 * biometric capability exists anywhere in this stack). Both are dropped rather than shipped as buttons
 * that don't do what they say. Same reasoning kills the streak-specific subhead ("keep your 7-week
 * streak alive") — nothing is known about who's typing until they've typed it, so the copy stays
 * honest and generic instead of guessing a number.
 */
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
    // `text-foreground` is not redundant next to `dark`. The base layer puts that class on <body>,
    // which is OUTSIDE this element — so body resolves --foreground against :root (the light staff
    // palette, near-black ink) and every descendant that doesn't set its own colour inherits it. The
    // inputs did exactly that and rendered near-black text on the near-black card. Re-declaring it
    // here computes the variable inside the .dark scope, where it is the light ink.
    <div className="dark relative flex min-h-svh flex-col justify-end overflow-hidden bg-background p-6 pb-10 text-foreground sm:justify-center sm:py-16">
      {/* Radial volt glow, top-left — pure decoration, sits behind everything. */}
      <div
        aria-hidden
        className="pointer-events-none absolute -top-1/4 -left-1/4 size-[70vh] rounded-full opacity-25 blur-3xl"
        style={{ background: 'radial-gradient(circle, var(--primary) 0%, transparent 70%)' }}
      />

      <div className="relative mx-auto w-full max-w-sm">
        <div className="mb-8 flex size-13 items-center justify-center rounded-2xl bg-primary text-primary-foreground shadow-[0_8px_22px_-6px_var(--primary)]">
          <Dumbbell className="size-6" />
        </div>

        <h1 className="font-display text-4xl leading-[1.05] font-black tracking-tight text-foreground">Welcome back.</h1>
        <p className="mt-2 text-sm text-muted-foreground">Sign in to your account.</p>

        <form onSubmit={handleSubmit} className="mt-8 space-y-5">
          <div className="space-y-1.5">
            <Label htmlFor="email" className="text-[11px] font-bold tracking-[0.1em] text-muted-foreground uppercase">
              Email
            </Label>
            <Input
              id="email"
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="h-14 rounded-2xl border-border bg-card px-4 text-base focus-visible:ring-[3px] focus-visible:ring-ring/20"
            />
          </div>

          <div className="space-y-1.5">
            <div className="flex items-center justify-between">
              <Label htmlFor="password" className="text-[11px] font-bold tracking-[0.1em] text-muted-foreground uppercase">
                Password
              </Label>
              <Link to="/forgot-password" className="text-xs font-medium text-primary hover:underline">
                Forgot?
              </Link>
            </div>
            <Input
              id="password"
              type="password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              disabled={mfaRequired}
              className="h-14 rounded-2xl border-border bg-card px-4 text-base focus-visible:ring-[3px] focus-visible:ring-ring/20"
            />
          </div>

          {mfaRequired && (
            <div className="space-y-1.5">
              <Label htmlFor="mfaCode" className="text-[11px] font-bold tracking-[0.1em] text-muted-foreground uppercase">
                Authenticator code
              </Label>
              <Input
                id="mfaCode"
                required
                autoFocus
                placeholder="6-digit code"
                value={mfaCode}
                onChange={(e) => setMfaCode(e.target.value)}
                className="h-14 rounded-2xl border-border bg-card px-4 text-base focus-visible:ring-[3px] focus-visible:ring-ring/20"
              />
            </div>
          )}

          <Button
            type="submit"
            disabled={login.isPending}
            className="h-[58px] w-full rounded-2xl text-base font-bold shadow-[0_8px_22px_-6px_var(--primary)]"
          >
            {login.isPending && <Loader2 className="size-4 animate-spin" />}
            Sign in
          </Button>
        </form>

        <p className="mt-6 text-center text-xs text-muted-foreground">
          New here? Ask the front desk for an invite.
        </p>

        {import.meta.env.DEV && (
          <div className="mt-6 rounded-2xl border border-border bg-card p-3 text-xs text-muted-foreground">
            <p className="mb-1 font-medium text-foreground">Dev only — demo accounts (Demo@12345)</p>
            <div className="flex flex-wrap gap-1">
              {DEMO_ROLES.map((role) => (
                <button
                  key={role}
                  type="button"
                  onClick={() => setEmail(`${role}@titanfitness.demo`)}
                  className="rounded-lg border border-border bg-background px-1.5 py-0.5 hover:bg-accent"
                >
                  {role}
                </button>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
