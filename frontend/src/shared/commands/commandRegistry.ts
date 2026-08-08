import type { LucideIcon } from 'lucide-react'

import { NAV_MODULES } from '@/shared/nav/modules'

export interface Command {
  id: string
  label: string
  /** The group heading it sits under in the palette. */
  group: string
  icon: LucideIcon
  /** Where it goes. Every command in this registry is navigation — see the remarks. */
  route: string
  /** Extra words that should match this command without being displayed. */
  keywords?: string[]
}

/**
 * The palette's commands, derived from the sidebar rather than listed separately.
 *
 * Deriving them matters more than it looks: a hand-written second list is a list that silently
 * disagrees with the sidebar the first time a module is added, renamed or re-permissioned, and the
 * disagreement shows up as a palette entry leading somewhere the person cannot go. NAV_MODULES
 * already carries the label, the icon, the route, the permission and the role requirement, so the
 * registry is a projection of it and cannot drift.
 *
 * **Every command here is navigation, and that is a deliberate limit.** A palette that performs
 * actions — "freeze this membership", "record a payment" — has to answer "on what?", and the answer
 * is either a second search step or a form, at which point it is a worse version of the screen that
 * already exists. Worse, an action fired from a palette skips the surrounding context that makes it
 * safe to confirm. The one exception this app genuinely wants — jumping to a specific member or
 * invoice — is served by the search results the palette shows above these commands, which ARE
 * specific records. So the palette navigates, and the destination screen acts.
 */
export function buildCommands(hasPermission: (code: string) => boolean, roles: readonly string[]): Command[] {
  return NAV_MODULES.filter(
    (m) => hasPermission(m.permission) && (!m.requiresRole || roles.includes(m.requiresRole)),
  ).map((m) => ({
    id: `nav:${m.key}`,
    label: m.label,
    // The sidebar's own section, so the palette's grouping and the sidebar's agree on sight.
    group: m.section,
    icon: m.icon,
    route: m.path,
    keywords: [m.key],
  }))
}

/**
 * Substring match over the label and the hidden keywords. Deliberately not fuzzy: fuzzy matching
 * earns its keep over hundreds of commands, and over this many it mostly produces surprising hits
 * that erode trust in the first result being the right one.
 */
export function filterCommands(commands: Command[], term: string): Command[] {
  const needle = term.trim().toLowerCase()
  if (!needle) {
    return commands
  }
  return commands.filter(
    (c) => c.label.toLowerCase().includes(needle) || c.keywords?.some((k) => k.toLowerCase().includes(needle)),
  )
}
