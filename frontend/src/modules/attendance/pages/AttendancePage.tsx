import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
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
  const { data, isLoading } = useAttendanceHistory({ branchId, page: 1, pageSize: 30 })

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Attendance</h1>
        <p className="text-sm text-muted-foreground">Simulated QR check-in and visit history.</p>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-1">
          <CheckInPanel />
          <PeakHoursCard />
        </div>

        <div className="lg:col-span-2 rounded-lg border">
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
              {isLoading &&
                Array.from({ length: 6 }).map((_, i) => (
                  <TableRow key={i}>
                    <TableCell colSpan={5}>
                      <Skeleton className="h-6 w-full" />
                    </TableCell>
                  </TableRow>
                ))}

              {!isLoading && data?.items.length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} className="py-8 text-center text-muted-foreground">
                    No attendance records yet.
                  </TableCell>
                </TableRow>
              )}

              {data?.items.map((record) => (
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
      </div>
    </div>
  )
}
