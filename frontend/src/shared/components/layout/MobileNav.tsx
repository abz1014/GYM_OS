import { useState } from 'react'
import { Menu } from 'lucide-react'

import { Sheet, SheetContent, SheetDescription, SheetTitle, SheetTrigger } from '@/components/ui/sheet'
import { SidebarAccount, SidebarNav } from '@/shared/components/layout/Sidebar'

/**
 * Below the md breakpoint the desktop sidebar is hidden entirely, which used to leave phones
 * with no way to navigate at all. This hamburger opens the same SidebarNav in a left drawer.
 */
export function MobileNav() {
  const [open, setOpen] = useState(false)

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger
        className="flex size-9 items-center justify-center rounded-md hover:bg-accent md:hidden"
        aria-label="Open navigation"
      >
        <Menu className="size-5" />
      </SheetTrigger>
      {/* flex-col so the account row pins to the bottom exactly as it does on the desktop rail —
          on a phone there is no rail, so this drawer is the ONLY place to reach it or sign out. */}
      <SheetContent side="left" className="flex w-[246px] flex-col bg-sidebar p-0 text-sidebar-foreground">
        <SheetTitle className="sr-only">Navigation</SheetTitle>
        <SheetDescription className="sr-only">Main module navigation</SheetDescription>
        <SidebarNav onNavigate={() => setOpen(false)} />
        <SidebarAccount />
      </SheetContent>
    </Sheet>
  )
}
