import { NavLink } from 'react-router-dom'
import { Dumbbell } from 'lucide-react'

import { cn } from '@/lib/utils'
import { useAuthStore } from '@/stores/authStore'
import { NAV_MODULES } from '@/shared/nav/modules'
import { Badge } from '@/components/ui/badge'

export function Sidebar() {
  const permissions = useAuthStore((s) => s.user?.permissions ?? [])

  return (
    <aside className="hidden w-64 shrink-0 flex-col border-r bg-sidebar text-sidebar-foreground md:flex">
      <div className="flex h-14 items-center gap-2 border-b px-4">
        <Dumbbell className="size-5 text-primary" />
        <span className="font-semibold tracking-tight">Titan Fitness</span>
      </div>

      <nav className="flex-1 space-y-0.5 overflow-y-auto p-2">
        {NAV_MODULES.map((module) => {
          const allowed = permissions.includes(module.permission)
          const comingSoon = module.wave > 1

          if (comingSoon) {
            return (
              <div
                key={module.key}
                className="flex cursor-not-allowed items-center justify-between gap-2 rounded-md px-3 py-2 text-sm text-muted-foreground/60"
                title="Coming soon"
              >
                <span className="flex items-center gap-2">
                  <module.icon className="size-4" />
                  {module.label}
                </span>
                <Badge variant="outline" className="text-[10px] text-muted-foreground/60">
                  Soon
                </Badge>
              </div>
            )
          }

          if (!allowed) {
            return null
          }

          return (
            <NavLink
              key={module.key}
              to={module.path}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                  isActive
                    ? 'bg-sidebar-accent text-sidebar-accent-foreground'
                    : 'text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground'
                )
              }
            >
              <module.icon className="size-4" />
              {module.label}
            </NavLink>
          )
        })}
      </nav>
    </aside>
  )
}
