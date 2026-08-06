import { MobileNav } from '@/shared/components/layout/MobileNav'

/**
 * The staff top bar, reduced to what the sidebar cannot hold.
 *
 * It used to carry the branch switcher and an avatar dropdown; both moved into the dark rail, where
 * the redesign puts them and where they are visible without a click. What is left is the drawer
 * trigger, which only exists below the md breakpoint because that is where the rail is hidden — so on
 * a desktop this bar renders nothing at all and the content starts at the top of the window.
 *
 * Members never see this: AppShell renders it only for staff (their own header is part of each
 * screen, and the bar above it was repeating the brand and a second avatar for no extra ability).
 */
export function Topbar() {
  return (
    <header className="flex h-14 shrink-0 items-center gap-4 border-b border-border bg-background px-4 md:hidden">
      <MobileNav />
    </header>
  )
}
