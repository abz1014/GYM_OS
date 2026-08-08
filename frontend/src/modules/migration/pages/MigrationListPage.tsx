import { useNavigate } from 'react-router-dom'

import { Badge } from '@/components/ui/badge'
import { ListEmpty, ListError, ListSkeleton, PageHeader } from '@/shared/components/console'
import { useImportJobs, type ImportStatus } from '@/modules/migration/api/migrationApi'
import { UploadImportDialog } from '@/modules/migration/components/UploadImportDialog'

function statusVariant(status: ImportStatus) {
  if (status === 'Completed') return 'default' as const
  if (status === 'Failed') return 'destructive' as const
  if (status === 'RolledBack') return 'secondary' as const
  return 'outline' as const
}

/** "RolledBack" is an enum name; a person reading a list of their own imports should see English. */
const statusLabel = (status: ImportStatus) => (status === 'RolledBack' ? 'Rolled back' : status)

export default function MigrationListPage() {
  const jobsQuery = useImportJobs()
  const jobs = jobsQuery.data
  const navigate = useNavigate()

  return (
    <div className="space-y-4">
      <PageHeader
        title="Migration Center"
        description="Bulk-import Members, Trainers, Memberships, Equipment, Attendance, Inventory, Payments, and Leads from CSV."
        actions={<UploadImportDialog />}
      />

      {jobsQuery.isError && (
        <ListError
          message="We couldn't load your imports"
          onRetry={() => jobsQuery.refetch()}
          isRetrying={jobsQuery.isFetching}
        />
      )}

      {jobsQuery.isLoading && <ListSkeleton rows={4} className="h-20 w-full rounded-2xl" />}

      {jobs?.length === 0 && !jobsQuery.isLoading && (
        <ListEmpty message="No imports yet." hint="Upload a CSV to bring existing records into GymOS." />
      )}

      <div className="space-y-2">
        {jobs?.map((job) => (
          <button
            key={job.id}
            type="button"
            onClick={() => navigate(`/migration/${job.id}`)}
            className="press flex w-full flex-wrap items-center justify-between gap-2 rounded-panel border border-border bg-card p-4 text-left edge-light-soft transition-colors hover:bg-accent/50"
          >
            <div className="min-w-0">
              <p className="truncate font-medium">{job.fileName}</p>
              <p className="text-sm text-muted-foreground">
                {job.entityType} · <span className="tabular-nums">{job.totalRows.toLocaleString()}</span> row(s) ·{' '}
                <span className="tabular-nums">{new Date(job.createdAt).toLocaleString()}</span>
              </p>
            </div>
            <div className="flex items-center gap-2">
              {/* An unparsed upload has no row breakdown yet — every one of these counts would be a
                  zero standing in for "not counted", which reads as a clean file rather than an
                  unread one. */}
              {job.status !== 'Uploaded' && (
                <span className="text-sm text-muted-foreground tabular-nums">
                  {job.validRows} valid, {job.duplicateRows} dup, {job.errorRows} error
                </span>
              )}
              <Badge variant={statusVariant(job.status)}>{statusLabel(job.status)}</Badge>
            </div>
          </button>
        ))}
      </div>
    </div>
  )
}
