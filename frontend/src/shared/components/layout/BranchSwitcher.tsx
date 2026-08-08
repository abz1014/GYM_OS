import { useEffect } from 'react'
import { Building2, CloudOff, RotateCw } from 'lucide-react'

import { useUiStore } from '@/stores/uiStore'
import { useBranchesQuery } from '@/shared/hooks/useBranches'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

export function BranchSwitcher() {
  const selectedBranchId = useUiStore((s) => s.selectedBranchId)
  const setSelectedBranchId = useUiStore((s) => s.setSelectedBranchId)

  const branchesQuery = useBranchesQuery()
  const branches = branchesQuery.data

  // The selected branch is persisted in localStorage, so it outlives the data it points at: a
  // branch that gets deleted/deactivated, a user whose branch access is revoked, or (in dev) a
  // reseeded database all leave a stale id behind. Every branch-scoped endpoint then answers 403
  // and the whole app looks broken, with no in-app way for the user to recover — so validate the
  // stored id against the branches this user can actually reach and fall back to the first one.
  useEffect(() => {
    if (!branches || branches.length === 0) {
      return
    }

    const isStillAccessible = selectedBranchId !== null && branches.some((b) => b.id === selectedBranchId)

    if (!isStillAccessible) {
      setSelectedBranchId(branches[0].id)
    }
  }, [branches, selectedBranchId, setSelectedBranchId])

  /*
   * The failure is shown, not swallowed. Returning null here was the visible half of the console's
   * worst failure mode: nearly every staff query is gated on `enabled: !!branchId`, and nothing sets
   * that id except the effect above, so a failed /api/branches left Dashboard, Front desk and CRM
   * sitting on skeletons that could never resolve — the queries were never enabled, so they never
   * errored either. The switcher disappearing was the only clue, and an absent control looks like a
   * permission, not a fault.
   *
   * The retry is worth having here even though react-query owns the retrying: this is the one
   * request whose failure blocks the rest of the console, so a person needs a way to unblock it that
   * isn't a full page reload.
   */
  if (branchesQuery.isError) {
    return (
      <div className="flex items-center gap-2 rounded-xl border border-sidebar-border bg-sidebar-accent/40 px-3 py-2 text-xs text-sidebar-foreground/70">
        <CloudOff className="size-4 shrink-0" aria-hidden />
        <span className="min-w-0 flex-1 truncate">Branches unavailable</span>
        <button
          type="button"
          onClick={() => void branchesQuery.refetch()}
          disabled={branchesQuery.isFetching}
          aria-label="Retry loading branches"
          className="shrink-0 rounded-lg p-1 transition-colors hover:bg-sidebar-accent hover:text-sidebar-foreground"
        >
          <RotateCw className={branchesQuery.isFetching ? 'size-3.5 animate-spin' : 'size-3.5'} />
        </button>
      </div>
    )
  }

  if (!branches || branches.length === 0) {
    return null
  }

  const selectedBranchName = branches.find((b) => b.id === selectedBranchId)?.name

  return (
    <Select value={selectedBranchId ?? undefined} onValueChange={setSelectedBranchId}>
      <SelectTrigger size="sm" className="w-[120px] min-w-0 sm:w-[180px]">
        <Building2 className="size-4 shrink-0" />
        <SelectValue placeholder="Select branch">
          <span className="block min-w-0 truncate">{selectedBranchName}</span>
        </SelectValue>
      </SelectTrigger>
      <SelectContent>
        {branches.map((branch) => (
          <SelectItem key={branch.id} value={branch.id}>
            {branch.name}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
