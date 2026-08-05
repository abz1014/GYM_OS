import { Link, useNavigate } from 'react-router-dom'
import { ChevronRight, LogOut } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { useLogout } from '@/modules/auth/api/authApi'
import { useAuthStore } from '@/stores/authStore'
import { MEMBER_MORE_LINKS, type MemberMoreLink } from '@/shared/nav/memberNav'

const GROUP_ORDER: MemberMoreLink['group'][] = ['Training', 'Community', 'Account']

/**
 * Everything that isn't a daily action. Keeping these off the tab bar is the whole point of the
 * four-tab shell: a member opens the app to log a session or check progress, not to read their
 * membership plan — so those live one tap deeper rather than competing for primary navigation.
 */
export default function MorePage() {
  const user = useAuthStore((s) => s.user)
  const refreshToken = useAuthStore((s) => s.refreshToken)
  const clearSession = useAuthStore((s) => s.clearSession)
  const logout = useLogout()
  const navigate = useNavigate()

  const handleLogout = () => {
    if (refreshToken) logout.mutate(refreshToken)
    clearSession()
    navigate('/login')
  }

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">More</h1>
        <p className="text-sm text-muted-foreground">
          {user ? `Signed in as ${user.firstName} ${user.lastName}` : 'Your gym'}
        </p>
      </div>

      {GROUP_ORDER.map((group) => {
        const links = MEMBER_MORE_LINKS.filter((l) => l.group === group)
        if (links.length === 0) return null

        return (
          <section key={group} className="space-y-2">
            <h2 className="text-xs font-semibold tracking-wide text-muted-foreground uppercase">{group}</h2>
            <div className="overflow-hidden rounded-xl border">
              {links.map((link, i) => (
                <Link
                  key={link.path}
                  to={link.path}
                  className={`flex min-h-16 items-center gap-3 px-4 py-3 transition-colors hover:bg-accent ${
                    i > 0 ? 'border-t' : ''
                  }`}
                >
                  <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-muted">
                    <link.icon className="size-5 text-foreground" />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block font-medium">{link.label}</span>
                    <span className="block truncate text-sm text-muted-foreground">{link.description}</span>
                  </span>
                  <ChevronRight className="size-5 shrink-0 text-muted-foreground" />
                </Link>
              ))}
            </div>
          </section>
        )
      })}

      <Button variant="outline" className="w-full" onClick={handleLogout}>
        <LogOut className="size-4" />
        Sign out
      </Button>
    </div>
  )
}
