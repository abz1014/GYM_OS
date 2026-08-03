import { useState } from 'react'
import { Loader2, Star } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useAddTrainerRating } from '@/modules/trainers/api/trainersApi'
import type { TrainerAssignment, TrainerSession } from '@/modules/trainers/api/trainersApi'

const SCORES = [1, 2, 3, 4, 5]
const NO_SESSION = 'none'

export function AddTrainerRatingDialog({
  trainerId,
  assignments,
  sessions,
}: {
  trainerId: string
  assignments: TrainerAssignment[]
  sessions: TrainerSession[]
}) {
  const [open, setOpen] = useState(false)
  const [memberId, setMemberId] = useState('')
  const [sessionId, setSessionId] = useState(NO_SESSION)
  const [score, setScore] = useState('5')
  const [comment, setComment] = useState('')

  const completedSessions = sessions.filter((s) => s.status === 'Completed')
  const addRating = useAddTrainerRating(trainerId)

  const handleSessionChange = (value: string) => {
    setSessionId(value)
    if (value !== NO_SESSION) {
      const session = completedSessions.find((s) => s.id === value)
      if (session) {
        setMemberId(session.memberId)
      }
    }
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!memberId) {
      toast.error('Select a client.')
      return
    }

    addRating.mutate(
      {
        memberId,
        score: Number(score),
        comment: comment || undefined,
        sessionId: sessionId === NO_SESSION ? undefined : sessionId,
      },
      {
        onSuccess: () => {
          toast.success('Rating added.')
          setOpen(false)
          setMemberId('')
          setSessionId(NO_SESSION)
          setScore('5')
          setComment('')
        },
        onError: () => toast.error('Could not add rating.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          <Star />
          Add Rating
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add client rating</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label>Related session (optional)</Label>
            <Select value={sessionId} onValueChange={handleSessionChange}>
              <SelectTrigger className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NO_SESSION}>General feedback (no specific session)</SelectItem>
                {completedSessions.map((s) => (
                  <SelectItem key={s.id} value={s.id}>
                    {s.memberName} — {new Date(s.scheduledAt).toLocaleDateString()}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label>Client</Label>
            <Select value={memberId} onValueChange={setMemberId} disabled={sessionId !== NO_SESSION}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Select client" />
              </SelectTrigger>
              <SelectContent>
                {assignments.map((a) => (
                  <SelectItem key={a.memberId} value={a.memberId}>
                    {a.memberName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {assignments.length === 0 && (
              <p className="text-xs text-muted-foreground">This trainer has no assigned clients yet.</p>
            )}
          </div>
          <div className="space-y-1.5">
            <Label>Score</Label>
            <Select value={score} onValueChange={setScore}>
              <SelectTrigger className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {SCORES.map((s) => (
                  <SelectItem key={s} value={String(s)}>
                    {s} / 5
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="comment">Comment (optional)</Label>
            <Textarea id="comment" value={comment} onChange={(e) => setComment(e.target.value)} />
          </div>
          <DialogFooter>
            <Button type="submit" disabled={addRating.isPending || !memberId}>
              {addRating.isPending && <Loader2 className="size-4 animate-spin" />}
              Add Rating
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
