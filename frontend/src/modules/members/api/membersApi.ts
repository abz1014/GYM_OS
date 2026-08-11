import { useMutation, useQueries, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'
import type { PagedList } from '@/types/paging'

export type MemberStatus = 'Active' | 'Frozen' | 'Expired' | 'Cancelled'

/**
 * Every status the domain has — MemberStatus in the backend is a four-value enum and the list
 * endpoint filters on exactly these. The redesign's "Expiring" and "At risk" tabs are not in this
 * list because they are not statuses: nothing stores them and `/api/members` cannot filter by them.
 * See MembersListPage for what was built instead.
 */
export const MEMBER_STATUSES: readonly MemberStatus[] = ['Active', 'Frozen', 'Expired', 'Cancelled']

export interface MemberListItem {
  id: string
  memberCode: string
  fullName: string
  email: string
  phone: string | null
  profilePhotoUrl: string | null
  status: MemberStatus
  joinDate: string
}

export interface MemberMembership {
  id: string
  membershipPlanId: string
  membershipPlanName: string
  startDate: string
  endDate: string
  status: string
  autoRenew: boolean
  freezeStartDate: string | null
  freezeEndDate: string | null
  pricePaid: number
  currency: string
  invoiceId: string | null
  cancellationReason: string | null
}

export interface MemberDetail extends MemberListItem {
  firstName: string
  lastName: string
  dateOfBirth: string | null
  gender: string | null
  address: string | null
  qrCodeToken: string
  branchId: string
  referredByMemberId: string | null
  referredByName: string | null
  /** The active coaching pairing, or null when the member trains on their own. */
  assignedTrainerId: string | null
  assignedTrainerName: string | null
  emergencyContacts: { id: string; name: string; relationship: string; phone: string; email: string | null }[]
  medicalNotes: { id: string; note: string; recordedAt: string }[]
  measurements: { id: string; measuredOn: string; weightKg: number | null; bodyFatPercentage: number | null }[]
  progressPhotos: { id: string; photoUrl: string; takenAt: string }[]
  memberMemberships: MemberMembership[]
}

interface ListParams {
  searchTerm?: string
  status?: MemberStatus
  branchId?: string | null
  page?: number
  pageSize?: number
}

export function useMembersList(params: ListParams) {
  return useQuery({
    queryKey: ['members', params],
    queryFn: async () => (await apiClient.get<PagedList<MemberListItem>>('/api/members', { params })).data,
  })
}

export function useMember(id: string | undefined) {
  return useQuery({
    queryKey: ['member', id],
    queryFn: async () => (await apiClient.get<MemberDetail>(`/api/members/${id}`)).data,
    enabled: !!id,
  })
}

export type MemberStatusCounts = Partial<Record<MemberStatus, number>>

/**
 * A status whose count request failed is left absent rather than defaulted to 0 — a tab reading
 * "Frozen 0" when the answer is actually unknown is the one thing a count is not allowed to do.
 */
function combineStatusCounts(results: UseQueryResult<number>[]) {
  const counts: MemberStatusCounts = {}
  MEMBER_STATUSES.forEach((status, index) => {
    const value = results[index]?.data
    if (typeof value === 'number') {
      counts[status] = value
    }
  })
  return { counts, isPending: results.some((result) => result.isPending) }
}

/**
 * Live counts for the list's status tabs. There is no aggregate/counts endpoint, so each count is
 * the totalCount of the *same* filtered query its tab applies, asked for a single row — which is
 * the point rather than a workaround: a tab cannot show a number the table it opens then
 * contradicts. searchTerm is carried through for the same reason.
 */
export function useMemberStatusCounts(searchTerm?: string) {
  return useQueries({
    queries: MEMBER_STATUSES.map((status) => ({
      queryKey: ['members', 'status-count', status, searchTerm ?? ''],
      queryFn: async () =>
        (
          await apiClient.get<PagedList<MemberListItem>>('/api/members', {
            params: { searchTerm: searchTerm || undefined, status, page: 1, pageSize: 1 },
          })
        ).data.totalCount,
    })),
    combine: combineStatusCounts,
  })
}

export interface MemberVisit {
  id: string
  checkInAt: string
  checkOutAt: string | null
  method: 'QrSimulated' | 'Manual'
}

/**
 * A member's own check-in history, from the attendance endpoint's existing memberId filter — the
 * member DTO carries no visit data of its own.
 *
 * Keyed under 'attendance' deliberately: useCheckIn (attendance module) invalidates ['attendance'],
 * and React Query matches keys by prefix, so a check-in taken from the detail panel refreshes the
 * panel's own visit numbers without either module knowing about the other's keys.
 */
export interface MemberTimelineEntry {
  /** Visit | Workout | Nutrition | Invoice | Payment | Membership | Measurement | Coaching */
  kind: string
  at: string
  /**
   * The source recorded a DATE and no time, so `at` is midnight UTC standing in for one. Render it
   * without an hour, and read its calendar date off the UTC parts — otherwise a measurement claims
   * to have been taken at 5am, and lands on the wrong day for anyone west of UTC.
   */
  isDateOnly: boolean
  title: string
  detail: string | null
}

/**
 * One member's history across every module, newest first.
 *
 * The server decides what belongs in it: each source is gated on the caller's own module permission,
 * so a receptionist's timeline has no training in it and a trainer's has no money. Nothing is
 * filtered here — the client never learns what it was not sent.
 */
export function useMemberTimeline(memberId: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: ['members', 'timeline', memberId],
    queryFn: async () =>
      (await apiClient.get<MemberTimelineEntry[]>(`/api/members/${memberId}/timeline`)).data,
    enabled: enabled && !!memberId,
  })
}

export function useMemberVisits(memberId: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: ['attendance', 'member-visits', memberId],
    queryFn: async () =>
      (await apiClient.get<PagedList<MemberVisit>>('/api/attendance', { params: { memberId, page: 1, pageSize: 8 } })).data,
    enabled: enabled && !!memberId,
  })
}

/** Visit count over a window — pageSize 1 because only totalCount is wanted, never the rows. */
export function useMemberVisitCountSince(memberId: string | undefined, fromDate: string, enabled: boolean) {
  return useQuery({
    queryKey: ['attendance', 'member-visit-count', memberId, fromDate],
    queryFn: async () =>
      (await apiClient.get<PagedList<MemberVisit>>('/api/attendance', { params: { memberId, fromDate, page: 1, pageSize: 1 } }))
        .data.totalCount,
    enabled: enabled && !!memberId,
  })
}

export type MemberInvoiceStatus = 'Draft' | 'Issued' | 'PartiallyPaid' | 'Paid' | 'Overdue' | 'Cancelled' | 'Refunded'

export interface MemberInvoice {
  id: string
  invoiceNumber: string
  issueDate: string
  dueDate: string
  status: MemberInvoiceStatus
  totalAmount: number
  amountPaid: number
  amountOutstanding: number
  currency: string
}

export function useMemberInvoices(memberId: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: ['invoices', 'member', memberId],
    queryFn: async () =>
      (await apiClient.get<PagedList<MemberInvoice>>('/api/invoices', { params: { memberId, page: 1, pageSize: 10 } })).data,
    enabled: enabled && !!memberId,
  })
}

export interface Branch {
  id: string
  name: string
}

/** Same ['branches'] key the member dialogs already use, so the panel rides their cache. */
export function useBranches() {
  return useQuery({
    queryKey: ['branches'],
    queryFn: async () => (await apiClient.get<Branch[]>('/api/branches')).data,
  })
}

interface CreateMemberInput {
  firstName: string
  lastName: string
  email: string
  phone?: string
  branchId: string
  referredByMemberId?: string | null
}

export function useCreateMember() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateMemberInput) => (await apiClient.post<string>('/api/members', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['members'] }),
  })
}

export function useRenewMembership(memberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { membershipPlanId: string; startDate: string; autoRenew: boolean; couponCode?: string | null }) =>
      (await apiClient.post(`/api/members/${memberId}/memberships`, input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['member', memberId] }),
  })
}

export function useFreezeMembership(memberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { memberMembershipId: string; freezeStartDate: string; freezeEndDate: string }) =>
      apiClient.post(`/api/members/memberships/${input.memberMembershipId}/freeze`, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['member', memberId] }),
  })
}

export function useResumeMembership(memberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (memberMembershipId: string) => apiClient.post(`/api/members/memberships/${memberMembershipId}/resume`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['member', memberId] }),
  })
}

export function useCancelMembership(memberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ memberMembershipId, reason }: { memberMembershipId: string; reason?: string }) =>
      apiClient.post(`/api/members/memberships/${memberMembershipId}/cancel`, { reason: reason ?? null }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['member', memberId] }),
  })
}

export function useReactivateMembership(memberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (memberMembershipId: string) => apiClient.post(`/api/members/memberships/${memberMembershipId}/reactivate`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['member', memberId] }),
  })
}

export function useTransferMember(memberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { newBranchId: string }) => apiClient.post(`/api/members/${memberId}/transfer`, input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['member', memberId] })
      queryClient.invalidateQueries({ queryKey: ['members'] })
    },
  })
}

interface UpdateMemberInput {
  firstName: string
  lastName: string
  email: string
  phone?: string | null
  dateOfBirth?: string | null
  gender?: string | null
  address?: string | null
  profilePhotoUrl?: string | null
}

export function useUpdateMember(memberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: UpdateMemberInput) => apiClient.put(`/api/members/${memberId}`, { id: memberId, ...input }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['member', memberId] })
      queryClient.invalidateQueries({ queryKey: ['members'] })
    },
  })
}

export function useAddEmergencyContact(memberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { name: string; relationship: string; phone: string; email?: string | null }) =>
      (await apiClient.post(`/api/members/${memberId}/emergency-contacts`, input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['member', memberId] }),
  })
}

export function useAddMedicalNote(memberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { note: string; recordedByUserId?: string | null }) =>
      (await apiClient.post(`/api/members/${memberId}/medical-notes`, input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['member', memberId] }),
  })
}

interface AddMeasurementInput {
  measuredOn: string
  weightKg?: number | null
  bodyFatPercentage?: number | null
  chestCm?: number | null
  waistCm?: number | null
  hipCm?: number | null
  armCm?: number | null
  thighCm?: number | null
  notes?: string | null
}

export function useAddMeasurement(memberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: AddMeasurementInput) =>
      (await apiClient.post(`/api/members/${memberId}/measurements`, input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['member', memberId] }),
  })
}

export function useAddProgressPhoto(memberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { photoUrl: string; notes?: string | null }) =>
      (await apiClient.post(`/api/members/${memberId}/progress-photos`, input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['member', memberId] }),
  })
}

/** One member's outcome in a batch action. `reason` is present only on failure. */
export interface BatchMemberOutcome {
  memberId: string
  memberName: string | null
  succeeded: boolean
  reason: string | null
}

export interface BatchResult {
  requested: number
  succeeded: number
  failed: number
  outcomes: BatchMemberOutcome[]
}

/**
 * Batch freeze and batch note.
 *
 * Both resolve rather than throw on a partial result, because a partial result is the expected
 * outcome, not an error: `MaxFreezeDays` belongs to the plan, so any selection spanning two plans
 * will contain members the freeze is legal for and members it is not. The caller reads
 * `succeeded`/`failed` and shows the split — see the action bar.
 *
 * They invalidate the whole `['members']` list rather than patching rows: a freeze changes a
 * member's status, and guessing the resulting row state client-side is how a list ends up disagreeing
 * with the server about who is frozen.
 */
export function useBatchFreezeMemberships() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { memberIds: string[]; freezeStartDate: string; freezeEndDate: string }) =>
      (await apiClient.post<BatchResult>('/api/members/batch/freeze', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['members'] }),
  })
}

export function useBatchAddMemberNote() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { memberIds: string[]; note: string }) =>
      (await apiClient.post<BatchResult>('/api/members/batch/notes', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['members'] }),
  })
}
