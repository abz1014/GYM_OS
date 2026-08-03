import { useParams, Link } from 'react-router-dom'
import { ArrowLeft, QrCode, Mail, Phone, MapPin, Cake } from 'lucide-react'

import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useMember } from '@/modules/members/api/membersApi'
import { RenewMembershipDialog } from '@/modules/members/components/RenewMembershipDialog'
import { FreezeMembershipDialog } from '@/modules/members/components/FreezeMembershipDialog'
import { TransferMemberDialog } from '@/modules/members/components/TransferMemberDialog'
import { EditMemberDialog } from '@/modules/members/components/EditMemberDialog'
import { AddEmergencyContactDialog } from '@/modules/members/components/AddEmergencyContactDialog'
import { AddMedicalNoteDialog } from '@/modules/members/components/AddMedicalNoteDialog'
import { AddMeasurementDialog } from '@/modules/members/components/AddMeasurementDialog'
import { AddProgressPhotoDialog } from '@/modules/members/components/AddProgressPhotoDialog'
import { ResumeMembershipButton } from '@/modules/members/components/ResumeMembershipButton'
import { CancelMembershipDialog } from '@/modules/members/components/CancelMembershipDialog'
import { ReactivateMembershipButton } from '@/modules/members/components/ReactivateMembershipButton'

export default function MemberDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data: member, isLoading } = useMember(id)

  if (isLoading || !member) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-40 w-full" />
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <Link to="/members" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="size-4" />
        Back to members
      </Link>

      <Card>
        <CardContent className="flex flex-wrap items-start justify-between gap-6">
          <div className="flex items-center gap-4">
            <Avatar className="size-16">
              <AvatarImage src={member.profilePhotoUrl ?? undefined} />
              <AvatarFallback className="text-lg">{member.firstName.at(0)}</AvatarFallback>
            </Avatar>
            <div>
              <div className="flex items-center gap-2">
                <h1 className="text-xl font-semibold">
                  {member.firstName} {member.lastName}
                </h1>
                <Badge>{member.status}</Badge>
                <EditMemberDialog member={member} />
              </div>
              <p className="text-sm text-muted-foreground">{member.memberCode}</p>
              <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted-foreground">
                <span className="flex items-center gap-1">
                  <Mail className="size-3.5" /> {member.email}
                </span>
                {member.phone && (
                  <span className="flex items-center gap-1">
                    <Phone className="size-3.5" /> {member.phone}
                  </span>
                )}
                {member.address && (
                  <span className="flex items-center gap-1">
                    <MapPin className="size-3.5" /> {member.address}
                  </span>
                )}
                {member.dateOfBirth && (
                  <span className="flex items-center gap-1">
                    <Cake className="size-3.5" /> {new Date(member.dateOfBirth).toLocaleDateString()}
                  </span>
                )}
              </div>
            </div>
          </div>

          <div className="flex flex-col items-center gap-2">
            <div className="flex flex-col items-center gap-1 rounded-lg border p-3">
              <QrCode className="size-16 text-foreground" />
              <span className="font-mono text-[10px] text-muted-foreground">{member.qrCodeToken.slice(0, 12)}…</span>
            </div>
            <TransferMemberDialog memberId={member.id} currentBranchId={member.branchId} />
          </div>
        </CardContent>
      </Card>

      <Tabs defaultValue="memberships">
        <TabsList>
          <TabsTrigger value="memberships">Memberships</TabsTrigger>
          <TabsTrigger value="measurements">Measurements</TabsTrigger>
          <TabsTrigger value="medical">Medical Notes</TabsTrigger>
          <TabsTrigger value="emergency">Emergency Contacts</TabsTrigger>
          <TabsTrigger value="photos">Progress Photos</TabsTrigger>
        </TabsList>

        <TabsContent value="memberships" className="space-y-3">
          <div className="flex justify-end">
            <RenewMembershipDialog memberId={member.id} />
          </div>
          {member.memberMemberships.length === 0 && (
            <p className="text-sm text-muted-foreground">No membership history yet.</p>
          )}
          {member.memberMemberships.map((mm) => (
            <Card key={mm.id}>
              <CardContent className="flex flex-wrap items-center justify-between gap-2">
                <div>
                  <p className="font-medium">{mm.membershipPlanName}</p>
                  <p className="text-sm text-muted-foreground">
                    {new Date(mm.startDate).toLocaleDateString()} → {new Date(mm.endDate).toLocaleDateString()}
                  </p>
                  {mm.status === 'Cancelled' && mm.cancellationReason && (
                    <p className="text-xs text-muted-foreground">Reason: {mm.cancellationReason}</p>
                  )}
                </div>
                <div className="flex items-center gap-2">
                  <Badge variant="outline">{mm.status}</Badge>
                  <span className="text-sm text-muted-foreground">
                    {mm.pricePaid.toLocaleString('en-US', { style: 'currency', currency: mm.currency })}
                  </span>
                  {mm.invoiceId && (
                    <Link to={`/billing/${mm.invoiceId}`} className="text-sm text-primary hover:underline">
                      Invoice
                    </Link>
                  )}
                  {mm.status === 'Active' && <FreezeMembershipDialog memberId={member.id} memberMembershipId={mm.id} />}
                  {mm.status === 'Frozen' && <ResumeMembershipButton memberId={member.id} memberMembershipId={mm.id} />}
                  {(mm.status === 'Active' || mm.status === 'Frozen' || mm.status === 'PendingActivation') && (
                    <CancelMembershipDialog memberId={member.id} memberMembershipId={mm.id} />
                  )}
                  {mm.status === 'Cancelled' && <ReactivateMembershipButton memberId={member.id} memberMembershipId={mm.id} />}
                </div>
              </CardContent>
            </Card>
          ))}
        </TabsContent>

        <TabsContent value="measurements" className="space-y-3">
          <div className="flex justify-end">
            <AddMeasurementDialog memberId={member.id} />
          </div>
          {member.measurements.length === 0 && <p className="text-sm text-muted-foreground">No measurements logged.</p>}
          {member.measurements.map((m) => (
            <Card key={m.id}>
              <CardContent className="flex gap-6 text-sm">
                <span>{new Date(m.measuredOn).toLocaleDateString()}</span>
                {m.weightKg && <span>Weight: {m.weightKg} kg</span>}
                {m.bodyFatPercentage && <span>Body fat: {m.bodyFatPercentage}%</span>}
              </CardContent>
            </Card>
          ))}
        </TabsContent>

        <TabsContent value="medical" className="space-y-3">
          <div className="flex justify-end">
            <AddMedicalNoteDialog memberId={member.id} />
          </div>
          {member.medicalNotes.length === 0 && <p className="text-sm text-muted-foreground">No medical notes on file.</p>}
          {member.medicalNotes.map((n) => (
            <Card key={n.id}>
              <CardContent className="text-sm">
                <p>{n.note}</p>
                <p className="mt-1 text-xs text-muted-foreground">{new Date(n.recordedAt).toLocaleString()}</p>
              </CardContent>
            </Card>
          ))}
        </TabsContent>

        <TabsContent value="emergency" className="space-y-3">
          <div className="flex justify-end">
            <AddEmergencyContactDialog memberId={member.id} />
          </div>
          {member.emergencyContacts.length === 0 && <p className="text-sm text-muted-foreground">No emergency contacts on file.</p>}
          {member.emergencyContacts.map((c) => (
            <Card key={c.id}>
              <CardContent className="text-sm">
                <p className="font-medium">
                  {c.name} <span className="font-normal text-muted-foreground">({c.relationship})</span>
                </p>
                <p className="text-muted-foreground">{c.phone}</p>
              </CardContent>
            </Card>
          ))}
        </TabsContent>

        <TabsContent value="photos" className="space-y-3">
          <div className="flex justify-end">
            <AddProgressPhotoDialog memberId={member.id} />
          </div>
          {member.progressPhotos.length === 0 ? (
            <p className="text-sm text-muted-foreground">No progress photos yet.</p>
          ) : (
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
              {member.progressPhotos.map((p) => (
                <div key={p.id} className="overflow-hidden rounded-lg border">
                  <img src={p.photoUrl} alt="Progress" className="aspect-[2/3] w-full object-cover" />
                  <p className="p-2 text-xs text-muted-foreground">{new Date(p.takenAt).toLocaleDateString()}</p>
                </div>
              ))}
            </div>
          )}
        </TabsContent>
      </Tabs>

      <div className="flex justify-end">
        <Button variant="outline" asChild>
          <Link to="/members">Done</Link>
        </Button>
      </div>
    </div>
  )
}
