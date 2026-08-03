import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'
import type { PagedList } from '@/types/paging'

export type InvoiceStatus = 'Draft' | 'Issued' | 'PartiallyPaid' | 'Paid' | 'Overdue' | 'Cancelled' | 'Refunded'
export type PaymentMethod = 'Cash' | 'Card' | 'BankTransfer' | 'Other'

export interface InvoiceListItem {
  id: string
  invoiceNumber: string
  memberId: string
  memberName: string
  issueDate: string
  dueDate: string
  status: InvoiceStatus
  totalAmount: number
  amountPaid: number
  amountOutstanding: number
  currency: string
}

export interface InvoiceDetail extends InvoiceListItem {
  subtotal: number
  taxAmount: number
  discountAmount: number
  notes: string | null
  lines: { id: string; itemType: string; description: string; quantity: number; unitPrice: number; lineTotal: number }[]
  payments: { id: string; method: PaymentMethod; amount: number; paidAt: string; status: string }[]
}

export function useInvoicesList(params: { memberId?: string; status?: InvoiceStatus; page?: number; pageSize?: number }) {
  return useQuery({
    queryKey: ['invoices', params],
    queryFn: async () => (await apiClient.get<PagedList<InvoiceListItem>>('/api/invoices', { params })).data,
  })
}

export function useInvoice(id: string | undefined) {
  return useQuery({
    queryKey: ['invoice', id],
    queryFn: async () => (await apiClient.get<InvoiceDetail>(`/api/invoices/${id}`)).data,
    enabled: !!id,
  })
}

interface CreateInvoiceInput {
  memberId: string
  branchId: string
  issueDate: string
  dueDate: string
  taxAmount: number
  discountAmount: number
  currency: string
  notes?: string
  lines: { itemType: string; description: string; quantity: number; unitPrice: number }[]
}

export function useCreateInvoice() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateInvoiceInput) => (await apiClient.post<string>('/api/invoices', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['invoices'] }),
  })
}

export function useRecordPayment(invoiceId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { method: PaymentMethod; amount: number }) =>
      (await apiClient.post(`/api/invoices/${invoiceId}/payments`, input)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['invoice', invoiceId] })
      queryClient.invalidateQueries({ queryKey: ['invoices'] })
    },
  })
}
