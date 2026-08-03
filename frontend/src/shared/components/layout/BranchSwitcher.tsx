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

  useEffect(() => {
    if (!selectedBranchId && branches && branches.length > 0) {
      setSelectedBranchId(branches[0].id)
    }
  }, [branches, selectedBranchId, setSelectedBranchId])

  if (!branches || branches.length === 0) {
    return null
  }

  return (
    <Select value={selectedBranchId ?? undefined} onValueChange={setSelectedBranchId}>
      <SelectTrigger size="sm" className="w-[180px]">
        <Building2 className="size-4" />
        <SelectValue placeholder="Select branch" />
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
