import { useState } from 'react'
import { Search } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Pagination } from '@/shared/components/Pagination'
import { useAttendanceHistory, useCheckOut, usePeakHours } from '@/modules/attendance/api/attendanceApi'
import { SimpleBarChart } from '@/modules/reports/components/SimpleBarChart'
import { useAuthStore } from '@/stores/authStore'
import { useUiStore } from '@/stores/uiStore'

const toDateOnlyString = (d: Date) => d.toISOString().slice(0, 10)

const EYEBROW = 'text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase'

/**
 * Nothing at all for a role that cannot check people out.
 *
 * POST /api/attendance/{id}/check-out requires attendance.check_in, and a Trainer holds
 * attendance.view without it — so this button was offered to precisely the role the kiosk sends
 * here, and pressing it 403'd with no error handler to say so: the row simply did not change. The
 * screen's other half was gated for this exact reason and this half was missed.
 *
 * The failure toast is the second half of the fix. A gate stops the wrong role seeing the button; it
 * does nothing for the right role when the request fails, and a check-out that silently does nothing
 * is how a member stays "in the building" all night.
 */
function CheckOutButton({ attendanceRecordId }: { attendanceRecordId: string }) {
  const canCheckOut = useAuthStore((s) => s.hasPermission)('attendance.check_in')
  const checkOut = useCheckOut()

  if (!canCheckOut) return null

  return (
    <Button
      size="sm"
      variant="outline"
      className="rounded-xl"
      disabled={checkOut.isPending}
      onClick={() =>
        checkOut.mutate(attendanceRecordId, {
          onError: () => toast.error("Couldn't check that member out."),
        })
      }
    >
      Check out
    </Button>
  )
}

function PeakHoursCard() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const toDate = new Date()
  const fromDate = new Date(toDate)
  fromDate.setDate(fromDate.getDate() - 29)

  const { data, isLoading } = usePeakHours({
    branchId,
    fromDate: toDateOnlyString(fromDate),
    toDate: toDateOnlyString(toDate),
  })

  return (
    <div className="rounded-3xl border border-border bg-card p-5">
      <p className={EYEBROW}>Peak hours · last 30 days</p>
      <div className="mt-4">
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <SimpleBarChart
            data={(data ?? []).map((b) => ({ label: `${b.hourOfDay}:00`, value: b.checkInCount }))}
            valueFormatter={(v) => `${v} check-in${v === 1 ? '' : 's'}`}
          />
        )}
      </div>
    </div>
  )
}

/**
 * Everything the front desk needs that isn't a scan: the visit log, the search over it, the
 * still-inside filter, and the check-out button that closes an open visit. This is the previous
 * Attendance page's content, unchanged in behaviour and moved behind the kiosk's second tab — the
 * scan screen is what the desk looks at all day, and the history is what it opens when something
 * needs correcting.
 */
export function AttendanceHistoryPanel() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  // Defaults to the one thing front desk actually needs to act on — who's still in the building
  // and needs checking out — instead of opening on an undifferentiated history dump.
  const [checkedInOnly, setCheckedInOnly] = useState(true)
  const [searchTerm, setSearchTerm] = useState('')
  const [page, setPage] = useState(1)
  const { data, isLoading } = useAttendanceHistory({
    branchId,
    checkedInOnly: checkedInOnly || undefined,
    searchTerm: searchTerm || undefined,
    page,
    pageSize: 20,
  })

  return (
    <div className="mx-auto w-full max-w-6xl space-y-6 p-6">
      <div className="grid grid-cols-1 gap-6 xl:grid-cols-[1fr_380px]">
        <div className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <div className="relative w-full max-w-xs">
              <Search className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                placeholder="Search member name…"
                className="h-11 rounded-xl bg-card pl-9"
                value={searchTerm}
                onChange={(e) => {
                  setSearchTerm(e.target.value)
                  setPage(1)
                }}
              />
            </div>
            <Button
              variant={checkedInOnly ? 'default' : 'outline'}
              className="h-11 rounded-xl font-bold"
              onClick={() => {
                setCheckedInOnly((v) => !v)
                setPage(1)
              }}
            >
              Currently checked in
            </Button>
          </div>

          {isLoading && (
            <div className="space-y-2">
              {Array.from({ length: 6 }).map((_, i) => (
                <Skeleton key={i} className="h-16 w-full rounded-2xl lg:h-10" />
              ))}
            </div>
          )}

          {!isLoading && data?.items.length === 0 && (
            <p className="rounded-2xl border border-border py-10 text-center text-sm text-muted-foreground">
              {checkedInOnly ? 'No one is currently checked in.' : 'No attendance records match.'}
            </p>
          )}

          {!isLoading && data && data.items.length > 0 && (
            <>
              {/* Mobile: card list — check-in/check-out datetimes are long strings that clip
                  badly in a table on a phone screen. */}
              <div className="space-y-2 lg:hidden">
                {data.items.map((record) => (
                  <div key={record.id} className="space-y-2 rounded-panel border border-border bg-card p-4">
                    <div className="flex items-center justify-between gap-2">
                      <p className="truncate font-medium">{record.memberName}</p>
                      <Badge variant="outline" className="shrink-0">
                        {record.method === 'QrSimulated' ? 'QR' : 'Manual'}
                      </Badge>
                    </div>
                    <div className="text-sm text-muted-foreground tabular-nums">
                      <p>In: {new Date(record.checkInAt).toLocaleString()}</p>
                      <p>Out: {record.checkOutAt ? new Date(record.checkOutAt).toLocaleString() : '—'}</p>
                    </div>
                    {!record.checkOutAt && <CheckOutButton attendanceRecordId={record.id} />}
                  </div>
                ))}
              </div>

              {/* Desktop / tablet: full table */}
              <div className="hidden overflow-hidden rounded-panel border border-border bg-card lg:block">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Member</TableHead>
                      <TableHead>Check-in</TableHead>
                      <TableHead>Check-out</TableHead>
                      <TableHead>Method</TableHead>
                      <TableHead></TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {data.items.map((record) => (
                      <TableRow key={record.id}>
                        <TableCell className="font-medium">{record.memberName}</TableCell>
                        <TableCell className="text-muted-foreground tabular-nums">
                          {new Date(record.checkInAt).toLocaleString()}
                        </TableCell>
                        <TableCell className="text-muted-foreground tabular-nums">
                          {record.checkOutAt ? new Date(record.checkOutAt).toLocaleString() : '—'}
                        </TableCell>
                        <TableCell>
                          <Badge variant="outline">{record.method === 'QrSimulated' ? 'QR' : 'Manual'}</Badge>
                        </TableCell>
                        <TableCell>{!record.checkOutAt && <CheckOutButton attendanceRecordId={record.id} />}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>

              <Pagination
                page={data.page}
                totalPages={data.totalPages}
                totalCount={data.totalCount}
                hasPreviousPage={data.hasPreviousPage}
                hasNextPage={data.hasNextPage}
                onPageChange={setPage}
                itemLabel="records"
              />
            </>
          )}
        </div>

        <PeakHoursCard />
      </div>
    </div>
  )
}
