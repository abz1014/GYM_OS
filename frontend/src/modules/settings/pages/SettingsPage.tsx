import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useAdminBranchesList, useSystemPreferences } from '@/modules/settings/api/settingsApi'
import { CreateBranchDialog } from '@/modules/settings/components/CreateBranchDialog'
import { EditBranchDialog } from '@/modules/settings/components/EditBranchDialog'
import { GymProfileForm } from '@/modules/settings/components/GymProfileForm'
import { PermissionMatrixTable } from '@/modules/settings/components/PermissionMatrixTable'
import { UpsertSystemPreferenceDialog } from '@/modules/settings/components/UpsertSystemPreferenceDialog'

function BranchesTab() {
  const { data: branches, isLoading } = useAdminBranchesList(true)

  return (
    <div className="space-y-3">
      <div className="flex justify-end">
        <CreateBranchDialog />
      </div>
      {isLoading && <Skeleton className="h-40 w-full" />}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {branches?.map((branch) => (
          <Card key={branch.id} className={!branch.isActive ? 'opacity-60' : undefined}>
            <CardContent className="space-y-1 p-3">
              <div className="flex items-center justify-between">
                <p className="font-medium">{branch.name}</p>
                <EditBranchDialog branch={branch} />
              </div>
              <p className="text-sm text-muted-foreground">
                {branch.addressLine}, {branch.city}, {branch.country}
              </p>
              <div className="flex flex-wrap gap-1">
                <Badge variant="outline">{branch.timeZone}</Badge>
                <Badge variant="outline">{branch.currency}</Badge>
                {!branch.isActive && <Badge variant="secondary">Inactive</Badge>}
              </div>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}

function SystemPreferencesTab() {
  const { data: preferences, isLoading } = useSystemPreferences(null)

  return (
    <div className="space-y-3">
      <div className="flex justify-end">
        <UpsertSystemPreferenceDialog />
      </div>
      {isLoading && <Skeleton className="h-32 w-full" />}
      {preferences?.length === 0 && !isLoading && (
        <p className="text-sm text-muted-foreground">No tenant-wide preferences configured yet.</p>
      )}
      <div className="space-y-2">
        {preferences?.map((pref) => (
          <Card key={pref.id}>
            <CardContent className="flex items-center justify-between p-3">
              <div>
                <p className="font-mono text-sm font-medium">{pref.key}</p>
                <p className="text-sm text-muted-foreground">{pref.value}</p>
                {pref.description && <p className="text-xs text-muted-foreground">{pref.description}</p>}
              </div>
              <UpsertSystemPreferenceDialog existing={pref} />
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}

export default function SettingsPage() {
  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Settings</h1>
        <p className="text-sm text-muted-foreground">Gym profile, branches, role permissions, and preferences.</p>
      </div>

      <Tabs defaultValue="profile">
        <TabsList>
          <TabsTrigger value="profile">Gym Profile</TabsTrigger>
          <TabsTrigger value="branches">Branches</TabsTrigger>
          <TabsTrigger value="permissions">Permission Matrix</TabsTrigger>
          <TabsTrigger value="preferences">System Preferences</TabsTrigger>
        </TabsList>

        <TabsContent value="profile">
          <GymProfileForm />
        </TabsContent>

        <TabsContent value="branches">
          <BranchesTab />
        </TabsContent>

        <TabsContent value="permissions">
          <PermissionMatrixTable />
        </TabsContent>

        <TabsContent value="preferences">
          <SystemPreferencesTab />
        </TabsContent>
      </Tabs>
    </div>
  )
}
