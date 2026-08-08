import { useState } from 'react'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useImportJobRows, type ImportRowStatus } from '@/modules/migration/api/migrationApi'

function statusVariant(status: ImportRowStatus) {
  if (status === 'Committed' || status === 'Valid') return 'default' as const
  if (status === 'Invalid') return 'destructive' as const
  if (status === 'Skipped') return 'secondary' as const
  return 'outline' as const
}

export function ImportRowsTable({ jobId }: { jobId: string }) {
  const [page, setPage] = useState(1)
  const { data, isLoading } = useImportJobRows(jobId, page)

  if (isLoading) {
    return <Skeleton className="h-64 w-full" />
  }

  if (!data) {
    return null
  }

  const columns = data.items[0] ? Object.keys(data.items[0].data) : []

  return (
    <div className="space-y-2">
      <div className="overflow-x-auto rounded-panel border border-border bg-card edge-light-soft">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>#</TableHead>
              {columns.map((col) => (
                <TableHead key={col}>{col}</TableHead>
              ))}
              <TableHead>Status</TableHead>
              <TableHead>Notes</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {data.items.map((row) => (
              <TableRow key={row.id}>
                <TableCell className="text-muted-foreground">{row.rowNumber}</TableCell>
                {columns.map((col) => (
                  <TableCell key={col} className="max-w-[160px] truncate">
                    {row.data[col]}
                  </TableCell>
                ))}
                <TableCell>
                  <Badge variant={statusVariant(row.status)}>{row.status}</Badge>
                  {row.isDuplicate && (
                    <Badge variant="secondary" className="ml-1">
                      Duplicate
                    </Badge>
                  )}
                </TableCell>
                <TableCell className="max-w-[200px] truncate text-xs text-muted-foreground">{row.validationErrors}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {data.totalPages > 1 && (
        <div className="flex items-center justify-end gap-2">
          <Button size="sm" variant="outline" disabled={!data.hasPreviousPage} onClick={() => setPage((p) => p - 1)}>
            Previous
          </Button>
          <span className="text-sm text-muted-foreground">
            Page {data.page} of {data.totalPages}
          </span>
          <Button size="sm" variant="outline" disabled={!data.hasNextPage} onClick={() => setPage((p) => p + 1)}>
            Next
          </Button>
        </div>
      )}
    </div>
  )
}
