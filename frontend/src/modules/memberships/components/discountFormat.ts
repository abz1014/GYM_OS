import type { Discount } from '@/modules/memberships/api/membershipsApi'

/**
 * How a discount reads once it has been saved. It lives in its own file because the memberships
 * page and the coupon dialog both render this figure and it must not drift between them — and
 * because a helper exported from a component file costs that file its fast refresh.
 *
 * A fixed amount is shown without a currency symbol, which looks like an omission and isn't.
 * DiscountDto carries `value` and nothing else — unlike MembershipPlanDto, which carries a currency
 * per plan — and a discount with a null membershipPlanId applies across every plan, each free to
 * price in its own currency. There is therefore no single currency this figure is in. The number is
 * real; a "$" in front of it would be a guess, and the amount coming off a member's bill is the
 * last place in this app worth guessing.
 */
export function discountValueLabel(discount: Pick<Discount, 'type' | 'value'>) {
  return discount.type === 'Percentage'
    ? `${discount.value.toLocaleString()}% off`
    : `${discount.value.toLocaleString()} off`
}
