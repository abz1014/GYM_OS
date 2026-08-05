import { useState } from 'react'
import { Loader2, RefreshCw } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Pagination } from '@/shared/components/Pagination'
import { useAdminBranchesList, useAuditLogs, useSystemPreferences } from '@/modules/settings/api/settingsApi'
import { CreateBranchDialog } from '@/modules/settings/components/CreateBranchDialog'
import { EditBranchDialog } from '@/modules/settings/components/EditBranchDialog'
import { GymProfileForm } from '@/modules/settings/components/GymProfileForm'
import { PermissionMatrixTable } from '@/modules/settings/components/PermissionMatrixTable'
import { UpsertSystemPreferenceDialog } from '@/modules/settings/components/UpsertSystemPreferenceDialog'
import { useRebuildExperienceProjections } from '@/modules/coaching/api/coachingApi'

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

function AuditLogTab() {
  const [page, setPage] = useState(1)
  const { data, isLoading } = useAuditLogs({ page, pageSize: 25 })

  return (
    <div className="space-y-3">
      <p className="text-sm text-muted-foreground">
        A record of every business action taken in the system — who did what, and when.
      </p>
      {isLoading && <Skeleton className="h-64 w-full" />}
      {data && (
        <>
          {data.items.length === 0 && (
            <p className="py-8 text-center text-sm text-muted-foreground">No audit entries yet.</p>
          )}

          {data.items.length > 0 && (
            <>
              {/* Mobile: card list */}
              <div className="space-y-2 md:hidden">
                {data.items.map((entry) => (
                  <div key={entry.id} className="space-y-1.5 rounded-lg border bg-card p-3">
                    <div className="flex items-center justify-between gap-2">
                      <span className="truncate font-mono text-xs">{entry.action}</span>
                      <Badge variant="outline" className="shrink-0">
                        {entry.entityType}
                      </Badge>
                    </div>
                    <p className="text-sm text-muted-foreground">
                      {entry.userName ?? 'System'} · {new Date(entry.occurredAt).toLocaleString()}
                    </p>
                  </div>
                ))}
              </div>

              {/* Desktop / tablet: full table */}
              <div className="hidden rounded-lg border md:block">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Action</TableHead>
                      <TableHead>Module</TableHead>
                      <TableHead>User</TableHead>
                      <TableHead>When</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {data.items.map((entry) => (
                      <TableRow key={entry.id}>
                        <TableCell className="font-mono text-xs">{entry.action}</TableCell>
                        <TableCell>
                          <Badge variant="outline">{entry.entityType}</Badge>
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">{entry.userName ?? '—'}</TableCell>
                        <TableCell className="text-sm text-muted-foreground">{new Date(entry.occurredAt).toLocaleString()}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            </>
          )}
          <Pagination
            page={data.page}
            totalPages={data.totalPages}
            totalCount={data.totalCount}
            hasPreviousPage={data.hasPreviousPage}
            hasNextPage={data.hasNextPage}
            onPageChange={setPage}
          />
        </>
      )}
    </div>
  )
}

function DataMaintenanceTab() {
  const rebuild = useRebuildExperienceProjections()

  return (
    <div className="space-y-3">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Rebuild member experience projections</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <p className="text-sm text-muted-foreground">
            Recomputes member level/XP totals and exercise mastery from their source history — the XP
            ledger and workout logs. Safe to run any time (never touches XP transactions, personal
            records, or existing achievements); useful after a scoring-rule change or to correct any
            drift. Also backfills any achievement a corrected total newly qualifies for.
          </p>
          <Button
            variant="outline"
            disabled={rebuild.isPending}
            onClick={() =>
              rebuild.mutate(undefined, {
                onSuccess: (result) =>
                  toast.success(
                    `Rebuilt ${result.progressionsRebuilt} progression${result.progressionsRebuilt === 1 ? '' : 's'} and ` +
                      `${result.masteryRowsRebuilt} mastery row${result.masteryRowsRebuilt === 1 ? '' : 's'} across ` +
                      `${result.membersConsidered} members` +
                      (result.achievementsBackfilled > 0 ? ` — backfilled ${result.achievementsBackfilled} achievement(s).` : '.'),
                  ),
                onError: () => toast.error('Could not rebuild projections.'),
              })
            }
          >
            {rebuild.isPending ? <Loader2 className="size-4 animate-spin" /> : <RefreshCw className="size-4" />}
            Rebuild projections
          </Button>
        </CardContent>
      </Card>
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
        <TabsList className="h-auto flex-wrap">
          <TabsTrigger value="profile">Gym Profile</TabsTrigger>
          <TabsTrigger value="branches">Branches</TabsTrigger>
          <TabsTrigger value="permissions">Permission Matrix</TabsTrigger>
          <TabsTrigger value="preferences">System Preferences</TabsTrigger>
          <TabsTrigger value="audit-log">Audit Log</TabsTrigger>
          <TabsTrigger value="data-maintenance">Data Maintenance</TabsTrigger>
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

        <TabsContent value="audit-log">
          <AuditLogTab />
        </TabsContent>

        <TabsContent value="data-maintenance">
          <DataMaintenanceTab />
        </TabsContent>
      </Tabs>
    </div>
  )
}
