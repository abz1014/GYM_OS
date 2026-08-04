using GymOS.Domain.Classes;

namespace GymOS.Application.Modules.Classes.Dtos;

public record ClassTypeDto(
    Guid Id, string Name, string? Description, int DefaultDurationMinutes, int DefaultCapacity, string? ColorHex, bool IsActive);

public record ClassScheduleDto(
    Guid Id, Guid ClassTypeId, string ClassTypeName, string? ColorHex, Guid? TrainerId, string? TrainerName,
    DayOfWeek DayOfWeek, TimeOnly StartTime, int DurationMinutes, int Capacity, string? Location, bool IsActive);

public record ClassSessionDto(
    Guid Id, Guid? ClassScheduleId, Guid ClassTypeId, string ClassTypeName, string? ColorHex, Guid? TrainerId,
    string? TrainerName, DateTimeOffset StartsAt, int DurationMinutes, int Capacity, string? Location, ClassSessionStatus Status);
