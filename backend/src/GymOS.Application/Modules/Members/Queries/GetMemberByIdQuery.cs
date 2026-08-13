using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Members.Dtos;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Members.Queries;

public record GetMemberByIdQuery(Guid Id) : IQuery<MemberDetailDto>;

public class GetMemberByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetMemberByIdQuery, MemberDetailDto>
{
    public async Task<MemberDetailDto> Handle(GetMemberByIdQuery request, CancellationToken cancellationToken)
    {
        var member = await db.Members.AsNoTracking()
            .Include(m => m.EmergencyContacts)
            .Include(m => m.MedicalNotes)
            .Include(m => m.Measurements)
            .Include(m => m.ProgressPhotos)
            .Include(m => m.MemberMemberships).ThenInclude(mm => mm.MembershipPlan)
            .Include(m => m.ReferredByMember)
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Member), request.Id);

        /*
         * The active coaching pairing, newest first when several are open. Its own query rather than
         * another Include because assignments hang off Trainer, not Member. The name comes from the
         * trainer's linked User — the same resolution GetMyCoachQuery uses — and the Trainer join
         * carries the global branch filter, so a cross-branch pairing resolves to null here exactly
         * as it does everywhere else.
         */
        var coach = await db.TrainerAssignments.AsNoTracking()
            .Where(a => a.MemberId == member.Id && a.IsActive)
            .OrderByDescending(a => a.StartDate)
            .Select(a => new
            {
                a.TrainerId,
                TrainerName = a.Trainer!.User!.FirstName + " " + a.Trainer.User.LastName
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new MemberDetailDto(
            member.Id, member.MemberCode, member.FirstName, member.LastName, member.DateOfBirth,
            member.Gender, member.Email, member.Phone, member.Address, member.ProfilePhotoUrl,
            member.JoinDate, member.Status, member.QrCodeToken, member.BranchId,
            member.ReferredByMemberId, member.ReferredByMember?.FullName,
            coach?.TrainerId, coach?.TrainerName,
            member.EmergencyContacts.Select(c => new EmergencyContactDto(c.Id, c.Name, c.Relationship, c.Phone, c.Email)).ToList(),
            member.MedicalNotes.Select(n => new MedicalNoteDto(n.Id, n.Note, n.RecordedByUserId, n.RecordedAt)).ToList(),
            member.Measurements.Select(x => new MemberMeasurementDto(
                x.Id, x.MeasuredOn, x.WeightKg, x.BodyFatPercentage, x.ChestCm, x.WaistCm, x.HipCm, x.ArmCm, x.ThighCm, x.Notes)).ToList(),
            member.ProgressPhotos.Select(p => new ProgressPhotoDto(p.Id, p.PhotoUrl, p.TakenAt, p.Notes)).ToList(),
            member.MemberMemberships.Select(mm => new MemberMembershipDto(
                mm.Id, mm.MembershipPlanId, mm.MembershipPlan?.Name ?? string.Empty, mm.StartDate, mm.EndDate,
                mm.Status, mm.AutoRenew, mm.FreezeStartDate, mm.FreezeEndDate,
                mm.FreezeDaysUsed, mm.MembershipPlan?.MaxFreezeDays,
                mm.PricePaid, mm.Currency, mm.InvoiceId,
                mm.CancellationReason)).ToList());
    }
}
