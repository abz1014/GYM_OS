import { useState } from 'react'
import { NavLink, useNavigate } from 'react-router-dom'
import { ChevronDown, Dumbbell, LogOut } from 'lucide-react'

import { cn } from '@/lib/utils'
import { useAuthStore } from '@/stores/authStore'
import { useLogout } from '@/modules/auth/api/authApi'
import { NAV_MODULES, NAV_SECTIONS, type NavModule } from '@/shared/nav/modules'
import { BranchSwitcher } from '@/shared/components/layout/BranchSwitcher'

function ModuleLink({ module, onNavigate }: { module: NavModule; onNavigate?: () => void }) {
  return (
    <NavLink
      to={module.path}
      onClick={onNavigate}
      className={({ isActive }) =>
        cn(
          'flex items-center justify-between gap-2 rounded-xl px-3 py-2 text-sm font-medium transition-colors',
          isActive
            ? 'bg-sidebar-primary text-sidebar-primary-foreground'
            : 'text-sidebar-foreground/70 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground',
        )
      }
    >
      <span className="flex min-w-0 items-center gap-2.5">
        <module.icon className="size-4 shrink-0" />
        <span className="truncate">{module.label}</span>
      </span>
    </NavLink>
  )
}

/**
 * The brand header + module nav, shared by the desktop sidebar and the mobile drawer (MobileNav) so
 * there is exactly one definition of what navigation looks like.
 *
 * Grouped rather than a flat list of eighteen. The groups are named for the job someone is doing —
 * running a shift, chasing money, looking after the building — because that is how a person arrives
 * at this console, and an alphabet-soup column of every module gave equal weight to "Dashboard" and
 * "Migration Center". Everything outside those three collapses behind one row, so the list a person
 * reads every day is short and the long tail is still one click away.
 */
export function SidebarNav({ onNavigate }: { onNavigate?: () => void }) {
  const permissions = useAuthStore((s) => s.user?.permissions ?? [])
  const [showMore, setShowMore] = useState(false)

  const allowed = NAV_MODULES.filter((m) => permissions.includes(m.permission))
  const more = allowed.filter((m) => m.section === 'More')

  return (
    <>
      <div className="flex h-14 shrink-0 items-center gap-2 px-4">
        <span className="flex size-8 items-center justify-center rounded-lg bg-sidebar-primary text-sidebar-primary-foreground">
          <Dumbbell className="size-4" />
        </span>
        <span className="font-display font-black tracking-tight text-sidebar-foreground">GymOS</span>
      </div>

      <div className="px-3 pb-2">
        <BranchSwitcher />
      </div>

      <nav className="flex-1 space-y-5 overflow-y-auto px-3 pb-3">
        {NAV_SECTIONS.map((section) => {
          const modules = allowed.filter((m) => m.section === section)
          if (modules.length === 0) return null

          return (
            <div key={section} className="space-y-0.5">
              <h2 className="px-3 pb-1 text-[10px] font-bold tracking-[0.14em] text-sidebar-foreground/40 uppercase">
                {section}
              </h2>
              {modules.map((m) => (
                <ModuleLink key={m.key} module={m} onNavigate={onNavigate} />
              ))}
            </div>
          )
        })}

        {/* The long tail. Collapsed by default and counted on its own row, so it reads as "there is
            more here" rather than padding the daily list with things opened once a quarter. */}
        {more.length > 0 && (
          <div className="space-y-0.5">
            <button
              type="button"
              onClick={() => setShowMore((v) => !v)}
              aria-expanded={showMore}
              className="flex w-full items-center justify-between gap-2 rounded-xl px-3 py-2 text-sm font-medium text-sidebar-foreground/70 transition-colors hover:bg-sidebar-accent hover:text-sidebar-accent-foreground"
            >
              <span className="flex items-center gap-2.5">
                <ChevronDown className={cn('size-4 transition-transform', !showMore && '-rotate-90')} />
                More modules
              </span>
              <span className="shrink-0 text-xs text-sidebar-foreground/40 tabular-nums">+{more.length}</span>
            </button>
            {showMore && more.map((m) => <ModuleLink key={m.key} module={m} onNavigate={onNavigate} />)}
          </div>
        )}
      </nav>
    </>
  )
}

/**
 * The signed-in person, pinned to the bottom of the dark rail. It used to live in the top bar as an
 * avatar dropdown; down here it is out of the way of the work and in the place every console of this
 * shape puts it.
 */
export function SidebarAccount() {
  const user = useAuthStore((s) => s.user)
  const refreshToken = useAuthStore((s) => s.refreshToken)
  const clearSession = useAuthStore((s) => s.clearSession)
  const logout = useLogout()
  const navigate = useNavigate()

  if (!user) return null

  const initials = `${user.firstName.at(0) ?? ''}${user.lastName.at(0) ?? ''}`

  const handleLogout = () => {
    if (refreshToken) logout.mutate(refreshToken)
    clearSession()
    navigate('/login')
  }

  return (
    <div className="flex items-center gap-2.5 border-t border-sidebar-border p-3">
      <button
        type="button"
        onClick={() => navigate('/account')}
        className="flex min-w-0 flex-1 items-center gap-2.5 rounded-xl p-1 text-left transition-colors hover:bg-sidebar-accent"
      >
        <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-sidebar-accent text-xs font-bold text-sidebar-accent-foreground">
          {initials}
        </span>
        <span className="min-w-0">
          <span className="block truncate text-sm font-medium text-sidebar-foreground">
            {user.firstName} {user.lastName}
          </span>
          <span className="block truncate text-xs text-sidebar-foreground/50">{user.roles.join(', ')}</span>
        </span>
      </button>
      <button
        type="button"
        onClick={handleLogout}
        aria-label="Log out"
        className="flex size-9 shrink-0 items-center justify-center rounded-xl text-sidebar-foreground/50 transition-colors hover:bg-sidebar-accent hover:text-sidebar-foreground"
      >
        <LogOut className="size-4" />
      </button>
    </div>
  )
}

export function Sidebar() {
  return (
    // Fixed dark chrome, in both themes — see index.css. 246px per the redesign's shell.
    <aside className="hidden w-[246px] shrink-0 flex-col bg-sidebar text-sidebar-foreground md:flex">
      <SidebarNav />
      <SidebarAccount />
    </aside>
  )
}
