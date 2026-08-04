import { ChevronLeft, ChevronRight } from 'lucide-react'

import { Button } from '@/components/ui/button'

interface PaginationProps {
  page: number
  totalPages: number
  totalCount: number
  hasPreviousPage: boolean
  hasNextPage: boolean
  onPageChange: (page: number) => void
  itemLabel?: string
}

export function Pagination({ page, totalPages, totalCount, hasPreviousPage, hasNextPage, onPageChange, itemLabel = 'total' }: PaginationProps) {
  if (totalCount === 0) {
    return null
  }

  return (
    <div className="flex items-center justify-between text-sm text-muted-foreground">
      <span>
        Page {page} of {totalPages || 1} · {totalCount.toLocaleString()} {itemLabel}
      </span>
      <div className="flex gap-2">
        <Button size="sm" variant="outline" disabled={!hasPreviousPage} onClick={() => onPageChange(page - 1)}>
          <ChevronLeft />
          Previous
        </Button>
        <Button size="sm" variant="outline" disabled={!hasNextPage} onClick={() => onPageChange(page + 1)}>
          Next
          <ChevronRight />
        </Button>
      </div>
    </div>
  )
}
