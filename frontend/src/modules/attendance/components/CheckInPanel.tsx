import { useState } from 'react'
import { QrCode } from 'lucide-react'
import { toast } from 'sonner'

import { Input } from '@/components/ui/input'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useMembersList } from '@/modules/members/api/membersApi'
import { useCheckIn } from '@/modules/attendance/api/attendanceApi'
import { useUiStore } from '@/stores/uiStore'

export function CheckInPanel() {
  const [searchTerm, setSearchTerm] = useState('')
  const branchId = useUiStore((s) => s.selectedBranchId)
  const checkIn = useCheckIn()

  const { data } = useMembersList({ searchTerm: searchTerm || undefined, status: 'Active', page: 1, pageSize: 6 })

  const handleCheckIn = (memberId: string, memberName: string) => {
    if (!branchId) {
      toast.error('Select a branch first.')
      return
    }

    checkIn.mutate(
      { memberId, branchId, method: 'QrSimulated' },
      {
        onSuccess: () => {
          toast.success(`${memberName} checked in.`)
          setSearchTerm('')
        },
        onError: () => toast.error('Check-in failed.'),
      }
    )
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <QrCode className="size-4" />
          QR Check-in (simulated)
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <Input
          placeholder="Search member by name to simulate a scan..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
        />
        {searchTerm && (
          <div className="divide-y rounded-md border">
            {data?.items.length === 0 && <p className="p-3 text-sm text-muted-foreground">No active members match.</p>}
            {data?.items.map((member) => (
              <button
                key={member.id}
                type="button"
                onClick={() => handleCheckIn(member.id, member.fullName)}
                disabled={checkIn.isPending}
                className="flex w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-accent"
              >
                <span>{member.fullName}</span>
                <span className="text-xs text-muted-foreground">{member.memberCode}</span>
              </button>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  )
}
