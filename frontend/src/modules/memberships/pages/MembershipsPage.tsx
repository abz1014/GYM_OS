import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { useMembershipPlans } from '@/modules/memberships/api/membershipsApi'
import { CreatePlanDialog } from '@/modules/memberships/components/CreatePlanDialog'

export default function MembershipsPage() {
  const { data: plans, isLoading } = useMembershipPlans(true)

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Membership Plans</h1>
          <p className="text-sm text-muted-foreground">Manage the plans members can subscribe to.</p>
        </div>
        <CreatePlanDialog />
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-40 w-full" />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {plans?.map((plan) => (
            <Card key={plan.id} className={!plan.isActive ? 'opacity-60' : undefined}>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <CardTitle>{plan.name}</CardTitle>
                  <Badge variant="outline">{plan.type}</Badge>
                </div>
              </CardHeader>
              <CardContent className="space-y-2">
                <p className="text-2xl font-semibold">
                  {plan.price.toLocaleString('en-US', { style: 'currency', currency: plan.currency })}
                  <span className="text-sm font-normal text-muted-foreground"> / {plan.durationDays} days</span>
                </p>
                {plan.description && <p className="text-sm text-muted-foreground">{plan.description}</p>}
                <p className="text-xs text-muted-foreground">
                  Freeze allowance: {plan.maxFreezeDays > 0 ? `${plan.maxFreezeDays} days` : 'Not allowed'}
                </p>
                {!plan.isActive && (
                  <Badge variant="secondary" className="mt-1">
                    Inactive
                  </Badge>
                )}
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}
