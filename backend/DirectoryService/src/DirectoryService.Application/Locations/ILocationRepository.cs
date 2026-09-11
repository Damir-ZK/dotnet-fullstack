using CSharpFunctionalExtensions;
using DirectoryService.Domain;
using DirectoryService.Domain.Common;

namespace DirectoryService.Application.Locations;

public interface ILocationRepository
{
    Task<Result<bool, Error>> NameExistsAsync(string name, CancellationToken cancellationToken);
    Task<Result<bool, Error>> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken);
    Task<UnitResult<Error>> AddAsync(Location location, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<Location>, Error>> GetAllAsync(CancellationToken cancellationToken);
    Task<Result<Location, Error>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<UnitResult<Error>> UpdateAsync(Location location, CancellationToken cancellationToken);
    Task<UnitResult<Error>> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
