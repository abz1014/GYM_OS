import { useNavigate } from 'react-router-dom'

import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useImportJobs, type ImportStatus } from '@/modules/migration/api/migrationApi'
import { UploadImportDialog } from '@/modules/migration/components/UploadImportDialog'

function statusVariant(status: ImportStatus) {
  if (status === 'Completed') return 'default' as const
  if (status === 'Failed') return 'destructive' as const
  if (status === 'RolledBack') return 'secondary' as const
  return 'outline' as const
}

export default function MigrationListPage() {
  const { data: jobs, isLoading } = useImportJobs()
  const navigate = useNavigate()

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Migration Center</h1>
          <p className="text-sm text-muted-foreground">
            Bulk-import Members, Trainers, Memberships, Equipment, Attendance, Inventory, Payments, and Leads from CSV.
          </p>
        </div>
        <UploadImportDialog />
      </div>

      {isLoading && (
        <div className="space-y-2">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-20 w-full" />
          ))}
        </div>
      )}

      {jobs?.length === 0 && !isLoading && <p className="text-sm text-muted-foreground">No imports yet.</p>}

      <div className="space-y-2">
        {jobs?.map((job) => (
          <Card key={job.id} className="cursor-pointer hover:bg-accent/50" onClick={() => navigate(`/migration/${job.id}`)}>
            <CardContent className="flex flex-wrap items-center justify-between gap-2 p-4">
              <div>
                <p className="font-medium">{job.fileName}</p>
                <p className="text-sm text-muted-foreground">
                  {job.entityType} · {job.totalRows} row(s) · {new Date(job.createdAt).toLocaleString()}
                </p>
              </div>
              <div className="flex items-center gap-2">
                {job.status !== 'Uploaded' && (
                  <span className="text-sm text-muted-foreground">
                    {job.validRows} valid, {job.duplicateRows} dup, {job.errorRows} error
                  </span>
                )}
                <Badge variant={statusVariant(job.status)}>{job.status}</Badge>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}
