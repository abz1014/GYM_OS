import { useNavigate } from 'react-router-dom'
import { Dumbbell, LogOut, User as UserIcon } from 'lucide-react'

import { useLogout } from '@/modules/auth/api/authApi'
import { useAuthStore } from '@/stores/authStore'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { BranchSwitcher } from '@/shared/components/layout/BranchSwitcher'
import { MobileNav } from '@/shared/components/layout/MobileNav'
import { useIsMemberOnly } from '@/shared/nav/memberNav'

export function Topbar() {
  const isMember = useIsMemberOnly()
  const user = useAuthStore((s) => s.user)
  const refreshToken = useAuthStore((s) => s.refreshToken)
  const clearSession = useAuthStore((s) => s.clearSession)
  const logout = useLogout()
  const navigate = useNavigate()

  const initials = user ? `${user.firstName.at(0) ?? ''}${user.lastName.at(0) ?? ''}` : ''

  const handleLogout = () => {
    if (refreshToken) {
      logout.mutate(refreshToken)
    }
    clearSession()
    navigate('/login')
  }

  return (
    <header className="flex h-14 items-center justify-between gap-4 border-b bg-background px-4">
      <div className="flex items-center gap-2">
        {/* Members navigate via the bottom tab bar and belong to a single branch, so neither the
            staff drawer nor the branch switcher applies to them. */}
        {isMember ? (
          <span className="flex items-center gap-2 font-semibold">
            <Dumbbell className="size-5 text-primary" />
            Titan Fitness
          </span>
        ) : (
          <>
            <MobileNav />
            <BranchSwitcher />
          </>
        )}
      </div>

      <DropdownMenu>
        <DropdownMenuTrigger className="flex items-center gap-2 rounded-md px-2 py-1 text-sm hover:bg-accent">
          <Avatar className="size-7">
            <AvatarFallback className="text-xs">{initials || <UserIcon className="size-4" />}</AvatarFallback>
          </Avatar>
          <span className="hidden sm:inline">
            {user?.firstName} {user?.lastName}
          </span>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <DropdownMenuLabel>{user?.roles.join(', ')}</DropdownMenuLabel>
          <DropdownMenuSeparator />
          <DropdownMenuItem onSelect={() => navigate('/account')}>
            <UserIcon />
            My Account
          </DropdownMenuItem>
          <DropdownMenuItem onSelect={handleLogout}>
            <LogOut />
            Log out
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </header>
  )
}
