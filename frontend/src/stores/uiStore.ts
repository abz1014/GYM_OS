import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface UiState {
  sidebarCollapsed: boolean
  selectedBranchId: string | null
  toggleSidebar: () => void
  setSelectedBranchId: (branchId: string | null) => void
}

export const useUiStore = create<UiState>()(
  persist(
    (set) => ({
      sidebarCollapsed: false,
      selectedBranchId: null,
      toggleSidebar: () => set((state) => ({ sidebarCollapsed: !state.sidebarCollapsed })),
      setSelectedBranchId: (branchId) => set({ selectedBranchId: branchId }),
    }),
    { name: 'gymos-ui' }
  )
)
