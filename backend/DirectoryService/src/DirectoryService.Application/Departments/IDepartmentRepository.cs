using CSharpFunctionalExtensions;
using DirectoryService.Domain.Common;
using DirectoryService.Domain.Departments;

namespace DirectoryService.Application.Departments;

public interface IDepartmentRepository
{
    Task<Result<bool, Error>> NameExistsAsync(string name, CancellationToken cancellationToken);
    Task<Result<bool, Error>> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken);
    Task<UnitResult<Error>> AddAsync(Department department, IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<Department>, Error>> GetAllAsync(CancellationToken cancellationToken);
    Task<Result<Department, Error>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<UnitResult<Error>> UpdateAsync(Department department, CancellationToken cancellationToken);
    Task<UnitResult<Error>> DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<UnitResult<Error>> UpdateDepartmentNameAsync(Guid id, string name, CancellationToken cancellationToken);
    Task<Result<bool, Error>> LocationLinkExistsAsync(Guid departmentId, Guid locationId, CancellationToken cancellationToken);
    Task<UnitResult<Error>> AddLocationLinkAsync(Guid departmentId, Guid locationId, CancellationToken cancellationToken);
    Task<UnitResult<Error>> RemoveLocationLinkAsync(Guid departmentId, Guid locationId, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<Guid>, Error>> GetLocationIdsAsync(Guid departmentId, CancellationToken cancellationToken);
}
