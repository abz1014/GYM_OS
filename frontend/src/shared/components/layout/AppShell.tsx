import { useEffect } from 'react'
import { Outlet } from 'react-router-dom'

import { MemberTabBar } from '@/shared/components/layout/MemberTabBar'
import { Sidebar } from '@/shared/components/layout/Sidebar'
import { Topbar } from '@/shared/components/layout/Topbar'
import { useIsMemberOnly } from '@/shared/nav/memberNav'

/**
 * Two shells, one route tree. Staff get the admin layout (sidebar + branch switcher); a member-only
 * login gets a consumer-app layout with bottom tabs and no sidebar at all.
 *
 * They diverge here rather than inside each page because the problem the member shell solves is
 * structural: members were navigating a seven-item admin sidebar built for staff, which is what made
 * the portal read as back-office software rather than a gym app.
 */
export function AppShell() {
  const isMember = useIsMemberOnly()

  /**
   * The member accent lives on <body>, not on the shell div.
   *
   * It has to: Radix portals dialogs — and Sonner its toasts — straight to document.body, which is
   * OUTSIDE any element this component renders. Scoping the theme to the shell left a member tapping
   * an orange button and getting a dialog with a black staff-coloured Save button. Putting the class
   * on body makes portalled content inherit the same variables as the page behind it.
   *
   * Removed on unmount and whenever a non-member is signed in, so the staff console is never tinted.
   */
  useEffect(() => {
    if (!isMember) return
    document.body.classList.add('member-theme')
    return () => document.body.classList.remove('member-theme')
  }, [isMember])

  return (
    <div className="flex h-svh w-full overflow-hidden">
      {!isMember && <Sidebar />}
      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar />
        <main
          className={
            isMember
              ? // Bottom padding clears the fixed tab bar plus the device's own safe area.
                'flex-1 overflow-y-auto p-4 pb-[calc(5rem+env(safe-area-inset-bottom))]'
              : 'flex-1 overflow-y-auto p-3 sm:p-6'
          }
        >
          <Outlet />
        </main>
      </div>
      {isMember && <MemberTabBar />}
    </div>
  )
}
