import { Badge } from '@/components/ui/badge'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { cn } from '@/lib/utils'
import { ListEmpty, ListError, ListSkeleton, PageHeader } from '@/shared/components/console'
import { useCoupons, useDiscounts, useMembershipPlans } from '@/modules/memberships/api/membershipsApi'
import { EditPlanDialog } from '@/modules/memberships/components/EditPlanDialog'
import { ActiveToggle } from '@/modules/memberships/components/ActiveToggle'
import { CreateCouponDialog } from '@/modules/memberships/components/CreateCouponDialog'
import { CreateDiscountDialog } from '@/modules/memberships/components/CreateDiscountDialog'
import { CreatePlanDialog } from '@/modules/memberships/components/CreatePlanDialog'
import { discountValueLabel } from '@/modules/memberships/components/discountFormat'
import { useAuthStore } from '@/stores/authStore'
import { isStale } from '@/shared/lib/queryTrust'

/**
 * The console's tab row is an underline strip; shadcn's Tabs ships a pill sitting in a grey tray.
 * These two strings flatten it into the same shape `FilterTabs` draws.
 *
 * Radix stays underneath rather than being swapped for `FilterTabs` because these tabs genuinely
 * switch panels — plans and discounts are different content, not two filters over one list — and
 * that is exactly the case FilterTabs' own doc comment says it is not for.
 */
const TAB_LIST_CLASS =
  'h-auto w-full flex-wrap justify-start gap-5 rounded-none border-b border-border bg-transparent p-0'
const TAB_TRIGGER_CLASS =
  '-mb-px h-auto flex-none rounded-none border-0 border-b-2 border-transparent px-0 py-0 pb-2.5 text-muted-foreground shadow-none transition-colors hover:text-foreground data-[state=active]:border-foreground data-[state=active]:bg-transparent data-[state=active]:text-foreground data-[state=active]:shadow-none'

const money = (amount: number, code: string) => amount.toLocaleString('en-US', { style: 'currency', currency: code })

/** Both dates are optional on discounts and coupons alike, and "no window at all" is the common case. */
function validityLabel(validFrom: string | null, validTo: string | null) {
  if (!validFrom && !validTo) {
    return null
  }
  const start = validFrom ? new Date(validFrom).toLocaleDateString() : '—'
  const end = validTo ? new Date(validTo).toLocaleDateString() : 'no end date'
  return `${start} → ${end}`
}

/** The small-caps line that heads each list, with the real size of that list beside it. */
function SectionLabel({ children, count }: { children: string; count?: number }) {
  return (
    <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
      {children}
      {/*
        Counting the array is honest here in a way it would not be on a paginated screen: all three
        membership endpoints return the whole set — no page size, no server-side Take — so its
        length is the total rather than the size of one page.
      */}
      {count !== undefined && <span className="ml-2 tabular-nums">{count.toLocaleString()}</span>}
    </p>
  )
}

function PlansTab() {
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const plansQuery = useMembershipPlans(true)
  const plans = plansQuery.data

  // The catalogue reads on memberships.view — which is why a Receptionist and an Accountant can
  // open this screen at all — but creating a plan is memberships.manage_plans, which neither holds.
  const canManagePlans = hasPermission('memberships.manage_plans')

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <SectionLabel count={plans?.length}>Plan catalogue</SectionLabel>
        {canManagePlans && <CreatePlanDialog />}
      </div>

      {isStale(plansQuery) && (
        <ListError
          message="We couldn't load the plan catalogue"
          onRetry={() => plansQuery.refetch()}
          isRetrying={plansQuery.isFetching}
        />
      )}

      {/* One ListSkeleton per grid column rather than one tall stack, so the placeholders land in
          the same three columns the cards will occupy and nothing shifts sideways on arrival. */}
      {plansQuery.isLoading && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 3 }).map((_, i) => (
            <ListSkeleton key={i} rows={2} className="h-52 w-full rounded-2xl" />
          ))}
        </div>
      )}

      {!plansQuery.isLoading && plans?.length === 0 && (
        <ListEmpty
          message="No plans yet."
          hint={canManagePlans ? 'Create one and it becomes sellable at the front desk.' : undefined}
        />
      )}

      {/*
        The cards carry no "N members on this plan" and there is no revenue-per-plan tile above
        them, though a pricing screen is exactly where a manager wants both. MembershipPlanDto is
        the catalogue row and nothing else — no subscriber count — and no endpoint anywhere
        aggregates memberships or payments by plan, so either figure would mean one request per
        card and a number that still wouldn't say which period it covered.
      */}
      {plans && plans.length > 0 && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {plans.map((plan) => (
            <article
              key={plan.id}
              className={cn('rounded-panel border border-border bg-card p-5 edge-light-soft', !plan.isActive && 'opacity-60')}
            >
              <div className="flex items-start justify-between gap-2">
                <h3 className="min-w-0 font-display text-lg font-bold tracking-tight">{plan.name}</h3>
                <Badge variant="outline" className="shrink-0">
                  {plan.type}
                </Badge>
              </div>

              <p className="mt-4 font-display text-3xl leading-none font-black tracking-tight tabular-nums">
                {money(plan.price, plan.currency)}
              </p>
              <p className="mt-1.5 text-sm text-muted-foreground tabular-nums">for {plan.durationDays} days</p>

              {plan.description && <p className="mt-3 text-sm text-muted-foreground">{plan.description}</p>}

              <p className="mt-3 text-xs text-muted-foreground tabular-nums">
                Freeze allowance: {plan.maxFreezeDays > 0 ? `${plan.maxFreezeDays} days` : 'not allowed'}
              </p>

              <div className="mt-3 flex flex-wrap items-center gap-2">
                {/* "Retired" rather than "Inactive": the plan is not broken, it is simply no longer
                    sold, and everyone still on it has a perfectly live membership. */}
                {!plan.isActive && <Badge variant="secondary">Retired</Badge>}
                {canManagePlans && <EditPlanDialog plan={plan} />}
              </div>
            </article>
          ))}
        </div>
      )}
    </div>
  )
}

function DiscountsAndCoupons() {
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const plansQuery = useMembershipPlans(true)
  const discountsQuery = useDiscounts(true)
  const couponsQuery = useCoupons(true)

  /*
   * One permission for both lists: POST /api/memberships/discounts and POST
   * /api/memberships/coupons are each memberships.manage_discounts. A coupon is a code pointing at
   * a discount, and the API treats minting either as the same authority — NOT manage_plans, which
   * only covers the catalogue on the other tab.
   */
  const canManageDiscounts = hasPermission('memberships.manage_discounts')

  const plans = plansQuery.data
  const discounts = discountsQuery.data
  const coupons = couponsQuery.data

  const planName = (id: string | null) => (id ? plans?.find((p) => p.id === id)?.name : undefined)

  /*
   * A coupon is a code pointing at a discount, and the code on its own says nothing about what it
   * takes off. Both lists are already loaded here and both ask for inactive rows, so the discount
   * behind a coupon is always in hand — including an archived one, which is precisely the coupon a
   * receptionist is most likely to be querying.
   */
  const discountFor = (id: string) => discounts?.find((d) => d.id === id)

  return (
    <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
      <section className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <SectionLabel count={discounts?.length}>Discounts</SectionLabel>
          {canManageDiscounts && <CreateDiscountDialog />}
        </div>

        {isStale(discountsQuery) && (
          <ListError
            message="We couldn't load the discounts"
            onRetry={() => discountsQuery.refetch()}
            isRetrying={discountsQuery.isFetching}
          />
        )}

        {discountsQuery.isLoading && <ListSkeleton rows={3} className="h-20 w-full rounded-2xl" />}

        {!discountsQuery.isLoading && discounts?.length === 0 && <ListEmpty message="No discounts yet." />}

        <div className="space-y-2">
          {discounts?.map((discount) => {
            const validity = validityLabel(discount.validFrom, discount.validTo)
            return (
              <article
                key={discount.id}
                className={cn(
                  'rounded-panel border border-border bg-card p-4 edge-light-soft',
                  !discount.isActive && 'opacity-60',
                )}
              >
                <div className="flex items-start justify-between gap-3">
                  <p className="min-w-0 truncate font-medium">{discount.name}</p>
                  <span className="shrink-0 font-display text-base font-bold tracking-tight tabular-nums">
                    {discountValueLabel(discount)}
                  </span>
                </div>
                <div className="mt-2 flex flex-wrap items-center gap-1.5 text-xs text-muted-foreground">
                  <Badge variant="outline">{planName(discount.membershipPlanId) ?? 'All plans'}</Badge>
                  {validity && <span className="tabular-nums">{validity}</span>}
                  {!discount.isActive && <Badge variant="secondary">Inactive</Badge>}
                  {canManageDiscounts && (
                    <ActiveToggle kind="discount" id={discount.id} label={discount.name} isActive={discount.isActive} />
                  )}
                </div>
              </article>
            )
          })}
        </div>
      </section>

      <section className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <SectionLabel count={coupons?.length}>Coupons</SectionLabel>
          {canManageDiscounts && <CreateCouponDialog />}
        </div>

        {isStale(couponsQuery) && (
          <ListError
            message="We couldn't load the coupons"
            onRetry={() => couponsQuery.refetch()}
            isRetrying={couponsQuery.isFetching}
          />
        )}

        {couponsQuery.isLoading && <ListSkeleton rows={3} className="h-20 w-full rounded-2xl" />}

        {!couponsQuery.isLoading && coupons?.length === 0 && <ListEmpty message="No coupons yet." />}

        <div className="space-y-2">
          {coupons?.map((coupon) => {
            const validity = validityLabel(coupon.validFrom, coupon.validTo)
            const discount = discountFor(coupon.discountId)
            return (
              <article
                key={coupon.id}
                className={cn(
                  'rounded-panel border border-border bg-card p-4 edge-light-soft',
                  !coupon.isActive && 'opacity-60',
                )}
              >
                <div className="flex items-start justify-between gap-3">
                  <p className="min-w-0 truncate font-mono font-medium tracking-wide tabular-nums">{coupon.code}</p>
                  <span className="shrink-0 text-sm text-muted-foreground tabular-nums">
                    {/*
                      With no redemption cap set there is no denominator to show — "12 / ∞" and
                      "12 / 0" are both lies about a coupon that simply never runs out.
                    */}
                    {coupon.maxRedemptions
                      ? `${coupon.timesRedeemed} / ${coupon.maxRedemptions} used`
                      : `${coupon.timesRedeemed} used`}
                  </span>
                </div>
                <div className="mt-2 flex flex-wrap items-center gap-1.5 text-xs text-muted-foreground">
                  {discount && (
                    <Badge variant="outline">
                      {discount.name} · {discountValueLabel(discount)}
                    </Badge>
                  )}
                  {validity && <span className="tabular-nums">{validity}</span>}
                  {!coupon.isActive && <Badge variant="secondary">Inactive</Badge>}
                  {canManageDiscounts && (
                    <ActiveToggle kind="coupon" id={coupon.id} label={coupon.code} isActive={coupon.isActive} />
                  )}
                </div>
              </article>
            )
          })}
        </div>
      </section>
    </div>
  )
}

export default function MembershipsPage() {
  return (
    <div className="space-y-4">
      <PageHeader title="Memberships" description="What the gym sells, and what comes off the price." />

      <Tabs defaultValue="plans" className="gap-4">
        <TabsList className={TAB_LIST_CLASS}>
          <TabsTrigger className={TAB_TRIGGER_CLASS} value="plans">
            Plans
          </TabsTrigger>
          <TabsTrigger className={TAB_TRIGGER_CLASS} value="discounts">
            Discounts &amp; coupons
          </TabsTrigger>
        </TabsList>

        <TabsContent value="plans">
          <PlansTab />
        </TabsContent>
        <TabsContent value="discounts">
          <DiscountsAndCoupons />
        </TabsContent>
      </Tabs>
    </div>
  )
}
