using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Members.Commands;

public record TransferMemberCommand(Guid MemberId, Guid NewBranchId) : ICommand<Unit>;

public class TransferMemberCommandValidator : AbstractValidator<TransferMemberCommand>
{
    public TransferMemberCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.NewBranchId).NotEmpty();
    }
}

public class TransferMemberCommandHandler(IApplicationDbContext db) : IRequestHandler<TransferMemberCommand, Unit>
{
    public async Task<Unit> Handle(TransferMemberCommand request, CancellationToken cancellationToken)
    {
        var member = await db.Members.FirstOrDefaultAsync(m => m.Id == request.MemberId, cancellationToken)
            ?? throw new NotFoundException(nameof(Member), request.MemberId);

        var branchExists = await db.Branches.AnyAsync(b => b.Id == request.NewBranchId, cancellationToken);
        if (!branchExists)
        {
            throw new NotFoundException(nameof(Domain.Tenancy.Branch), request.NewBranchId);
        }

        member.BranchId = request.NewBranchId;

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
