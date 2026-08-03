import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { useAttendanceHistory } from '@/modules/attendance/api/attendanceApi'
import { CheckInPanel } from '@/modules/attendance/components/CheckInPanel'
import { useUiStore } from '@/stores/uiStore'

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
        <div className="lg:col-span-1">
          <CheckInPanel />
        </div>

        <div className="lg:col-span-2 rounded-lg border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Member</TableHead>
                <TableHead>Check-in</TableHead>
                <TableHead>Check-out</TableHead>
                <TableHead>Method</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading &&
                Array.from({ length: 6 }).map((_, i) => (
                  <TableRow key={i}>
                    <TableCell colSpan={4}>
                      <Skeleton className="h-6 w-full" />
                    </TableCell>
                  </TableRow>
                ))}

              {!isLoading && data?.items.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} className="py-8 text-center text-muted-foreground">
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
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      </div>
    </div>
  )
}
