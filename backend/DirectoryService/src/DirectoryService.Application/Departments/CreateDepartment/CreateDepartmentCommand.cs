using DirectoryService.Application.Common;
using DirectoryService.Contracts;

// ReSharper disable once CheckNamespace
namespace DirectoryService.Application.Departments;

public sealed record CreateDepartmentCommand(
    string Name,
    string Slug,
    Guid? ParentId,
    IReadOnlyList<Guid>? LocationIds) : ICommand<DepartmentDto>;
