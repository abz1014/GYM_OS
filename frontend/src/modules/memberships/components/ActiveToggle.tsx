import { Loader2, Power } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { useSetCouponActive, useSetDiscountActive } from '@/modules/memberships/api/membershipsApi'

/**
 * Turn a discount or a coupon off — the control neither one had.
 *
 * Phrased as "Stop / Reactivate" rather than "Delete": switching off ends every future redemption
 * while leaving the invoices that already used the code intact and explicable. A leaked or
 * mispriced code needed a developer before this existed.
 */
export function ActiveToggle({
  kind, id, label, isActive,
}: {
  kind: 'coupon' | 'discount'
  id: string
  /** What the confirmation names — the code, or the discount's name. */
  label: string
  isActive: boolean
}) {
  const setCoupon = useSetCouponActive()
  const setDiscount = useSetDiscountActive()
  const mutation = kind === 'coupon' ? setCoupon : setDiscount

  const apply = () => {
    if (isActive && !window.confirm(`Stop "${label}"? It can no longer be redeemed, and invoices that already used it are unchanged.`)) {
      return
    }

    mutation.mutate(
      { id, isActive: !isActive },
      {
        onSuccess: () =>
          toast.success(isActive ? `"${label}" can no longer be redeemed.` : `"${label}" is live again.`),
        onError: (err) =>
          toast.error(
            (err as { response?: { data?: { title?: string } } })?.response?.data?.title
              ?? `Couldn't change "${label}".`,
          ),
      },
    )
  }

  return (
    <Button
      size="sm"
      variant="ghost"
      className="press h-7 shrink-0 px-2 text-xs"
      disabled={mutation.isPending}
      onClick={apply}
    >
      {mutation.isPending ? <Loader2 className="size-3.5 animate-spin" /> : <Power className="size-3.5" aria-hidden />}
      {isActive ? 'Stop' : 'Reactivate'}
    </Button>
  )
}
