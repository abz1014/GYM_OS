using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Trainers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Trainers.Commands;

public record AddTrainerRatingCommand(Guid TrainerId, Guid MemberId, int Score, string? Comment) : ICommand<Guid>;

public class AddTrainerRatingCommandValidator : AbstractValidator<AddTrainerRatingCommand>
{
    public AddTrainerRatingCommandValidator()
    {
        RuleFor(x => x.TrainerId).NotEmpty();
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.Score).InclusiveBetween(1, 5);
    }
}

public class AddTrainerRatingCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AddTrainerRatingCommand, Guid>
{
    public async Task<Guid> Handle(AddTrainerRatingCommand request, CancellationToken cancellationToken)
    {
        var trainerExists = await db.Trainers.AnyAsync(t => t.Id == request.TrainerId, cancellationToken);
        if (!trainerExists)
        {
            throw new NotFoundException(nameof(Trainer), request.TrainerId);
        }

        var rating = new TrainerRating
        {
            TrainerId = request.TrainerId,
            MemberId = request.MemberId,
            Score = request.Score,
            Comment = request.Comment,
            RatedAt = dateTimeProvider.UtcNow
        };

        db.TrainerRatings.Add(rating);
        await db.SaveChangesAsync(cancellationToken);

        return rating.Id;
    }
}
