import { useState } from 'react'
import { CloudOff } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useMembersList } from '@/modules/members/api/membersApi'
import { AddMealEntryDialog } from '@/modules/nutrition/components/AddMealEntryDialog'
import { CreateDietPlanDialog } from '@/modules/nutrition/components/CreateDietPlanDialog'
import { CreateFoodItemDialog } from '@/modules/nutrition/components/CreateFoodItemDialog'
import { LogWaterDialog } from '@/modules/nutrition/components/LogWaterDialog'
import { ListEmpty, ListError, ListSkeleton, StatTile } from '@/shared/components/console'
import { isStale } from '@/shared/lib/queryTrust'
import { useAuthStore } from '@/stores/authStore'
import {
  useDietPlan,
  useFoodItemsList,
  useMemberDietPlans,
  useMemberWaterLogs,
  useMyNutritionClients,
  type DietPlanListItem,
  type NutritionClientRow,
} from '@/modules/nutrition/api/nutritionApi'

function DietPlanCard({
  planListItem,
  isSelected,
  onSelect,
}: {
  planListItem: DietPlanListItem
  isSelected: boolean
  onSelect: () => void
}) {
  const { data: plan } = useDietPlan(isSelected ? planListItem.id : undefined)

  return (
    <div>
      <button
        type="button"
        onClick={onSelect}
        className="flex w-full items-center justify-between rounded-xl border border-border p-3 text-left text-sm hover:bg-accent"
      >
        <span className="font-medium">{planListItem.name}</span>
      </button>
      {isSelected && plan && (
        <div className="space-y-2 rounded-b-md border border-t-0 p-3">
          <div className="flex items-center justify-between text-sm text-muted-foreground">
            <span>
              {new Date(plan.startDate).toLocaleDateString()}
              {plan.endDate && ` → ${new Date(plan.endDate).toLocaleDateString()}`}
              {plan.targetCalories && ` · Target ${plan.targetCalories} kcal`}
            </span>
            <AddMealEntryDialog dietPlanId={plan.id} />
          </div>
          {plan.mealEntries.length === 0 && <p className="text-sm text-muted-foreground">No meals logged yet.</p>}
          {plan.mealEntries.map((m) => (
            <div key={m.id} className="flex items-center justify-between text-sm">
              <span>
                {m.foodItemName} × {m.quantity}
              </span>
              <div className="flex items-center gap-2">
                <Badge variant="outline">{m.mealType}</Badge>
                {m.consumedAt && <span className="text-xs text-muted-foreground">{new Date(m.consumedAt).toLocaleString()}</span>}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

const dayFormat = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' })

/**
 * How long since this member last logged anything against the plan.
 *
 * Null is its own sentence — "never logged" is a different problem from "stopped logging", and the
 * first one is usually a plan the member never opened rather than a member who lapsed.
 */
function loggedLabel(at: string | null): string {
  if (!at) return 'Never logged'
  const days = Math.floor((Date.now() - new Date(at).getTime()) / 86_400_000)
  if (days <= 0) return 'Logged today'
  if (days === 1) return 'Logged yesterday'
  if (days < 30) return `Logged ${days} days ago`
  return `Logged ${dayFormat.format(new Date(at))}`
}

function ClientRow({ client, onOpen }: { client: NutritionClientRow; onOpen: () => void }) {
  return (
    <button
      type="button"
      onClick={onOpen}
      className="flex w-full items-start gap-3 rounded-2xl border border-border bg-card p-3 text-left transition-colors hover:bg-accent/50"
    >
      <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-secondary font-display text-sm font-black">
        {client.memberName.charAt(0).toUpperCase()}
      </span>
      <span className="min-w-0 flex-1">
        <span className="flex items-center justify-between gap-2">
          <span className="truncate font-medium">{client.memberName}</span>
          {/* The only badge on the row, and only when something is wrong with it. A green "Active"
              chip on every row would make the lapsed ones harder to find, not easier. */}
          {!client.planIsActive && <span className="shrink-0 text-xs font-bold text-warning">Plan ended</span>}
        </span>
        <span className="block truncate text-sm text-muted-foreground">{client.planName}</span>
        <span className="mt-0.5 flex flex-wrap items-center gap-x-3 text-xs text-muted-foreground">
          <span className="tabular-nums">{client.memberCode}</span>
          <span>{loggedLabel(client.lastMealLoggedAt)}</span>
          <span className="tabular-nums">
            {client.mealsLoggedLast7Days} {client.mealsLoggedLast7Days === 1 ? 'meal' : 'meals'} this week
          </span>
        </span>
      </span>
    </button>
  )
}

/**
 * The nutritionist's own caseload.
 *
 * Before this the module opened on a food library and a search box that required already knowing a
 * member's name — fine for looking someone up, useless for answering "who am I responsible for and
 * who has gone quiet". The roster is derived from the plans this user wrote, so it needed no new
 * table; the same relationship was already in the data, just never read back.
 */
function MyClients({ onOpen }: { onOpen: (memberId: string, memberName: string) => void }) {
  const clients = useMyNutritionClients()

  /*
   * This screen's whole claim is completeness — "these are all of my clients, and these three are the
   * ones who need me". A roster that quietly drops rows is worse than one that fails, so a query that
   * cannot be believed is never rendered as a list. isStale rather than isError because a paused
   * query (no network) reports isError FALSE indefinitely; see shared/lib/queryTrust.
   */
  const stale = isStale(clients)

  if (clients.isPending && !stale) return <ListSkeleton rows={5} />
  if (!clients.data) {
    return <ListError message="We couldn't load your clients." onRetry={() => clients.refetch()} isRetrying={clients.isFetching} />
  }

  const rows = clients.data.clients
  const lapsed = rows.filter((c) => !c.planIsActive).length

  return (
    <div className="space-y-3">
      {/* Data on screen, but from an earlier minute — say so rather than let a shrunken roster read
          as a client who has left. */}
      {stale && (
        <p className="flex items-start gap-1.5 text-sm text-muted-foreground">
          <CloudOff className="mt-0.5 size-3.5 shrink-0" />
          We couldn't refresh this — the roster below may be out of date.
        </p>
      )}

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <StatTile label="My clients" value={String(rows.length)} caption="Members you have written a plan for" />
        <StatTile
          label="Plans ended"
          value={String(lapsed)}
          caption={lapsed > 0 ? 'Still yours — the prescription ran out' : 'Every plan is current'}
          captionTone={lapsed > 0 ? 'warning' : 'muted'}
          tone={lapsed > 0 ? 'warning' : undefined}
        />
        <StatTile
          label="No plan at all"
          value={String(clients.data.activeMembersWithoutAPlan)}
          caption="Active members with no current plan from anyone"
        />
      </div>

      {rows.length === 0 ? (
        <ListEmpty
          message="You have no clients yet."
          hint="Find a member under Member Nutrition and write them a diet plan — they'll appear here."
        />
      ) : (
        <div className="space-y-2">
          {rows.map((c) => (
            <ClientRow key={c.memberId} client={c} onOpen={() => onOpen(c.memberId, c.memberName)} />
          ))}
        </div>
      )}
    </div>
  )
}

function MemberNutrition({
  selected,
  onSelect,
}: {
  selected: { id: string; name: string } | null
  onSelect: (member: { id: string; name: string } | null) => void
}) {
  const [searchTerm, setSearchTerm] = useState('')
  const [selectedPlanId, setSelectedPlanId] = useState('')

  const memberId = selected?.id ?? ''
  const memberName = selected?.name ?? ''

  const { data: members } = useMembersList({ searchTerm: searchTerm || undefined, status: 'Active', page: 1, pageSize: 6 })
  const { data: plans, isLoading: plansLoading } = useMemberDietPlans(memberId || undefined)
  const { data: waterLogs, isLoading: waterLoading } = useMemberWaterLogs(memberId || undefined)

  return (
    <div className="space-y-3">
      <Input
        placeholder="Search member to view or log their nutrition..."
        value={memberId ? memberName : searchTerm}
        onChange={(e) => {
          onSelect(null)
          setSelectedPlanId('')
          setSearchTerm(e.target.value)
        }}
      />
      {!memberId && searchTerm && (
        <div className="divide-y divide-border overflow-hidden rounded-panel border border-border bg-card edge-light-soft">
          {members?.items.length === 0 && <p className="p-3 text-sm text-muted-foreground">No members match.</p>}
          {members?.items.map((m) => (
            <button
              key={m.id}
              type="button"
              onClick={() => {
                onSelect({ id: m.id, name: m.fullName })
                setSearchTerm('')
              }}
              className="flex w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-accent"
            >
              {m.fullName}
              <span className="text-xs text-muted-foreground">{m.memberCode}</span>
            </button>
          ))}
        </div>
      )}

      {memberId && (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">{memberName}'s diet plans</p>
              <CreateDietPlanDialog memberId={memberId} />
            </div>
            {plansLoading && <Skeleton className="h-16 w-full rounded-2xl" />}
            {plans?.length === 0 && <p className="text-sm text-muted-foreground">No diet plans yet.</p>}
            <div className="space-y-2">
              {plans?.map((p) => (
                <DietPlanCard
                  key={p.id}
                  planListItem={p}
                  isSelected={selectedPlanId === p.id}
                  onSelect={() => setSelectedPlanId((prev) => (prev === p.id ? '' : p.id))}
                />
              ))}
            </div>
          </div>

          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">{memberName}'s water intake</p>
              <LogWaterDialog memberId={memberId} />
            </div>
            {waterLoading && <Skeleton className="h-16 w-full rounded-2xl" />}
            {waterLogs?.length === 0 && <p className="text-sm text-muted-foreground">No water logged yet.</p>}
            <div className="space-y-1">
              {waterLogs?.map((w) => (
                <div key={w.id} className="flex items-center justify-between rounded-xl border border-border px-3 py-2 text-sm">
                  <span>{w.amountMl} ml</span>
                  <span className="text-xs text-muted-foreground">{new Date(w.loggedAt).toLocaleString()}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

export default function NutritionPage() {
  const { data: foodItems, isLoading } = useFoodItemsList()

  /*
   * The tab is controlled and the selected member lives up here so the roster can hand off to the
   * detail view: pressing a client opens THAT member's plans rather than dropping the nutritionist
   * back at a search box with the name they just clicked. Same handoff the front desk makes when a
   * scan opens the member workspace.
   */
  /*
   * Only the nutritionist opens on the roster. A manager or owner also holds nutrition.view and has
   * written no plans, so for them "My Clients" is a correctly-empty screen — and landing on an empty
   * screen reads as a broken module rather than as an accurate answer to a question they didn't ask.
   * They start where their question actually is: a named member.
   */
  const isNutritionist = useAuthStore((s) => s.user)?.roles.includes('Nutritionist') ?? false
  const [tab, setTab] = useState(isNutritionist ? 'clients' : 'member')
  const [selected, setSelected] = useState<{ id: string; name: string } | null>(null)

  return (
    <div className="space-y-4">
      <div>
        <h1 className="font-display text-3xl leading-tight font-black tracking-tight">Nutrition</h1>
        <p className="text-sm text-muted-foreground">Your clients, diet plans, food library, and hydration tracking.</p>
      </div>

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList className="h-auto flex-wrap">
          {/* First and default: the module now opens on the caseload rather than on a reference table. */}
          <TabsTrigger value="clients">My Clients</TabsTrigger>
          <TabsTrigger value="member">Member Nutrition</TabsTrigger>
          <TabsTrigger value="food">Food Library</TabsTrigger>
        </TabsList>

        <TabsContent value="clients">
          <MyClients
            onOpen={(id, name) => {
              setSelected({ id, name })
              setTab('member')
            }}
          />
        </TabsContent>

        <TabsContent value="food" className="space-y-3">
          <div className="flex justify-end">
            <CreateFoodItemDialog />
          </div>
          {isLoading ? (
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {Array.from({ length: 6 }).map((_, i) => (
                <Skeleton key={i} className="h-28 w-full" />
              ))}
            </div>
          ) : (
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {foodItems?.map((food) => (
                <div key={food.id} className="space-y-2 rounded-panel border border-border bg-card p-3 edge-light-soft">
                    <div className="flex items-center justify-between">
                      <p className="font-medium">{food.name}</p>
                      <span className="text-sm text-muted-foreground">{food.servingSizeDescription}</span>
                    </div>
                    <div className="flex flex-wrap gap-1">
                      <Badge variant="default">{food.caloriesPerServing} kcal</Badge>
                      <Badge variant="outline">P {food.proteinG}g</Badge>
                      <Badge variant="outline">C {food.carbsG}g</Badge>
                      <Badge variant="outline">F {food.fatG}g</Badge>
                    </div>
                </div>
              ))}
            </div>
          )}
        </TabsContent>

        <TabsContent value="member">
          <MemberNutrition selected={selected} onSelect={setSelected} />
        </TabsContent>
      </Tabs>
    </div>
  )
}
