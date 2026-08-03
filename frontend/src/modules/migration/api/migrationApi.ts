import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'
import type { PagedList } from '@/types/paging'

export type ImportEntityType =
  | 'Member'
  | 'Trainer'
  | 'Membership'
  | 'Equipment'
  | 'Attendance'
  | 'Inventory'
  | 'Payment'
  | 'Lead'
export type ImportStatus = 'Uploaded' | 'Parsing' | 'Validated' | 'Committing' | 'Completed' | 'Failed' | 'RolledBack'
export type ImportRowStatus = 'Pending' | 'Valid' | 'Invalid' | 'Committed' | 'Skipped'

export interface ImportJobListItem {
  id: string
  entityType: ImportEntityType
  fileName: string
  status: ImportStatus
  totalRows: number
  validRows: number
  duplicateRows: number
  errorRows: number
  createdAt: string
  committedAt: string | null
  rolledBackAt: string | null
}

export interface ImportFieldMapping {
  sourceColumnName: string
  targetFieldName: string
}

export interface ImportJobDetail extends ImportJobListItem {
  detectedColumns: string[]
  fieldMappings: ImportFieldMapping[]
}

export interface ImportRow {
  id: string
  rowNumber: number
  data: Record<string, string>
  status: ImportRowStatus
  validationErrors: string | null
  isDuplicate: boolean
  mappedEntityId: string | null
}

export interface ImportEntitySchema {
  entityType: ImportEntityType
  requiredFields: string[]
  optionalFields: string[]
}

export function useImportJobs() {
  return useQuery({
    queryKey: ['import-jobs'],
    queryFn: async () => (await apiClient.get<ImportJobListItem[]>('/api/migration/jobs')).data,
  })
}

export function useImportJob(id: string | undefined) {
  return useQuery({
    queryKey: ['import-job', id],
    queryFn: async () => (await apiClient.get<ImportJobDetail>(`/api/migration/jobs/${id}`)).data,
    enabled: !!id,
  })
}

export function useImportJobRows(id: string | undefined, page: number) {
  return useQuery({
    queryKey: ['import-job-rows', id, page],
    queryFn: async () =>
      (await apiClient.get<PagedList<ImportRow>>(`/api/migration/jobs/${id}/rows`, { params: { page, pageSize: 50 } })).data,
    enabled: !!id,
  })
}

export function useImportEntitySchemas() {
  return useQuery({
    queryKey: ['import-entity-schemas'],
    queryFn: async () => (await apiClient.get<ImportEntitySchema[]>('/api/migration/jobs/entity-schemas')).data,
  })
}

export function useUploadImportJob() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ entityType, file }: { entityType: ImportEntityType; file: File }) => {
      const formData = new FormData()
      formData.append('entityType', entityType)
      formData.append('file', file)
      return (await apiClient.post<ImportJobDetail>('/api/migration/jobs', formData)).data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['import-jobs'] }),
  })
}

export function useSetImportFieldMappings(jobId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (mappings: ImportFieldMapping[]) =>
      apiClient.put(`/api/migration/jobs/${jobId}/field-mappings`, { importJobId: jobId, mappings }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['import-job', jobId] }),
  })
}

export function useValidateImportJob(jobId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () => apiClient.post(`/api/migration/jobs/${jobId}/validate`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['import-job', jobId] })
      queryClient.invalidateQueries({ queryKey: ['import-job-rows', jobId] })
    },
  })
}

export function useCommitImportJob(jobId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (branchId: string) => apiClient.post(`/api/migration/jobs/${jobId}/commit`, { importJobId: jobId, branchId }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['import-job', jobId] })
      queryClient.invalidateQueries({ queryKey: ['import-job-rows', jobId] })
      queryClient.invalidateQueries({ queryKey: ['import-jobs'] })
    },
  })
}

export function useRollbackImportJob(jobId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () => apiClient.post(`/api/migration/jobs/${jobId}/rollback`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['import-job', jobId] })
      queryClient.invalidateQueries({ queryKey: ['import-job-rows', jobId] })
      queryClient.invalidateQueries({ queryKey: ['import-jobs'] })
    },
  })
}
