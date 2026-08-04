using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Classes.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Classes.Queries;

public record GetClassTypesListQuery : IQuery<List<ClassTypeDto>>;

public class GetClassTypesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetClassTypesListQuery, List<ClassTypeDto>>
{
    public async Task<List<ClassTypeDto>> Handle(GetClassTypesListQuery request, CancellationToken cancellationToken) =>
        await db.ClassTypes.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new ClassTypeDto(
                t.Id, t.Name, t.Description, t.DefaultDurationMinutes, t.DefaultCapacity, t.ColorHex, t.IsActive))
            .ToListAsync(cancellationToken);
}
