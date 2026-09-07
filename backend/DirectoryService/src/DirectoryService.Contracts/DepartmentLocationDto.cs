namespace DirectoryService.Contracts;

public sealed record DepartmentLocationDto(Guid Id, Guid DepartmentId, Guid LocationId, bool IsPrimaryLocation);