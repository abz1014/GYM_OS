using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Commands;

/// <summary>
/// The member correcting their own phone number.
///
/// Phone only, and that narrowness is the design. The staff UpdateMemberCommand rewrites the whole
/// record — name, email, date of birth, address — and handing that shape to the member role would
/// let the field that identifies them for login (email) and the field the gym bills against be
/// changed from the portal with no trace. The phone number is the one the member is most likely to
/// have changed and the gym most needs correct, since it is what the SMS and WhatsApp channels
/// address; a stale one means every reminder about their own membership goes to a dead handset.
/// </summary>
public record UpdateMyProfileCommand(string Phone) : ICommand<Unit>;

public class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator()
        => RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
}

public class UpdateMyProfileCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<UpdateMyProfileCommand, Unit>
{
    public async Task<Unit> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);

        var member = await db.Members.FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken)
            ?? throw new NotFoundException(nameof(Member), memberId);

        member.Phone = request.Phone.Trim();
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
