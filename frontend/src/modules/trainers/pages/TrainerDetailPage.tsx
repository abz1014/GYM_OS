import { useParams, Link } from 'react-router-dom'
import { ArrowLeft, Star } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useTrainer, useUpdateCommissionStatus } from '@/modules/trainers/api/trainersApi'
import { AddCommissionRecordDialog } from '@/modules/trainers/components/AddCommissionRecordDialog'
import { AddTrainerRatingDialog } from '@/modules/trainers/components/AddTrainerRatingDialog'
import { AddTrainerScheduleDialog } from '@/modules/trainers/components/AddTrainerScheduleDialog'
import { AssignClientDialog } from '@/modules/trainers/components/AssignClientDialog'

const currency = (amount: number) => amount.toLocaleString('en-US', { style: 'currency', currency: 'USD' })

const COMMISSION_STATUSES = ['Pending', 'Paid'] as const

function CommissionStatusCell({
  trainerId,
  commissionRecordId,
  status,
}: {
  trainerId: string
  commissionRecordId: string
  status: string
}) {
  const updateStatus = useUpdateCommissionStatus(trainerId)

  return (
    <Select
      value={status}
      onValueChange={(v) => updateStatus.mutate({ commissionRecordId, status: v as 'Pending' | 'Paid' })}
    >
      <SelectTrigger size="sm" className="w-[110px]">
        <Badge variant={status === 'Paid' ? 'default' : 'outline'}>{status}</Badge>
      </SelectTrigger>
      <SelectContent>
        {COMMISSION_STATUSES.map((s) => (
          <SelectItem key={s} value={s}>
            {s}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}

export default function TrainerDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data: trainer, isLoading } = useTrainer(id)

  if (isLoading || !trainer) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-40 w-full" />
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <Link to="/trainers" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="size-4" />
        Back to trainers
      </Link>

      <Card>
        <CardContent className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-xl font-semibold">
                {trainer.firstName} {trainer.lastName}
              </h1>
              {!trainer.isActive && <Badge variant="secondary">Inactive</Badge>}
            </div>
            <p className="text-sm text-muted-foreground">{trainer.email}</p>
            <p className="mt-1 text-sm">{trainer.specialties}</p>
            {trainer.bio && <p className="mt-1 text-sm text-muted-foreground">{trainer.bio}</p>}
          </div>
          <div className="text-right">
            <p className="text-2xl font-semibold">{trainer.commissionRate}%</p>
            <p className="text-xs text-muted-foreground">Commission rate</p>
            <p className="mt-2 text-lg font-semibold text-success">{currency(trainer.totalCommissionEarned)}</p>
            <p className="text-xs text-muted-foreground">Total earned</p>
          </div>
        </CardContent>
      </Card>

      <Tabs defaultValue="clients">
        <TabsList>
          <TabsTrigger value="clients">Clients</TabsTrigger>
          <TabsTrigger value="schedule">Schedule</TabsTrigger>
          <TabsTrigger value="ratings">Ratings</TabsTrigger>
          <TabsTrigger value="commissions">Commissions</TabsTrigger>
        </TabsList>

        <TabsContent value="clients" className="space-y-3">
          <div className="flex justify-end">
            <AssignClientDialog trainerId={trainer.id} />
          </div>
          {trainer.assignments.length === 0 && <p className="text-sm text-muted-foreground">No clients assigned yet.</p>}
          {trainer.assignments.map((a) => (
            <Card key={a.id}>
              <CardContent className="flex items-center justify-between">
                <span>{a.memberName}</span>
                <div className="flex items-center gap-2">
                  <span className="text-sm text-muted-foreground">Since {new Date(a.startDate).toLocaleDateString()}</span>
                  <Badge variant={a.isActive ? 'default' : 'outline'}>{a.isActive ? 'Active' : 'Ended'}</Badge>
                </div>
              </CardContent>
            </Card>
          ))}
        </TabsContent>

        <TabsContent value="schedule" className="space-y-3">
          <div className="flex justify-end">
            <AddTrainerScheduleDialog trainerId={trainer.id} />
          </div>
          {trainer.schedules.length === 0 && <p className="text-sm text-muted-foreground">No schedule set.</p>}
          {trainer.schedules.map((s) => (
            <Card key={s.id}>
              <CardContent className="flex items-center justify-between text-sm">
                <span className="font-medium">{s.dayOfWeek}</span>
                <span className="text-muted-foreground">
                  {s.startTime} – {s.endTime}
                </span>
                <Badge variant={s.isAvailable ? 'default' : 'secondary'}>{s.isAvailable ? 'Available' : 'Unavailable'}</Badge>
              </CardContent>
            </Card>
          ))}
        </TabsContent>

        <TabsContent value="ratings" className="space-y-3">
          <div className="flex justify-end">
            <AddTrainerRatingDialog trainerId={trainer.id} assignments={trainer.assignments} />
          </div>
          {trainer.ratings.length === 0 && <p className="text-sm text-muted-foreground">No ratings yet.</p>}
          {trainer.ratings.map((r) => (
            <Card key={r.id}>
              <CardContent className="space-y-1 text-sm">
                <div className="flex items-center justify-between">
                  <span className="font-medium">{r.memberName}</span>
                  <span className="flex items-center gap-1 text-warning">
                    <Star className="size-3.5 fill-current" /> {r.score}/5
                  </span>
                </div>
                {r.comment && <p className="text-muted-foreground">{r.comment}</p>}
              </CardContent>
            </Card>
          ))}
        </TabsContent>

        <TabsContent value="commissions" className="space-y-3">
          <div className="flex justify-end">
            <AddCommissionRecordDialog trainerId={trainer.id} />
          </div>
          {trainer.commissionRecords.length === 0 && <p className="text-sm text-muted-foreground">No commission records yet.</p>}
          {trainer.commissionRecords.map((c) => (
            <Card key={c.id}>
              <CardContent className="flex items-center justify-between text-sm">
                <span>{new Date(c.period).toLocaleDateString(undefined, { year: 'numeric', month: 'long' })}</span>
                <span className="font-medium">{currency(c.amount)}</span>
                <CommissionStatusCell trainerId={trainer.id} commissionRecordId={c.id} status={c.status} />
              </CardContent>
            </Card>
          ))}
        </TabsContent>
      </Tabs>
    </div>
  )
}
