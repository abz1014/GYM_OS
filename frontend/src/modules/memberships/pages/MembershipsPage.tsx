import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useCoupons, useDiscounts, useMembershipPlans } from '@/modules/memberships/api/membershipsApi'
import { CreateCouponDialog } from '@/modules/memberships/components/CreateCouponDialog'
import { CreateDiscountDialog } from '@/modules/memberships/components/CreateDiscountDialog'
import { CreatePlanDialog } from '@/modules/memberships/components/CreatePlanDialog'

function DiscountsAndCoupons() {
  const { data: plans } = useMembershipPlans(true)
  const { data: discounts, isLoading: discountsLoading } = useDiscounts(true)
  const { data: coupons, isLoading: couponsLoading } = useCoupons(true)

  const planName = (id: string | null) => (id ? plans?.find((p) => p.id === id)?.name : undefined)

  return (
    <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="font-medium">Discounts</h2>
          <CreateDiscountDialog />
        </div>
        {discountsLoading && <Skeleton className="h-24 w-full" />}
        {discounts?.length === 0 && !discountsLoading && <p className="text-sm text-muted-foreground">No discounts yet.</p>}
        <div className="space-y-2">
          {discounts?.map((d) => (
            <Card key={d.id} className={!d.isActive ? 'opacity-60' : undefined}>
              <CardContent className="space-y-1 p-3">
                <div className="flex items-center justify-between">
                  <p className="font-medium">{d.name}</p>
                  <span className="text-sm font-semibold">{d.type === 'Percentage' ? `${d.value}%` : `$${d.value}`}</span>
                </div>
                <div className="flex flex-wrap items-center gap-1 text-xs text-muted-foreground">
                  <Badge variant="outline">{planName(d.membershipPlanId) ?? 'All plans'}</Badge>
                  {(d.validFrom || d.validTo) && (
                    <span>
                      {d.validFrom ? new Date(d.validFrom).toLocaleDateString() : '—'} →{' '}
                      {d.validTo ? new Date(d.validTo).toLocaleDateString() : 'no end date'}
                    </span>
                  )}
                  {!d.isActive && <Badge variant="secondary">Inactive</Badge>}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      </div>

      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="font-medium">Coupons</h2>
          <CreateCouponDialog />
        </div>
        {couponsLoading && <Skeleton className="h-24 w-full" />}
        {coupons?.length === 0 && !couponsLoading && <p className="text-sm text-muted-foreground">No coupons yet.</p>}
        <div className="space-y-2">
          {coupons?.map((c) => (
            <Card key={c.id} className={!c.isActive ? 'opacity-60' : undefined}>
              <CardContent className="space-y-1 p-3">
                <div className="flex items-center justify-between">
                  <p className="font-mono font-medium">{c.code}</p>
                  <span className="text-sm text-muted-foreground">
                    {c.timesRedeemed}
                    {c.maxRedemptions ? ` / ${c.maxRedemptions}` : ''} redeemed
                  </span>
                </div>
                <div className="flex flex-wrap items-center gap-1 text-xs text-muted-foreground">
                  {(c.validFrom || c.validTo) && (
                    <span>
                      {c.validFrom ? new Date(c.validFrom).toLocaleDateString() : '—'} →{' '}
                      {c.validTo ? new Date(c.validTo).toLocaleDateString() : 'no end date'}
                    </span>
                  )}
                  {!c.isActive && <Badge variant="secondary">Inactive</Badge>}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      </div>
    </div>
  )
}

export default function MembershipsPage() {
  const { data: plans, isLoading } = useMembershipPlans(true)

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Memberships</h1>
        <p className="text-sm text-muted-foreground">Manage plans, discounts, and coupons.</p>
      </div>

      <Tabs defaultValue="plans">
        <TabsList>
          <TabsTrigger value="plans">Plans</TabsTrigger>
          <TabsTrigger value="discounts">Discounts &amp; Coupons</TabsTrigger>
        </TabsList>

        <TabsContent value="plans" className="space-y-4">
          <div className="flex justify-end">
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
        </TabsContent>

        <TabsContent value="discounts">
          <DiscountsAndCoupons />
        </TabsContent>
      </Tabs>
    </div>
  )
}
