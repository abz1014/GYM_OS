import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Loader2, Upload } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useImportEntitySchemas, useUploadImportJob, type ImportEntityType } from '@/modules/migration/api/migrationApi'

export function UploadImportDialog() {
  const [open, setOpen] = useState(false)
  const [entityType, setEntityType] = useState<ImportEntityType | ''>('')
  const [file, setFile] = useState<File | null>(null)

  const { data: schemas } = useImportEntitySchemas()
  const uploadJob = useUploadImportJob()
  const navigate = useNavigate()

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!entityType || !file) {
      toast.error('Select an entity type and a CSV file.')
      return
    }

    uploadJob.mutate(
      { entityType, file },
      {
        onSuccess: (job) => {
          toast.success(`Uploaded ${job.totalRows} row(s).`)
          setOpen(false)
          setFile(null)
          setEntityType('')
          navigate(`/migration/${job.id}`)
        },
        onError: () => toast.error('Could not upload the file.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <Upload />
          New Import
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Import from CSV</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label>What are you importing?</Label>
            <Select value={entityType} onValueChange={(v) => setEntityType(v as ImportEntityType)}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Select entity type" />
              </SelectTrigger>
              <SelectContent>
                {schemas?.map((schema) => (
                  <SelectItem key={schema.entityType} value={schema.entityType}>
                    {schema.entityType}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              Membership, Attendance, and Payment imports aren't supported yet — they reference existing records
              rather than creating standalone ones.
            </p>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="importFile">CSV file</Label>
            <input
              id="importFile"
              type="file"
              accept=".csv,text/csv"
              required
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
              className="block w-full text-sm file:mr-3 file:rounded-md file:border file:bg-background file:px-3 file:py-1.5 file:text-sm"
            />
          </div>
          <DialogFooter>
            <Button type="submit" disabled={uploadJob.isPending}>
              {uploadJob.isPending && <Loader2 className="size-4 animate-spin" />}
              Upload
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
