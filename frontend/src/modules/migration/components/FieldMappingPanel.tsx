import { useState } from 'react'
import { Loader2 } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import {
  useImportEntitySchemas,
  useSetImportFieldMappings,
  useValidateImportJob,
  type ImportJobDetail,
} from '@/modules/migration/api/migrationApi'

const SKIP = '__skip__'

export function FieldMappingPanel({ job }: { job: ImportJobDetail }) {
  const { data: schemas } = useImportEntitySchemas()
  const schema = schemas?.find((s) => s.entityType === job.entityType)

  const [mapping, setMapping] = useState<Record<string, string>>(() => {
    const initial: Record<string, string> = {}
    for (const col of job.detectedColumns) {
      const existing = job.fieldMappings.find((m) => m.sourceColumnName === col)
      initial[col] = existing?.targetFieldName ?? SKIP
    }
    return initial
  })

  const setMappings = useSetImportFieldMappings(job.id)
  const validateJob = useValidateImportJob(job.id)

  if (!schema) {
    return <p className="text-sm text-muted-foreground">Loading field schema…</p>
  }

  const allFields = [...schema.requiredFields, ...schema.optionalFields]
  const mappedTargets = new Set(Object.values(mapping).filter((v) => v !== SKIP))
  const missingRequired = schema.requiredFields.filter((f) => !mappedTargets.has(f))

  const handleSaveAndValidate = async () => {
    const mappings = Object.entries(mapping)
      .filter(([, target]) => target !== SKIP)
      .map(([sourceColumnName, targetFieldName]) => ({ sourceColumnName, targetFieldName }))

    if (missingRequired.length > 0) {
      toast.error(`Map all required fields: ${missingRequired.join(', ')}`)
      return
    }

    try {
      await setMappings.mutateAsync(mappings)
      await validateJob.mutateAsync()
      toast.success('Import validated.')
    } catch {
      toast.error('Could not validate the import.')
    }
  }

  return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground">
        Map each CSV column to a {job.entityType} field. Required fields:{' '}
        {schema.requiredFields.map((f) => (
          <Badge key={f} variant="outline" className="ml-1">
            {f}
          </Badge>
        ))}
      </p>

      <div className="space-y-2">
        {job.detectedColumns.map((col) => (
          <div key={col} className="flex items-center gap-3 rounded-xl border border-border p-2">
            <span className="w-48 truncate font-mono text-sm">{col}</span>
            <span className="text-muted-foreground">→</span>
            <Select value={mapping[col]} onValueChange={(v) => setMapping((prev) => ({ ...prev, [col]: v }))}>
              <SelectTrigger className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={SKIP}>Don't import</SelectItem>
                {allFields.map((field) => (
                  <SelectItem key={field} value={field}>
                    {field}
                    {schema.requiredFields.includes(field) ? ' (required)' : ''}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        ))}
      </div>

      <Button onClick={handleSaveAndValidate} disabled={setMappings.isPending || validateJob.isPending}>
        {(setMappings.isPending || validateJob.isPending) && <Loader2 className="size-4 animate-spin" />}
        Save Mappings &amp; Validate
      </Button>
    </div>
  )
}
