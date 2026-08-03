using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Members.Commands;

public record AddEmergencyContactCommand(Guid MemberId, string Name, string Relationship, string Phone, string? Email) : ICommand<Guid>;

public class AddEmergencyContactCommandValidator : AbstractValidator<AddEmergencyContactCommand>
{
    public AddEmergencyContactCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Phone).NotEmpty();
    }
}

public class AddEmergencyContactCommandHandler(IApplicationDbContext db) : IRequestHandler<AddEmergencyContactCommand, Guid>
{
    public async Task<Guid> Handle(AddEmergencyContactCommand request, CancellationToken cancellationToken)
    {
        await EnsureMemberExists(db, request.MemberId, cancellationToken);

        var contact = new EmergencyContact
        {
            MemberId = request.MemberId,
            Name = request.Name,
            Relationship = request.Relationship,
            Phone = request.Phone,
            Email = request.Email
        };

        db.EmergencyContacts.Add(contact);
        await db.SaveChangesAsync(cancellationToken);
        return contact.Id;
    }

    internal static async Task EnsureMemberExists(IApplicationDbContext db, Guid memberId, CancellationToken cancellationToken)
    {
        if (!await db.Members.AnyAsync(m => m.Id == memberId, cancellationToken))
        {
            throw new NotFoundException(nameof(Member), memberId);
        }
    }
}

public record AddMedicalNoteCommand(Guid MemberId, string Note, Guid? RecordedByUserId) : ICommand<Guid>;

public class AddMedicalNoteCommandValidator : AbstractValidator<AddMedicalNoteCommand>
{
    public AddMedicalNoteCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.Note).NotEmpty();
    }
}

public class AddMedicalNoteCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AddMedicalNoteCommand, Guid>
{
    public async Task<Guid> Handle(AddMedicalNoteCommand request, CancellationToken cancellationToken)
    {
        await AddEmergencyContactCommandHandler.EnsureMemberExists(db, request.MemberId, cancellationToken);

        var note = new MedicalNote
        {
            MemberId = request.MemberId,
            Note = request.Note,
            RecordedByUserId = request.RecordedByUserId,
            RecordedAt = dateTimeProvider.UtcNow
        };

        db.MedicalNotes.Add(note);
        await db.SaveChangesAsync(cancellationToken);
        return note.Id;
    }
}

public record AddMeasurementCommand(
    Guid MemberId, DateOnly MeasuredOn, decimal? WeightKg, decimal? BodyFatPercentage,
    decimal? ChestCm, decimal? WaistCm, decimal? HipCm, decimal? ArmCm, decimal? ThighCm, string? Notes) : ICommand<Guid>;

public class AddMeasurementCommandValidator : AbstractValidator<AddMeasurementCommand>
{
    public AddMeasurementCommandValidator() => RuleFor(x => x.MemberId).NotEmpty();
}

public class AddMeasurementCommandHandler(IApplicationDbContext db) : IRequestHandler<AddMeasurementCommand, Guid>
{
    public async Task<Guid> Handle(AddMeasurementCommand request, CancellationToken cancellationToken)
    {
        await AddEmergencyContactCommandHandler.EnsureMemberExists(db, request.MemberId, cancellationToken);

        var measurement = new MemberMeasurement
        {
            MemberId = request.MemberId,
            MeasuredOn = request.MeasuredOn,
            WeightKg = request.WeightKg,
            BodyFatPercentage = request.BodyFatPercentage,
            ChestCm = request.ChestCm,
            WaistCm = request.WaistCm,
            HipCm = request.HipCm,
            ArmCm = request.ArmCm,
            ThighCm = request.ThighCm,
            Notes = request.Notes
        };

        db.MemberMeasurements.Add(measurement);
        await db.SaveChangesAsync(cancellationToken);
        return measurement.Id;
    }
}

public record AddProgressPhotoCommand(Guid MemberId, string PhotoUrl, string? Notes) : ICommand<Guid>;

public class AddProgressPhotoCommandValidator : AbstractValidator<AddProgressPhotoCommand>
{
    public AddProgressPhotoCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.PhotoUrl).NotEmpty();
    }
}

public class AddProgressPhotoCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AddProgressPhotoCommand, Guid>
{
    public async Task<Guid> Handle(AddProgressPhotoCommand request, CancellationToken cancellationToken)
    {
        await AddEmergencyContactCommandHandler.EnsureMemberExists(db, request.MemberId, cancellationToken);

        var photo = new ProgressPhoto
        {
            MemberId = request.MemberId,
            PhotoUrl = request.PhotoUrl,
            Notes = request.Notes,
            TakenAt = dateTimeProvider.UtcNow
        };

        db.ProgressPhotos.Add(photo);
        await db.SaveChangesAsync(cancellationToken);
        return photo.Id;
    }
}
