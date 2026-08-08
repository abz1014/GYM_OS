import { useParams, Link } from 'react-router-dom'
import { ArrowLeft, RotateCcw } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { ListError, PageHeader } from '@/shared/components/console'
import { useImportJob, useRollbackImportJob } from '@/modules/migration/api/migrationApi'
import { CommitImportPanel } from '@/modules/migration/components/CommitImportPanel'
import { FieldMappingPanel } from '@/modules/migration/components/FieldMappingPanel'
import { ImportRowsTable } from '@/modules/migration/components/ImportRowsTable'

export default function ImportJobDetailPage() {
  const { id } = useParams<{ id: string }>()
  const jobQuery = useImportJob(id)
  const job = jobQuery.data
  const rollbackJob = useRollbackImportJob(id ?? '')

  const backLink = (
    <Link to="/migration" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
      <ArrowLeft className="size-4" />
      Back to imports
    </Link>
  )

  // Failure and loading were one branch, so an import that 404'd or a dropped connection left the
  // skeleton pulsing with no way forward. Separating them costs nothing and gives staff the retry.
  if (jobQuery.isError) {
    return (
      <div className="space-y-6">
        {backLink}
        <ListError
          message="We couldn't load this import"
          onRetry={() => jobQuery.refetch()}
          isRetrying={jobQuery.isFetching}
        />
      </div>
    )
  }

  if (!job) {
    return (
      <div className="space-y-4">
        {backLink}
        <Skeleton className="h-10 w-48 rounded-2xl" />
        <Skeleton className="h-40 w-full rounded-2xl" />
      </div>
    )
  }

  const handleRollback = () => {
    rollbackJob.mutate(undefined, {
      onSuccess: () => toast.success('Import rolled back.'),
      onError: () => toast.error('Could not roll back the import.'),
    })
  }

  return (
    <div className="space-y-6">
      {backLink}

      <PageHeader
        eyebrow={job.entityType}
        title={
          <span className="flex flex-wrap items-center gap-3">
            <span className="break-all">{job.fileName}</span>
            <Badge className="align-middle">{job.status === 'RolledBack' ? 'Rolled back' : job.status}</Badge>
          </span>
        }
        description={
          <span>
            <span className="tabular-nums">{job.totalRows.toLocaleString()}</span> row(s) · Uploaded{' '}
            <span className="tabular-nums">{new Date(job.createdAt).toLocaleString()}</span>
          </span>
        }
        actions={
          job.status === 'Completed' && (
            <Button
              variant="destructive"
              size="sm"
              className="rounded-xl"
              disabled={rollbackJob.isPending}
              onClick={handleRollback}
            >
              <RotateCcw />
              Roll back
            </Button>
          )
        }
      />

      {job.status === 'Uploaded' && <FieldMappingPanel job={job} />}

      {job.status === 'Validated' && (
        <>
          <CommitImportPanel job={job} />
          <ImportRowsTable jobId={job.id} />
        </>
      )}

      {(job.status === 'Completed' || job.status === 'RolledBack') && (
        <>
          {job.status === 'RolledBack' && (
            <p className="text-sm text-muted-foreground">
              Rolled back <span className="tabular-nums">{job.rolledBackAt && new Date(job.rolledBackAt).toLocaleString()}</span>.
            </p>
          )}
          <ImportRowsTable jobId={job.id} />
        </>
      )}

      {job.status === 'Failed' && <p className="text-sm text-destructive">This import failed. Check the file and try again.</p>}
    </div>
  )
}
