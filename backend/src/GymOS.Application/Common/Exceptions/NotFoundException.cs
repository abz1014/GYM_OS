namespace GymOS.Application.Common.Exceptions;

public class NotFoundException(string entityName, object key)
    : Exception($"Entity \"{entityName}\" ({key}) was not found.");
