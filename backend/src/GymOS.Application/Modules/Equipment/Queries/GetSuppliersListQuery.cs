using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Equipment.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Equipment.Queries;

public record GetSuppliersListQuery : IQuery<List<SupplierDto>>;

public class GetSuppliersListQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSuppliersListQuery, List<SupplierDto>>
{
    public Task<List<SupplierDto>> Handle(GetSuppliersListQuery request, CancellationToken cancellationToken)
        => db.Suppliers.AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SupplierDto(s.Id, s.Name, s.ContactName, s.Phone, s.Email))
            .ToListAsync(cancellationToken);
}
