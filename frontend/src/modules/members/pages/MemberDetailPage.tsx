import { Link, useParams } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'

import { MemberDetailPanel } from '@/modules/members/components/MemberDetailPanel'

/**
 * The full-page half of the master-detail screen. The redesign fuses list and detail into one view,
 * but this route stays: it's what a deep link, a bookmark, a new-tab click and every screen too
 * narrow for a 392px rail all land on. It renders the exact same panel the rail does, so the two
 * surfaces can't drift — there is one member detail in this app, shown in two places.
 */
export default function MemberDetailPage() {
  const { id } = useParams<{ id: string }>()

  if (!id) {
    return null
  }

  return (
    <div className="mx-auto w-full max-w-2xl space-y-4">
      <Link to="/members" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="size-4" />
        Back to members
      </Link>

      <MemberDetailPanel memberId={id} variant="page" />
    </div>
  )
}
