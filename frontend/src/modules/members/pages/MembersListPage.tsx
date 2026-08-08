import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ArrowUpRight } from 'lucide-react'

import { Checkbox } from '@/components/ui/checkbox'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Pagination } from '@/shared/components/Pagination'
import { FilterTabs, ListEmpty, ListError, ListSkeleton, PageHeader, SearchField, type FilterTab } from '@/shared/components/console'
import { cn } from '@/lib/utils'
import { MEMBER_STATUSES, useMemberStatusCounts, useMembersList, type MemberStatus } from '@/modules/members/api/membersApi'
import { CreateMemberDialog } from '@/modules/members/components/CreateMemberDialog'
import { MemberDetailPanel } from '@/modules/members/components/MemberDetailPanel'
import { MembersActionBar } from '@/modules/members/components/MembersActionBar'
import { MemberStatusPill } from '@/modules/members/components/MemberStatusPill'
import { useAuthStore } from '@/stores/authStore'

const joinedFormat = new Intl.DateTimeFormat('en-US', { day: 'numeric', month: 'short', year: 'numeric' })

/** Tailwind's `xl` — the narrowest width at which a 392px rail and a readable table both fit. */
const RAIL_BREAKPOINT = '(min-width: 1280px)'

function useHasRoomForRail(): boolean {
  const [matches, setMatches] = useState(() => window.matchMedia(RAIL_BREAKPOINT).matches)

  useEffect(() => {
    const query = window.matchMedia(RAIL_BREAKPOINT)
    const onChange = (event: MediaQueryListEvent) => setMatches(event.matches)
    query.addEventListener('change', onChange)
    return () => query.removeEventListener('change', onChange)
  }, [])

  return matches
}

/**
 * The list fires one request per keystroke, and the status tabs multiply that by four. Holding the
 * term for a beat is what makes live counts affordable — without it, typing a nine-letter name
 * costs 45 requests to answer a question nobody asked until the last letter.
 */
function useDebounced<T>(value: T, delayMs: number): T {
  const [settled, setSettled] = useState(value)

  useEffect(() => {
    const timer = window.setTimeout(() => setSettled(value), delayMs)
    return () => window.clearTimeout(timer)
  }, [value, delayMs])

  return settled
}

/** Expired reads as "needs chasing" and Cancelled as "gone" — the counts say so before the pills do. */
const COUNT_TONE: Record<MemberStatus, string> = {
  Active: 'text-muted-foreground',
  Frozen: 'text-muted-foreground',
  Expired: 'text-warning',
  Cancelled: 'text-destructive',
}

/**
 * Members, as a master-detail screen: the directory on the left, the member you clicked held open
 * on the right instead of replacing the page. /members/:id is untouched and still renders the same
 * panel — a deep link, a bookmark, a middle-click, and every screen too narrow for a rail all keep
 * working, which is the only reason the rail could be added without taking anything away.
 *
 * Three things the redesign asked for are missing, each because the data behind them is:
 *
 * - **"Expiring" and "At risk" tabs.** MemberStatus is a four-value enum — Active, Frozen, Expired,
 *   Cancelled — and `/api/members` filters on exactly those. Neither "expiring" nor "at risk" is a
 *   status anything stores. A real at-risk *list* does exist (`/api/reports/at-risk-members`), but
 *   it's an unpaginated report behind reports.view with no matching filter on this endpoint, so a
 *   tab built on it could show a count it then couldn't page, search or sort. Four tabs that filter
 *   truthfully beat six that half-work.
 * - **Plan and Last visit columns.** MemberListItemDto carries neither, and there is no batch
 *   endpoint for either — filling them would mean one request per visible row. Both are real in the
 *   detail rail, where a single member is being asked about.
 * - **The Filters button and its active-count badge.** Status was the only filter this screen ever
 *   had, and it's now the tab strip. A button that opens nothing is decoration with a number on it.
 */
export default function MembersListPage() {
  const [searchTerm, setSearchTerm] = useState('')
  const [status, setStatus] = useState<MemberStatus | 'all'>('all')
  const [page, setPage] = useState(1)
  const [selectedMemberId, setSelectedMemberId] = useState<string | null>(null)
  /*
   * Batch selection, deliberately separate from `selectedMemberId`.
   *
   * They answer different questions — "whose record am I reading" versus "who am I about to act on"
   * — and merging them would mean opening a member's detail silently enlisted them in the next bulk
   * freeze. Keeping them apart is also why the checkbox stops propagation: ticking a box must never
   * also swap the rail to that member.
   */
  const [checkedIds, setCheckedIds] = useState<Set<string>>(new Set())
  const navigate = useNavigate()
  const hasPermission = useAuthStore((s) => s.hasPermission)

  const hasRoomForRail = useHasRoomForRail()
  const debouncedSearch = useDebounced(searchTerm.trim(), 250)

  const membersQuery = useMembersList({
    searchTerm: debouncedSearch || undefined,
    status: status === 'all' ? undefined : status,
    page,
    pageSize: 50,
  })
  const data = membersQuery.data
  const { counts } = useMemberStatusCounts(debouncedSearch || undefined)

  const pageIds = data?.items.map((m) => m.id) ?? []
  // "All" means all on THIS page, never the whole filtered set: the endpoint pages at 50 and the
  // batch endpoints cap at 100, so a header box that silently meant "all 300 matches" would promise
  // an action the server would refuse.
  const allOnPageChecked = pageIds.length > 0 && pageIds.every((id) => checkedIds.has(id))

  const toggleOne = (id: string) =>
    setCheckedIds((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  const togglePage = () =>
    setCheckedIds((prev) => {
      const next = new Set(prev)
      if (allOnPageChecked) pageIds.forEach((id) => next.delete(id))
      else pageIds.forEach((id) => next.add(id))
      return next
    })

  const openMember = (id: string) => {
    // Below xl there is no room for a rail, so a row click has to do what it always did.
    if (hasRoomForRail) {
      setSelectedMemberId(id)
    } else {
      navigate(`/members/${id}`)
    }
  }

  /**
   * "All" is the sum of the four, not the list's own totalCount: the moment a status tab is
   * selected that totalCount becomes the count of THAT status, and the All tab would sit there
   * quietly showing it. The four statuses are exhaustive — MemberStatus has no fifth value — so
   * their sum is the total, and it stays the total whichever tab is open. Absent if any one of the
   * four hasn't answered, since a sum missing a term is just a smaller wrong number.
   */
  const statusCounts = MEMBER_STATUSES.map((s) => counts[s])
  const allCount = statusCounts.every((count) => count !== undefined)
    ? statusCounts.reduce<number>((sum, count) => sum + (count ?? 0), 0)
    : undefined

  const tabs: FilterTab<MemberStatus | 'all'>[] = [
    { key: 'all', label: 'All', count: allCount },
    ...MEMBER_STATUSES.map((s) => ({ key: s, label: s, count: counts[s], countClassName: COUNT_TONE[s] })),
  ]

  return (
    <div className="flex gap-5">
      <div className="min-w-0 flex-1 space-y-4">
        {/* The design's "Export" sits beside the create button. Nothing in the API exports a member
            list — the export endpoints under /api/reports cover revenue, attendance, cohorts and
            at-risk members, never the directory itself. */}
        {/* POST /api/members is members.create, which Trainer and Nutritionist do not hold though
            both read this directory. Absent rather than disabled, matching the action bar below. */}
        <PageHeader title="Members" actions={hasPermission('members.create') ? <CreateMemberDialog /> : undefined} />

        <SearchField
          placeholder="Name, code, phone or email"
          value={searchTerm}
          onChange={(value) => {
            setSearchTerm(value)
            setPage(1)
          }}
        />

        <FilterTabs
          tabs={tabs}
          active={status}
          onChange={(key) => {
            setStatus(key)
            setPage(1)
          }}
        />

        {membersQuery.isError && (
          <ListError
            message="We couldn't load the member list"
            onRetry={() => membersQuery.refetch()}
            isRetrying={membersQuery.isFetching}
          />
        )}

        {membersQuery.isLoading && <ListSkeleton />}

        {!membersQuery.isLoading && data?.items.length === 0 && <ListEmpty message="No members found." />}

        {!membersQuery.isLoading && data && data.items.length > 0 && (
          <>
            {/* Mobile: card list — a table's Email/Joined columns have no room on a phone screen and
                end up hard-clipped mid-word, so small screens get a scannable card instead. */}
            <div className="space-y-2 md:hidden">
              {data.items.map((member) => (
                <button
                  key={member.id}
                  type="button"
                  onClick={() => openMember(member.id)}
                  className="flex w-full items-center gap-3 rounded-panel border border-border bg-card p-3 text-left active:bg-accent"
                >
                  <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-secondary font-display text-sm font-black">
                    {member.fullName.charAt(0).toUpperCase()}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="flex items-center justify-between gap-2">
                      <span className="truncate font-medium">{member.fullName}</span>
                      <MemberStatusPill status={member.status} />
                    </span>
                    <span className="block truncate text-sm text-muted-foreground">{member.email}</span>
                    <span className="block text-xs text-muted-foreground tabular-nums">{member.memberCode}</span>
                  </span>
                </button>
              ))}
            </div>

            <div className="hidden overflow-hidden rounded-3xl border border-border bg-card md:block">
              <Table>
                <TableHeader>
                  <TableRow className="hover:bg-transparent">
                    <TableHead className="w-10">
                      <Checkbox
                        checked={allOnPageChecked}
                        onCheckedChange={togglePage}
                        aria-label="Select all members on this page"
                      />
                    </TableHead>
                    <TableHead className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                      Member
                    </TableHead>
                    <TableHead className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                      Email
                    </TableHead>
                    <TableHead className="hidden text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase 2xl:table-cell">
                      Phone
                    </TableHead>
                    <TableHead className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                      Joined
                    </TableHead>
                    <TableHead className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                      Status
                    </TableHead>
                    <TableHead className="w-10" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.items.map((member) => {
                    const isSelected = member.id === selectedMemberId
                    return (
                      <TableRow
                        key={member.id}
                        onClick={() => openMember(member.id)}
                        aria-selected={isSelected}
                        className={cn(
                          'cursor-pointer',
                          // A volt edge rather than a border so the row's own height and the table's
                          // grid don't shift by 3px the moment a member is selected.
                          isSelected && 'bg-primary/10 shadow-[inset_3px_0_0_var(--primary)] hover:bg-primary/10',
                        )}
                      >
                        <TableCell
                          // The cell, not just the box, swallows the click: a checkbox is a small
                          // target and the misses would otherwise open the rail instead of ticking.
                          onClick={(e) => e.stopPropagation()}
                        >
                          <Checkbox
                            checked={checkedIds.has(member.id)}
                            onCheckedChange={() => toggleOne(member.id)}
                            aria-label={`Select ${member.fullName}`}
                          />
                        </TableCell>
                        <TableCell>
                          <div className="flex items-center gap-2.5">
                            <span className="flex size-8 shrink-0 items-center justify-center rounded-xl bg-secondary font-display text-xs font-black">
                              {member.fullName.charAt(0).toUpperCase()}
                            </span>
                            <span className="min-w-0">
                              <span className="block truncate font-medium">{member.fullName}</span>
                              {/* The design pairs the code with a branch name; MemberListItemDto has
                                  no branch, so the code stands alone rather than beside a guess. */}
                              <span className="block text-xs text-muted-foreground tabular-nums">{member.memberCode}</span>
                            </span>
                          </div>
                        </TableCell>
                        <TableCell className="max-w-[16rem] truncate text-muted-foreground">{member.email}</TableCell>
                        <TableCell className="hidden text-muted-foreground tabular-nums 2xl:table-cell">
                          {member.phone ?? '—'}
                        </TableCell>
                        <TableCell className="text-muted-foreground tabular-nums">
                          {joinedFormat.format(new Date(`${member.joinDate}T00:00:00`))}
                        </TableCell>
                        <TableCell>
                          <MemberStatusPill status={member.status} />
                        </TableCell>
                        <TableCell>
                          {/* A real link, not a menu: it keeps middle-click and "open in new tab"
                              working on a row whose plain click no longer navigates. */}
                          <Link
                            to={`/members/${member.id}`}
                            onClick={(e) => e.stopPropagation()}
                            aria-label={`Open ${member.fullName}'s full profile`}
                            className="flex size-8 items-center justify-center rounded-xl text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
                          >
                            <ArrowUpRight className="size-4" />
                          </Link>
                        </TableCell>
                      </TableRow>
                    )
                  })}
                </TableBody>
              </Table>
            </div>

            {/* Below the table, above the pager, and sticky — so it stays reachable while the person
                scrolls a long list ticking boxes, which is exactly when they need it. Renders nothing
                without a selection, and nothing for a role holding neither batch permission. */}
            <MembersActionBar selectedIds={[...checkedIds]} onClear={() => setCheckedIds(new Set())} />

            <Pagination
              page={data.page}
              totalPages={data.totalPages}
              totalCount={data.totalCount}
              hasPreviousPage={data.hasPreviousPage}
              hasNextPage={data.hasNextPage}
              onPageChange={setPage}
              itemLabel="members"
            />
          </>
        )}
      </div>

      {/* The rail only exists once a member is chosen — an empty 392px column is a placeholder, and
          this screen doesn't ship those. It sticks while the list scrolls past it. */}
      {hasRoomForRail && selectedMemberId && (
        <aside className="sticky top-0 hidden max-h-[calc(100svh-6rem)] w-[392px] shrink-0 self-start overflow-y-auto xl:block">
          <MemberDetailPanel memberId={selectedMemberId} variant="rail" onClose={() => setSelectedMemberId(null)} />
        </aside>
      )}
    </div>
  )
}
