import { useState } from 'react'
import { Loader2, Pencil } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { useUpdateNotificationTemplate, type NotificationTemplate } from '@/modules/notifications/api/notificationsApi'

export function EditNotificationTemplateDialog({ template }: { template: NotificationTemplate }) {
  const [open, setOpen] = useState(false)
  const [subject, setSubject] = useState(template.subject)
  const [bodyTemplate, setBodyTemplate] = useState(template.bodyTemplate)
  const [isActive, setIsActive] = useState(template.isActive)

  const updateTemplate = useUpdateNotificationTemplate(template.id)

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    updateTemplate.mutate(
      { subject, bodyTemplate, isActive },
      {
        onSuccess: () => {
          toast.success('Template updated.')
          setOpen(false)
        },
        onError: () => toast.error('Could not update template.'),
      }
    )
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) {
          setSubject(template.subject)
          setBodyTemplate(template.bodyTemplate)
          setIsActive(template.isActive)
        }
      }}
    >
      <DialogTrigger asChild>
        <Button variant="ghost" size="icon">
          <Pencil />
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit template — {template.code}</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="subject">Subject</Label>
            <Input id="subject" required value={subject} onChange={(e) => setSubject(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="body">Body template</Label>
            <Textarea id="body" required rows={4} value={bodyTemplate} onChange={(e) => setBodyTemplate(e.target.value)} />
          </div>
          <div className="flex items-center gap-2">
            <Checkbox id="isActive" checked={isActive} onCheckedChange={(checked) => setIsActive(checked === true)} />
            <Label htmlFor="isActive">Active</Label>
          </div>
          <DialogFooter>
            <Button type="submit" disabled={updateTemplate.isPending}>
              {updateTemplate.isPending && <Loader2 className="size-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
