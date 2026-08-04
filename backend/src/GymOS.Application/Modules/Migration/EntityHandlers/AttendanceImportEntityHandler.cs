using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Modules.Attendance.Commands;
using GymOS.Domain.Attendance;
using GymOS.Domain.Migration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Migration.EntityHandlers;

/// <summary>
/// Imports a historical check-in against an already-existing member (resolved by email).
/// Delegates to ImportAttendanceRecordCommand rather than the live CheckInCommand — see that
/// command's doc comment for why (no dashboard notification, explicit historical timestamp instead
/// of "now").
/// </summary>
public class AttendanceImportEntityHandler(IApplicationDbContext db) : IImportEntityHandler
{
    public ImportEntityType EntityType => ImportEntityType.Attendance;

    public IReadOnlyList<string> RequiredFields { get; } = ["MemberEmail", "CheckInAt"];

    public IReadOnlyList<string> OptionalFields { get; } = ["CheckOutAt"];

    public string? GetNaturalKey(IReadOnlyDictionary<string, string> fields)
    {
        if (!fields.TryGetValue("MemberEmail", out var email) || string.IsNullOrWhiteSpace(email)
            || !fields.TryGetValue("CheckInAt", out var checkInAt) || string.IsNullOrWhiteSpace(checkInAt))
        {
            return null;
        }

        return $"{email.Trim().ToLowerInvariant()}|{checkInAt.Trim()}";
    }

    public async Task<ImportValidationResult> ValidateAsync(IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        foreach (var required in RequiredFields)
        {
            if (!fields.TryGetValue(required, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return ImportValidationResult.Invalid($"Missing required field '{required}'.");
            }
        }

        var email = fields["MemberEmail"];
        var member = await db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == email, cancellationToken);
        if (member is null)
        {
            return ImportValidationResult.Invalid($"No member found with email '{email}'. Import members first.");
        }

        if (!ImportDateTimeParsing.TryParseUtc(fields["CheckInAt"], out var checkInAt))
        {
            return ImportValidationResult.Invalid($"'{fields["CheckInAt"]}' is not a valid date/time for CheckInAt.");
        }

        if (fields.TryGetValue("CheckOutAt", out var checkOutRaw) && !string.IsNullOrWhiteSpace(checkOutRaw))
        {
            if (!ImportDateTimeParsing.TryParseUtc(checkOutRaw, out var checkOutAt))
            {
                return ImportValidationResult.Invalid($"'{checkOutRaw}' is not a valid date/time for CheckOutAt.");
            }

            if (checkOutAt < checkInAt)
            {
                return ImportValidationResult.Invalid("CheckOutAt cannot be before CheckInAt.");
            }
        }

        var duplicate = await db.AttendanceRecords.AnyAsync(a => a.MemberId == member.Id && a.CheckInAt == checkInAt, cancellationToken);
        if (duplicate)
        {
            return ImportValidationResult.Duplicate($"'{email}' already has a check-in recorded at {checkInAt:yyyy-MM-dd HH:mm}.");
        }

        return ImportValidationResult.Ok();
    }

    public async Task<Guid> CommitAsync(IReadOnlyDictionary<string, string> fields, Guid branchId, ISender sender, CancellationToken cancellationToken)
    {
        var member = await db.Members.AsNoTracking().FirstAsync(m => m.Email == fields["MemberEmail"], cancellationToken);

        ImportDateTimeParsing.TryParseUtc(fields["CheckInAt"], out var checkInAt);

        DateTimeOffset? checkOutAt = fields.TryGetValue("CheckOutAt", out var co) && !string.IsNullOrWhiteSpace(co)
            && ImportDateTimeParsing.TryParseUtc(co, out var coParsed)
            ? coParsed
            : null;

        return await sender.Send(new ImportAttendanceRecordCommand(member.Id, branchId, checkInAt, checkOutAt), cancellationToken);
    }

    public async Task RollbackAsync(Guid mappedEntityId, CancellationToken cancellationToken)
    {
        var record = await db.AttendanceRecords.FirstOrDefaultAsync(a => a.Id == mappedEntityId, cancellationToken)
            ?? throw new NotFoundException(nameof(AttendanceRecord), mappedEntityId);

        // No soft-state field exists on AttendanceRecord (unlike Trainer.IsActive or Asset.Status),
        // so hard-delete is the only option — matching the pattern the interface's own comment
        // anticipates for entities without a natural "cancelled" state.
        db.AttendanceRecords.Remove(record);
        await db.SaveChangesAsync(cancellationToken);
    }
}
