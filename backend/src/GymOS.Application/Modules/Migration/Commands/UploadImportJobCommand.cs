using System.Text;
using System.Text.Json;
using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Migration.Dtos;
using GymOS.Domain.Migration;
using MediatR;

namespace GymOS.Application.Modules.Migration.Commands;

public record UploadImportJobCommand(ImportEntityType EntityType, string FileName, string FileContent) : ICommand<ImportJobDetailDto>;

public class UploadImportJobCommandValidator : AbstractValidator<UploadImportJobCommand>
{
    public UploadImportJobCommandValidator()
    {
        RuleFor(x => x.FileName).NotEmpty();
        RuleFor(x => x.FileContent).NotEmpty();
    }
}

public class UploadImportJobCommandHandler(IApplicationDbContext db, IObjectStorage objectStorage, ICurrentUserService currentUser)
    : IRequestHandler<UploadImportJobCommand, ImportJobDetailDto>
{
    public async Task<ImportJobDetailDto> Handle(UploadImportJobCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var rows = CsvParser.ParseRows(request.FileContent);
        if (rows.Count == 0)
        {
            throw new ValidationException("The file is empty.");
        }

        var headers = rows[0];
        var dataRows = rows.Skip(1).ToList();

        var job = new ImportJob
        {
            TenantId = tenantId,
            EntityType = request.EntityType,
            FileName = request.FileName,
            Status = ImportStatus.Uploaded,
            TotalRows = dataRows.Count,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = currentUser.UserId
        };

        db.ImportJobs.Add(job);

        var storageKey = $"imports/{tenantId}/{job.Id}/{request.FileName}";
        await using var contentStream = new MemoryStream(Encoding.UTF8.GetBytes(request.FileContent));
        var fileUrl = await objectStorage.UploadAsync(storageKey, contentStream, "text/csv", cancellationToken);
        job.FileUrl = fileUrl;

        for (var i = 0; i < dataRows.Count; i++)
        {
            var rowValues = dataRows[i];
            var rowData = new Dictionary<string, string>();

            for (var col = 0; col < headers.Length; col++)
            {
                rowData[headers[col]] = col < rowValues.Length ? rowValues[col] : string.Empty;
            }

            db.ImportRows.Add(new ImportRow
            {
                ImportJobId = job.Id,
                RowNumber = i + 1,
                RawDataJson = JsonSerializer.Serialize(rowData),
                Status = ImportRowStatus.Pending
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        return new ImportJobDetailDto(
            job.Id, job.EntityType, job.FileName, job.Status, job.TotalRows, job.ValidRows, job.DuplicateRows, job.ErrorRows,
            job.CreatedAt, job.CommittedAt, job.RolledBackAt, headers, []);
    }
}
