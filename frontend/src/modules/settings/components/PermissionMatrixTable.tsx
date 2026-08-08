import { Fragment, useMemo } from 'react'

import { Checkbox } from '@/components/ui/checkbox'
import { Skeleton } from '@/components/ui/skeleton'
import { usePermissionMatrix, useSetRolePermission } from '@/modules/settings/api/settingsApi'

export function PermissionMatrixTable() {
  const { data, isLoading, isError } = usePermissionMatrix()
  const setRolePermission = useSetRolePermission()

  const grantedSet = useMemo(
    () => new Set((data?.grants ?? []).map((g) => `${g.roleId}:${g.permissionId}`)),
    [data?.grants]
  )

  if (isLoading) {
    return <Skeleton className="h-96 w-full" />
  }

  if (isError) {
    return <p className="text-sm text-muted-foreground">Only Owners can manage the role permission matrix.</p>
  }

  if (!data) {
    return null
  }

  const grouped = data.permissions.reduce<Record<string, typeof data.permissions>>((acc, p) => {
    ;(acc[p.module] ??= []).push(p)
    return acc
  }, {})

  const toggle = (roleId: string, permissionId: string) => {
    const granted = grantedSet.has(`${roleId}:${permissionId}`)
    setRolePermission.mutate({ roleId, permissionId, granted: !granted })
  }

  return (
    <div className="overflow-x-auto rounded-panel border border-border bg-card edge-light-soft">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b bg-muted/50">
            <th className="sticky left-0 bg-muted/50 px-3 py-2 text-left font-medium">Permission</th>
            {data.roles.map((role) => (
              <th key={role.id} className="min-w-[90px] px-2 py-2 text-center font-medium">
                {role.name}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {Object.entries(grouped).map(([module, permissions]) => (
            <Fragment key={module}>
              <tr className="border-b bg-muted/20">
                <td colSpan={data.roles.length + 1} className="px-3 py-1 text-xs font-semibold uppercase text-muted-foreground">
                  {module}
                </td>
              </tr>
              {permissions.map((permission) => (
                <tr key={permission.id} className="border-b last:border-0">
                  <td className="sticky left-0 bg-background px-3 py-2">
                    <p>{permission.description}</p>
                    <p className="font-mono text-xs text-muted-foreground">{permission.code}</p>
                  </td>
                  {data.roles.map((role) => (
                    <td key={role.id} className="px-2 py-2 text-center">
                      <Checkbox
                        checked={grantedSet.has(`${role.id}:${permission.id}`)}
                        onCheckedChange={() => toggle(role.id, permission.id)}
                        disabled={setRolePermission.isPending}
                      />
                    </td>
                  ))}
                </tr>
              ))}
            </Fragment>
          ))}
        </tbody>
      </table>
    </div>
  )
}
