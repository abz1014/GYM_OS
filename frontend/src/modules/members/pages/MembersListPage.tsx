import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search } from 'lucide-react'

import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
import { Pagination } from '@/shared/components/Pagination'
import { useMembersList, type MemberStatus } from '@/modules/members/api/membersApi'
import { CreateMemberDialog } from '@/modules/members/components/CreateMemberDialog'

const STATUS_VARIANT: Record<MemberStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Active: 'default',
  Frozen: 'secondary',
  Expired: 'outline',
  Cancelled: 'destructive',
}

export default function MembersListPage() {
  const [searchTerm, setSearchTerm] = useState('')
  const [status, setStatus] = useState<MemberStatus | 'all'>('all')
  const [page, setPage] = useState(1)
  const navigate = useNavigate()

  const { data, isLoading } = useMembersList({
    searchTerm: searchTerm || undefined,
    status: status === 'all' ? undefined : status,
    page,
    pageSize: 50,
  })

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Members</h1>
          <p className="text-sm text-muted-foreground">{data?.totalCount ?? '—'} total members</p>
        </div>
        <CreateMemberDialog />
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <div className="relative w-full max-w-xs">
          <Search className="absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder="Search name, email, code..."
            className="pl-8"
            value={searchTerm}
            onChange={(e) => {
              setSearchTerm(e.target.value)
              setPage(1)
            }}
          />
        </div>
        <Select
          value={status}
          onValueChange={(v) => {
            setStatus(v as MemberStatus | 'all')
            setPage(1)
          }}
        >
          <SelectTrigger className="w-[160px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            <SelectItem value="Active">Active</SelectItem>
            <SelectItem value="Frozen">Frozen</SelectItem>
            <SelectItem value="Expired">Expired</SelectItem>
            <SelectItem value="Cancelled">Cancelled</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {isLoading && (
        <div className="space-y-2">
          {Array.from({ length: 8 }).map((_, i) => (
            <Skeleton key={i} className="h-16 w-full md:h-10" />
          ))}
        </div>
      )}

      {!isLoading && data?.items.length === 0 && (
        <p className="py-8 text-center text-sm text-muted-foreground">No members found.</p>
      )}

      {!isLoading && data && data.items.length > 0 && (
        <>
          {/* Mobile: card list — a table's Email/Phone/Joined columns have no room on a phone
              screen and end up hard-clipped mid-word, so small screens get a scannable card
              instead of a shrunk table. */}
          <div className="space-y-2 md:hidden">
            {data.items.map((member) => (
              <button
                key={member.id}
                type="button"
                onClick={() => navigate(`/members/${member.id}`)}
                className="flex w-full items-center gap-3 rounded-lg border bg-card p-3 text-left active:bg-accent"
              >
                <Avatar className="size-10 shrink-0">
                  <AvatarImage src={member.profilePhotoUrl ?? undefined} />
                  <AvatarFallback>{member.fullName.at(0)}</AvatarFallback>
                </Avatar>
                <div className="min-w-0 flex-1">
                  <div className="flex items-center justify-between gap-2">
                    <p className="truncate font-medium">{member.fullName}</p>
                    <Badge variant={STATUS_VARIANT[member.status]} className="shrink-0">
                      {member.status}
                    </Badge>
                  </div>
                  <p className="truncate text-sm text-muted-foreground">{member.email}</p>
                  <p className="text-xs text-muted-foreground">
                    {member.memberCode} · Joined {new Date(member.joinDate).toLocaleDateString()}
                  </p>
                </div>
              </button>
            ))}
          </div>

          {/* Desktop / tablet: full table */}
          <div className="hidden rounded-lg border md:block">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Member</TableHead>
                  <TableHead>Code</TableHead>
                  <TableHead>Email</TableHead>
                  <TableHead>Phone</TableHead>
                  <TableHead>Joined</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.items.map((member) => (
                  <TableRow
                    key={member.id}
                    className="cursor-pointer"
                    onClick={() => navigate(`/members/${member.id}`)}
                  >
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <Avatar className="size-7">
                          <AvatarImage src={member.profilePhotoUrl ?? undefined} />
                          <AvatarFallback className="text-xs">{member.fullName.at(0)}</AvatarFallback>
                        </Avatar>
                        {member.fullName}
                      </div>
                    </TableCell>
                    <TableCell className="text-muted-foreground">{member.memberCode}</TableCell>
                    <TableCell className="text-muted-foreground">{member.email}</TableCell>
                    <TableCell className="text-muted-foreground">{member.phone ?? '—'}</TableCell>
                    <TableCell className="text-muted-foreground">{new Date(member.joinDate).toLocaleDateString()}</TableCell>
                    <TableCell>
                      <Badge variant={STATUS_VARIANT[member.status]}>{member.status}</Badge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

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
  )
}
