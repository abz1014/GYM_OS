import { useState } from 'react'
import { Search } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Pagination } from '@/shared/components/Pagination'
import { useAttendanceHistory, useCheckOut, usePeakHours } from '@/modules/attendance/api/attendanceApi'
import { CheckInPanel } from '@/modules/attendance/components/CheckInPanel'
import { SimpleBarChart } from '@/modules/reports/components/SimpleBarChart'
import { useUiStore } from '@/stores/uiStore'

const toDateOnlyString = (d: Date) => d.toISOString().slice(0, 10)

function CheckOutButton({ attendanceRecordId }: { attendanceRecordId: string }) {
  const checkOut = useCheckOut()

  return (
    <Button size="sm" variant="outline" disabled={checkOut.isPending} onClick={() => checkOut.mutate(attendanceRecordId)}>
      Check Out
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
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Peak Hours (last 30 days)</CardTitle>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <SimpleBarChart
            data={(data ?? []).map((b) => ({ label: `${b.hourOfDay}:00`, value: b.checkInCount }))}
            valueFormatter={(v) => `${v} check-in${v === 1 ? '' : 's'}`}
          />
        )}
      </CardContent>
    </Card>
  )
}

export default function AttendancePage() {
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
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Attendance</h1>
        <p className="text-sm text-muted-foreground">Who's checked in right now, and the full visit history.</p>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-1">
          <CheckInPanel />
          <PeakHoursCard />
        </div>

        <div className="space-y-3 lg:col-span-2">
          <div className="flex flex-wrap items-center gap-2">
            <div className="relative w-full max-w-xs">
              <Search className="absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                placeholder="Search member name..."
                className="pl-8"
                value={searchTerm}
                onChange={(e) => {
                  setSearchTerm(e.target.value)
                  setPage(1)
                }}
              />
            </div>
            <Button
              variant={checkedInOnly ? 'default' : 'outline'}
              size="sm"
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
                <Skeleton key={i} className="h-16 w-full lg:h-10" />
              ))}
            </div>
          )}

          {!isLoading && data?.items.length === 0 && (
            <p className="rounded-lg border py-8 text-center text-sm text-muted-foreground">
              {checkedInOnly ? 'No one is currently checked in.' : 'No attendance records match.'}
            </p>
          )}

          {!isLoading && data && data.items.length > 0 && (
            <>
              {/* Mobile: card list — check-in/check-out datetimes are long strings that clip
                  badly in a table on a phone screen. */}
              <div className="space-y-2 lg:hidden">
                {data.items.map((record) => (
                  <div key={record.id} className="space-y-2 rounded-lg border bg-card p-3">
                    <div className="flex items-center justify-between gap-2">
                      <p className="truncate font-medium">{record.memberName}</p>
                      <Badge variant="outline" className="shrink-0">
                        {record.method === 'QrSimulated' ? 'QR' : 'Manual'}
                      </Badge>
                    </div>
                    <div className="text-sm text-muted-foreground">
                      <p>In: {new Date(record.checkInAt).toLocaleString()}</p>
                      <p>Out: {record.checkOutAt ? new Date(record.checkOutAt).toLocaleString() : '—'}</p>
                    </div>
                    {!record.checkOutAt && <CheckOutButton attendanceRecordId={record.id} />}
                  </div>
                ))}
              </div>

              {/* Desktop / tablet: full table */}
              <div className="hidden rounded-lg border lg:block">
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
                        <TableCell>{record.memberName}</TableCell>
                        <TableCell className="text-muted-foreground">{new Date(record.checkInAt).toLocaleString()}</TableCell>
                        <TableCell className="text-muted-foreground">
                          {record.checkOutAt ? new Date(record.checkOutAt).toLocaleString() : '—'}
                        </TableCell>
                        <TableCell>
                          <Badge variant="outline">{record.method === 'QrSimulated' ? 'QR' : 'Manual'}</Badge>
                        </TableCell>
                        <TableCell>
                          {!record.checkOutAt && <CheckOutButton attendanceRecordId={record.id} />}
                        </TableCell>
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
      </div>
    </div>
  )
}
