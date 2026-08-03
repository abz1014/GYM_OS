import { useNavigate } from 'react-router-dom'
import { Star } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Card, CardContent } from '@/components/ui/card'
import { useTrainersList } from '@/modules/trainers/api/trainersApi'
import { CreateTrainerDialog } from '@/modules/trainers/components/CreateTrainerDialog'
import { useUiStore } from '@/stores/uiStore'

export default function TrainersListPage() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const { data: trainers, isLoading } = useTrainersList(branchId)
  const navigate = useNavigate()

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Trainers</h1>
          <p className="text-sm text-muted-foreground">{trainers?.length ?? '—'} trainers</p>
        </div>
        <CreateTrainerDialog />
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-36 w-full" />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {trainers?.map((trainer) => (
            <Card key={trainer.id} className="cursor-pointer" onClick={() => navigate(`/trainers/${trainer.id}`)}>
              <CardContent className="space-y-2">
                <div className="flex items-center justify-between">
                  <p className="font-medium">{trainer.fullName}</p>
                  {!trainer.isActive && <Badge variant="secondary">Inactive</Badge>}
                </div>
                <p className="text-sm text-muted-foreground">{trainer.specialties}</p>
                <div className="flex items-center justify-between text-sm">
                  <span className="text-muted-foreground">{trainer.activeClientCount} active clients</span>
                  {trainer.averageRating && (
                    <span className="flex items-center gap-1 text-warning">
                      <Star className="size-3.5 fill-current" /> {trainer.averageRating.toFixed(1)}
                    </span>
                  )}
                </div>
                <Badge variant="outline">{trainer.commissionRate}% commission</Badge>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}
