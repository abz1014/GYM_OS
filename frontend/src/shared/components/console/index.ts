/**
 * The staff console's shared visual vocabulary.
 *
 * The redesign specified three staff screens (dashboard, members, front desk) out of the fifteen the
 * console actually has. These are the pieces those three screens had in common, pulled out so the
 * remaining twelve inherit the same language instead of each re-deriving it from a screenshot.
 */
export { PageHeader } from './PageHeader'
export { StatTile, type StatTone, type StatRailTone } from './StatTile'
export { FilterTabs, type FilterTab } from './FilterTabs'
export { SearchField } from './SearchField'
export { ListError, ListEmpty, ListSkeleton } from './ListStates'
export { SEVERITY_ROW, type SeverityTone } from './severityRows'
