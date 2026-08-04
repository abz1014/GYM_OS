import { useEffect } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Building2 } from 'lucide-react'

import { apiClient } from '@/lib/apiClient'
import { useUiStore } from '@/stores/uiStore'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

interface Branch {
  id: string
  name: string
  city: string
}

export function BranchSwitcher() {
  const selectedBranchId = useUiStore((s) => s.selectedBranchId)
  const setSelectedBranchId = useUiStore((s) => s.setSelectedBranchId)

  const { data: branches } = useQuery({
    queryKey: ['branches'],
    queryFn: async () => (await apiClient.get<Branch[]>('/api/branches')).data,
  })

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
