import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search } from 'lucide-react'

import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
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
  const navigate = useNavigate()

  const { data, isLoading } = useMembersList({
    searchTerm: searchTerm || undefined,
    status: status === 'all' ? undefined : status,
    page: 1,
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
            onChange={(e) => setSearchTerm(e.target.value)}
          />
        </div>
        <Select value={status} onValueChange={(v) => setStatus(v as MemberStatus | 'all')}>
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

      <div className="rounded-lg border">
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
            {isLoading &&
              Array.from({ length: 8 }).map((_, i) => (
                <TableRow key={i}>
                  <TableCell colSpan={6}>
                    <Skeleton className="h-6 w-full" />
                  </TableCell>
                </TableRow>
              ))}

            {!isLoading && data?.items.length === 0 && (
              <TableRow>
                <TableCell colSpan={6} className="py-8 text-center text-muted-foreground">
                  No members found.
                </TableCell>
              </TableRow>
            )}

            {data?.items.map((member) => (
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
    </div>
  )
}
