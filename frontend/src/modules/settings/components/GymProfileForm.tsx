import { useEffect, useState } from 'react'
import { Loader2, Save } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { useGymProfile, useUpdateGymProfile, type GymProfile } from '@/modules/settings/api/settingsApi'

const emptyProfile: GymProfile = {
  legalName: '',
  displayName: '',
  logoUrl: null,
  supportEmail: null,
  supportPhone: null,
  defaultCurrency: 'USD',
  defaultTimeZone: 'UTC',
}

export function GymProfileForm() {
  const { data: profile, isLoading } = useGymProfile()
  const updateProfile = useUpdateGymProfile()
  const [form, setForm] = useState<GymProfile>(emptyProfile)

  useEffect(() => {
    if (profile) {
      setForm(profile)
    }
  }, [profile])

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    updateProfile.mutate(form, {
      onSuccess: () => toast.success('Gym profile updated.'),
      onError: () => toast.error('Could not update gym profile.'),
    })
  }

  if (isLoading) {
    return <Skeleton className="h-72 w-full max-w-xl" />
  }

  return (
    <form onSubmit={handleSubmit} className="max-w-xl space-y-4">
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1.5">
          <Label htmlFor="legalName">Legal name</Label>
          <Input
            id="legalName"
            required
            value={form.legalName}
            onChange={(e) => setForm({ ...form, legalName: e.target.value })}
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="displayName">Display name</Label>
          <Input
            id="displayName"
            required
            value={form.displayName}
            onChange={(e) => setForm({ ...form, displayName: e.target.value })}
          />
        </div>
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1.5">
          <Label htmlFor="supportEmail">Support email</Label>
          <Input
            id="supportEmail"
            type="email"
            value={form.supportEmail ?? ''}
            onChange={(e) => setForm({ ...form, supportEmail: e.target.value || null })}
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="supportPhone">Support phone</Label>
          <Input
            id="supportPhone"
            value={form.supportPhone ?? ''}
            onChange={(e) => setForm({ ...form, supportPhone: e.target.value || null })}
          />
        </div>
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1.5">
          <Label htmlFor="defaultCurrency">Default currency</Label>
          <Input
            id="defaultCurrency"
            required
            value={form.defaultCurrency}
            onChange={(e) => setForm({ ...form, defaultCurrency: e.target.value.toUpperCase() })}
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="defaultTimeZone">Default time zone</Label>
          <Input
            id="defaultTimeZone"
            required
            value={form.defaultTimeZone}
            onChange={(e) => setForm({ ...form, defaultTimeZone: e.target.value })}
          />
        </div>
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="logoUrl">Logo URL (optional)</Label>
        <Input
          id="logoUrl"
          type="url"
          value={form.logoUrl ?? ''}
          onChange={(e) => setForm({ ...form, logoUrl: e.target.value || null })}
        />
      </div>
      <Button type="submit" disabled={updateProfile.isPending}>
        {updateProfile.isPending ? <Loader2 className="size-4 animate-spin" /> : <Save />}
        Save Changes
      </Button>
    </form>
  )
}
