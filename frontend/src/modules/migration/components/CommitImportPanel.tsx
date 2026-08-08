import { useState } from 'react'
import { CheckCircle2, Loader2 } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useAdminBranchesList } from '@/modules/settings/api/settingsApi'
import { useCommitImportJob, type ImportJobDetail } from '@/modules/migration/api/migrationApi'

export function CommitImportPanel({ job }: { job: ImportJobDetail }) {
  const [branchId, setBranchId] = useState('')
  const { data: branches } = useAdminBranchesList(false)
  const commitJob = useCommitImportJob(job.id)

  // validRows and duplicateRows are disjoint counts (a duplicate row is never counted as valid),
  // so the number of rows that will actually be created is validRows on its own.
  const importableCount = job.validRows

  const handleCommit = () => {
    if (!branchId) {
      toast.error('Select a branch to import into.')
      return
    }

    commitJob.mutate(branchId, {
      onSuccess: () => toast.success('Import committed.'),
      onError: () => toast.error('Could not commit the import.'),
    })
  }

  return (
    <div className="space-y-4 rounded-2xl border border-border bg-card p-5 shadow-sm">
      <p className="text-sm">
        <span className="font-medium">{importableCount}</span> of {job.totalRows} row(s) will be created.{' '}
        {job.duplicateRows > 0 && `${job.duplicateRows} duplicate(s) and `}
        {job.errorRows > 0 && `${job.errorRows} invalid row(s) `}
        {(job.duplicateRows > 0 || job.errorRows > 0) && 'will be skipped.'}
      </p>
      <div className="max-w-xs space-y-1.5">
        <Label>Branch</Label>
        <Select value={branchId} onValueChange={setBranchId}>
          <SelectTrigger className="w-full">
            <SelectValue placeholder="Select branch" />
          </SelectTrigger>
          <SelectContent>
            {branches?.map((b) => (
              <SelectItem key={b.id} value={b.id}>
                {b.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <Button onClick={handleCommit} disabled={commitJob.isPending || importableCount === 0}>
        {commitJob.isPending ? <Loader2 className="size-4 animate-spin" /> : <CheckCircle2 />}
        Commit Import
      </Button>
    </div>
  )
}
