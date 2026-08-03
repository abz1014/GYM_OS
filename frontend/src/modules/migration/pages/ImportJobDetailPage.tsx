import { useParams, Link } from 'react-router-dom'
import { ArrowLeft, RotateCcw } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useImportJob, useRollbackImportJob } from '@/modules/migration/api/migrationApi'
import { CommitImportPanel } from '@/modules/migration/components/CommitImportPanel'
import { FieldMappingPanel } from '@/modules/migration/components/FieldMappingPanel'
import { ImportRowsTable } from '@/modules/migration/components/ImportRowsTable'

export default function ImportJobDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data: job, isLoading } = useImportJob(id)
  const rollbackJob = useRollbackImportJob(id ?? '')

  if (isLoading || !job) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-40 w-full" />
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
      <Link to="/migration" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="size-4" />
        Back to imports
      </Link>

      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-xl font-semibold">{job.fileName}</h1>
            <Badge>{job.status}</Badge>
          </div>
          <p className="text-sm text-muted-foreground">
            {job.entityType} · {job.totalRows} row(s) · Uploaded {new Date(job.createdAt).toLocaleString()}
          </p>
        </div>
        {job.status === 'Completed' && (
          <Button variant="destructive" size="sm" disabled={rollbackJob.isPending} onClick={handleRollback}>
            <RotateCcw />
            Roll Back
          </Button>
        )}
      </div>

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
              Rolled back {job.rolledBackAt && new Date(job.rolledBackAt).toLocaleString()}.
            </p>
          )}
          <ImportRowsTable jobId={job.id} />
        </>
      )}

      {job.status === 'Failed' && <p className="text-sm text-destructive">This import failed. Check the file and try again.</p>}
    </div>
  )
}
