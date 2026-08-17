import { useEffect } from 'react'
import { Outlet } from 'react-router-dom'

import { CommandPalette } from '@/shared/components/CommandPalette'
import { useCommandPalette } from '@/shared/hooks/useCommandPalette'
import { MemberTabBar } from '@/shared/components/layout/MemberTabBar'
import { Sidebar } from '@/shared/components/layout/Sidebar'
import { Topbar } from '@/shared/components/layout/Topbar'
import { useIsMemberOnly } from '@/shared/nav/memberNav'
import { usePermissionSync } from '@/shared/hooks/usePermissionSync'

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

  // Re-reads permissions from the server on tab focus and after any 403, so an access change reaches
  // an already-open tab instead of waiting for the person to log out. See usePermissionSync.
  usePermissionSync()

  /*
   * The palette is staff-only, and mounting it here is what makes ⌘K work from every screen without
   * any page knowing it exists. Members are excluded deliberately: the whole registry is built from
   * the staff sidebar, so a member would open a palette listing screens they cannot reach, and the
   * member surface is a thumb-driven phone app where a keyboard shortcut has nothing to bind to.
   */
  const palette = useCommandPalette()

  /**
   * The member app is the dark surface (see index.css's file header) — `.dark` lives on <body>, not
   * the shell div.
   *
   * It has to: Radix portals dialogs — and Sonner its toasts — straight to document.body, which is
   * OUTSIDE any element this component renders. Scoping the theme to the shell left a member tapping
   * a volt button and getting a dialog with light staff-coloured chrome. Putting the class on body
   * makes portalled content inherit the same variables as the page behind it.
   *
   * Removed on unmount and whenever a non-member is signed in, so the staff console — which has no
   * light/dark toggle of its own — is never darkened by a leftover class from a previous session.
   */
  useEffect(() => {
    if (!isMember) return
    document.body.classList.add('dark')
    return () => document.body.classList.remove('dark')
  }, [isMember])

  return (
    <div className="flex h-svh w-full overflow-hidden">
      {!isMember && <Sidebar onOpenSearch={() => palette.setOpen(true)} />}
      <div className="flex min-w-0 flex-1 flex-col">
        {/*
          No app bar on the member surface. Every member screen opens with its own header — the date,
          the greeting, and the member's own initial — and the staff Topbar sat above that repeating
          the brand and a second avatar, so the top of every screen carried two identity chips and no
          extra ability. Log out already lives on More, which is where a member goes looking for it.
        */}
        {!isMember && <Topbar />}
        <main
          className={
            isMember
              ? // Clears the fixed tab bar, the device's own safe area, AND the log button, which is
                // a circle raised half its height above the bar's top edge — without the extra room
                // it sits on top of whatever the page ends with.
                'flex-1 overflow-y-auto p-4 pb-[calc(7rem+env(safe-area-inset-bottom))]'
              : 'flex-1 overflow-y-auto p-3 sm:p-6'
          }
        >
          <Outlet />
        </main>
      </div>
      {isMember && <MemberTabBar />}
      {!isMember && <CommandPalette open={palette.open} onOpenChange={palette.setOpen} />}
    </div>
  )
}
