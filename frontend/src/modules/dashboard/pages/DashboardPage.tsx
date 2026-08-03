import { DollarSign, Wallet, Users, UserPlus, CalendarClock, QrCode } from 'lucide-react'

import { Skeleton } from '@/components/ui/skeleton'
import { StatCard } from '@/shared/components/StatCard'
import { useAuthStore } from '@/stores/authStore'
import { useDashboardHub } from '@/shared/hooks/useDashboardHub'
import { useDashboardSummary } from '@/modules/dashboard/api/dashboardApi'

const currency = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

export default function DashboardPage() {
  const user = useAuthStore((s) => s.user)
  const { data, isLoading } = useDashboardSummary()
  useDashboardHub()

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Welcome back, {user?.firstName}</h1>
        <p className="text-sm text-muted-foreground">Here's what's happening at Titan Fitness today.</p>
      </div>

      {isLoading || !data ? (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-28 w-full" />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <StatCard label="Today's Revenue" value={currency.format(data.todayRevenue)} icon={DollarSign} tone="success" />
          <StatCard label="Cash Collected Today" value={currency.format(data.todayCashCollected)} icon={Wallet} />
          <StatCard label="Active Members" value={data.activeMembersCount.toLocaleString()} icon={Users} />
          <StatCard label="New Members This Month" value={data.newMembersThisMonthCount.toLocaleString()} icon={UserPlus} tone="success" />
          <StatCard
            label="Expiring in 7 Days"
            value={data.expiringMembershipsNext7DaysCount.toLocaleString()}
            icon={CalendarClock}
            tone={data.expiringMembershipsNext7DaysCount > 0 ? 'warning' : 'default'}
          />
          <StatCard label="Check-ins Today" value={data.todayAttendanceCount.toLocaleString()} icon={QrCode} />
        </div>
      )}

      <div className="rounded-lg border border-dashed p-6 text-center text-sm text-muted-foreground">
        Trainer schedule, equipment alerts, maintenance reminders, and inventory alerts widgets arrive with the
        Trainers/Equipment/Maintenance/Inventory modules (Wave 2).
      </div>
    </div>
  )
}
