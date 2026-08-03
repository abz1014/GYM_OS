import { useState } from 'react'
import { Menu } from 'lucide-react'

import { Sheet, SheetContent, SheetDescription, SheetTitle, SheetTrigger } from '@/components/ui/sheet'
import { SidebarNav } from '@/shared/components/layout/Sidebar'

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
      <SheetContent side="left" className="w-64 bg-sidebar text-sidebar-foreground">
        <SheetTitle className="sr-only">Navigation</SheetTitle>
        <SheetDescription className="sr-only">Main module navigation</SheetDescription>
        <SidebarNav onNavigate={() => setOpen(false)} />
      </SheetContent>
    </Sheet>
  )
}
