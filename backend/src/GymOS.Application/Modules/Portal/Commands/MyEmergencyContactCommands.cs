using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Commands;

/// <summary>
/// The member maintaining their own emergency contacts.
///
/// These existed as staff-only records, which meant the one piece of data whose whole purpose is to
/// be right on the worst day could only be corrected by someone else, during opening hours, by
/// asking. A member whose partner changed number had no way to fix it.
///
/// Every operation resolves the owning member from the JWT and then checks the contact belongs to
/// them. A contact id belonging to somebody else answers NOT FOUND, never forbidden — a 403 would
/// confirm the id exists, turning these endpoints into an oracle for probing other members' records.
/// Same convention as AchieveMyGoalCommand and CancelMyClassBookingCommand.
/// </summary>
public record AddMyEmergencyContactCommand(string Name, string Phone, string Relationship) : ICommand<Guid>;

public class AddMyEmergencyContactCommandValidator : AbstractValidator<AddMyEmergencyContactCommand>
{
    public AddMyEmergencyContactCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Relationship).NotEmpty().MaximumLength(100);
    }
}

public class AddMyEmergencyContactCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<AddMyEmergencyContactCommand, Guid>
{
    public async Task<Guid> Handle(AddMyEmergencyContactCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);

        var contact = new EmergencyContact
        {
            MemberId = memberId,
            Name = request.Name.Trim(),
            Phone = request.Phone.Trim(),
            Relationship = request.Relationship.Trim(),
        };

        db.EmergencyContacts.Add(contact);
        await db.SaveChangesAsync(cancellationToken);

        return contact.Id;
    }
}

/// <summary>Edits one of the member's OWN contacts. Someone else's id is a 404 — see the remarks on
/// <see cref="AddMyEmergencyContactCommand"/>.</summary>
public record UpdateMyEmergencyContactCommand(Guid Id, string Name, string Phone, string Relationship) : ICommand<Unit>;

public class UpdateMyEmergencyContactCommandValidator : AbstractValidator<UpdateMyEmergencyContactCommand>
{
    public UpdateMyEmergencyContactCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Relationship).NotEmpty().MaximumLength(100);
    }
}

public class UpdateMyEmergencyContactCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<UpdateMyEmergencyContactCommand, Unit>
{
    public async Task<Unit> Handle(UpdateMyEmergencyContactCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var contact = await FindOwnAsync(db, memberId, request.Id, cancellationToken);

        contact.Name = request.Name.Trim();
        contact.Phone = request.Phone.Trim();
        contact.Relationship = request.Relationship.Trim();

        // Email is left alone rather than cleared. The portal form does not collect it, and treating
        // "this form has no email field" as "the member deleted their contact's email" would quietly
        // destroy a detail staff entered at signup every time a phone number was corrected.
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }

    internal static async Task<EmergencyContact> FindOwnAsync(
        IApplicationDbContext db, Guid memberId, Guid contactId, CancellationToken cancellationToken)
    {
        var contact = await db.EmergencyContacts.FirstOrDefaultAsync(c => c.Id == contactId, cancellationToken);

        // One shape for both "no such contact" and "not yours": the caller cannot tell them apart,
        // which is the point.
        if (contact is null || contact.MemberId != memberId)
        {
            throw new NotFoundException(nameof(EmergencyContact), contactId);
        }

        return contact;
    }
}

/// <summary>Removes one of the member's OWN contacts. Someone else's id is a 404 — see the remarks
/// on <see cref="AddMyEmergencyContactCommand"/>.</summary>
public record DeleteMyEmergencyContactCommand(Guid Id) : ICommand<Unit>;

public class DeleteMyEmergencyContactCommandValidator : AbstractValidator<DeleteMyEmergencyContactCommand>
{
    public DeleteMyEmergencyContactCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public class DeleteMyEmergencyContactCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<DeleteMyEmergencyContactCommand, Unit>
{
    public async Task<Unit> Handle(DeleteMyEmergencyContactCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var contact = await UpdateMyEmergencyContactCommandHandler.FindOwnAsync(db, memberId, request.Id, cancellationToken);

        db.EmergencyContacts.Remove(contact);
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
