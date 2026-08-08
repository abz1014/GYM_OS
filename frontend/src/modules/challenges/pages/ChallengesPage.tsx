import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Flag, Loader2, Plus, Users } from 'lucide-react'
import { toast } from 'sonner'

import { apiClient } from '@/lib/apiClient'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Skeleton } from '@/components/ui/skeleton'
import { ListEmpty, PageHeader } from '@/shared/components/console'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useChallengesList, useCreateChallenge } from '@/modules/challenges/api/challengesApi'

interface Branch {
  id: string
  name: string
}

const dateFmt = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', year: 'numeric', timeZone: 'UTC' })

function CreateChallengeDialog() {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [branchId, setBranchId] = useState<string>('all')
  const [startDate, setStartDate] = useState('')
  const [endDate, setEndDate] = useState('')
  const [targetWorkoutCount, setTargetWorkoutCount] = useState('5')

  const { data: branches } = useQuery({
    queryKey: ['branches'],
    queryFn: async () => (await apiClient.get<Branch[]>('/api/branches')).data,
  })

  const create = useCreateChallenge()

  const isValid = name.trim().length > 0 && startDate && endDate && Number(targetWorkoutCount) > 0

  const reset = () => {
    setName('')
    setDescription('')
    setBranchId('all')
    setStartDate('')
    setEndDate('')
    setTargetWorkoutCount('5')
  }

  const handleSubmit = () => {
    if (!isValid) return
    create.mutate(
      {
        name: name.trim(),
        description: description.trim() || null,
        branchId: branchId === 'all' ? null : branchId,
        startDate,
        endDate,
        targetWorkoutCount: Number(targetWorkoutCount),
      },
      {
        onSuccess: () => {
          toast.success('Challenge created.')
          reset()
          setOpen(false)
        },
        onError: () => toast.error('Could not create the challenge.'),
      },
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" className="gap-1">
          <Plus className="size-4" />
          Create challenge
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create a community challenge</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="challenge-name">Name</Label>
            <Input id="challenge-name" placeholder="e.g. August Consistency Challenge" maxLength={150}
              value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="challenge-description">Description (optional)</Label>
            <Textarea id="challenge-description" placeholder="What members are working toward"
              value={description} onChange={(e) => setDescription(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label>Branch</Label>
            <Select value={branchId} onValueChange={setBranchId}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="All branches" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All branches</SelectItem>
                {branches?.map((b) => (
                  <SelectItem key={b.id} value={b.id}>
                    {b.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="challenge-start">Start date</Label>
              <Input id="challenge-start" type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="challenge-end">End date</Label>
              <Input id="challenge-end" type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="challenge-target">Target workouts to complete</Label>
            <Input id="challenge-target" type="number" min={1}
              value={targetWorkoutCount} onChange={(e) => setTargetWorkoutCount(e.target.value)} />
          </div>
        </div>
        <DialogFooter>
          <Button disabled={!isValid || create.isPending} onClick={handleSubmit}>
            {create.isPending && <Loader2 className="size-4 animate-spin" />}
            Create challenge
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export default function ChallengesPage() {
  const { data: challenges, isLoading } = useChallengesList()

  return (
    <div className="space-y-4">
      <PageHeader
        title="Community challenges"
        description="Create challenges members opt into from their portal."
        actions={<CreateChallengeDialog />}
      />

      {isLoading && (
        <div className="grid gap-3 sm:grid-cols-2">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-36 w-full rounded-2xl" />
          ))}
        </div>
      )}

      {challenges && challenges.length === 0 && (
        <ListEmpty message="No challenges yet." hint="Create one to get members opting in." />
      )}

      {challenges && challenges.length > 0 && (
        <div className="grid gap-3 sm:grid-cols-2">
          {challenges.map((c) => (
            <div key={c.id} className="space-y-2 rounded-panel border border-border bg-card p-5 edge-light-soft">
              <h2 className="flex items-center gap-2 font-display text-xl font-bold tracking-tight">
                <Flag className="size-4 shrink-0 text-warning" />
                {c.name}
              </h2>
              {c.description && <p className="text-sm text-muted-foreground">{c.description}</p>}
              <p className="text-xs text-muted-foreground tabular-nums">
                {dateFmt.format(new Date(`${c.startDate}T00:00:00Z`))} – {dateFmt.format(new Date(`${c.endDate}T00:00:00Z`))}
                {' · '}Target: {c.targetWorkoutCount} workouts
                {' · '}{c.branchId ? 'Branch-specific' : 'All branches'}
              </p>
              <div className="flex items-center gap-2">
                <Badge variant="secondary" className="gap-1 tabular-nums">
                  <Users className="size-3" />
                  {c.participantCount} joined
                </Badge>
                <Badge variant="secondary" className="tabular-nums">{c.completedCount} completed</Badge>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
