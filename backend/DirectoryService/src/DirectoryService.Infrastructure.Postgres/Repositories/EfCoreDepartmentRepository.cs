using CSharpFunctionalExtensions;
using DirectoryService.Application.Departments;
using DirectoryService.Domain.Common;
using DirectoryService.Domain.Departments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DirectoryService.Infrastructure.Postgres.Repositories;

public sealed class EfCoreDepartmentRepository : IDepartmentRepository
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<EfCoreDepartmentRepository> _logger;

    public EfCoreDepartmentRepository(AppDbContext dbContext, ILogger<EfCoreDepartmentRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public Task<Result<bool, Error>> NameExistsAsync(string name, CancellationToken cancellationToken) =>
        NameExistsAsync(name, null, cancellationToken);

    public async Task<Result<bool, Error>> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        try
        {
            var query = _dbContext.Departments
                .Where(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));

            if (excludeId.HasValue)
            {
                query = query.Where(d => d.Id != excludeId.Value);
            }

            var exists = await query.AnyAsync(cancellationToken);
            return Result.Success<bool, Error>(exists);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if department name {Name} exists.", name);
            return Errors.General.Database("A database error occurred while checking if department name exists.");
        }
    }

    public async Task<UnitResult<Error>> AddAsync(Department department, IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await _dbContext.Departments.AddAsync(department, cancellationToken);

            foreach (var locationId in locationIds.Distinct())
            {
                var departmentLocationResult = DepartmentLocation.Create(
                    Guid.NewGuid(),
                    department.Id,
                    locationId,
                    isPrimaryLocation: false);

                if (departmentLocationResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return departmentLocationResult.Error;
                }

                await _dbContext.DepartmentLocations.AddAsync(departmentLocationResult.Value, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return UnitResult.Success<Error>();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save department {DepartmentId} with name {Name}", department.Id, department.Name);
            await transaction.RollbackAsync(cancellationToken);
            return Errors.General.Database("A database error occurred while saving department.");
        }
    }

    public async Task<UnitResult<Error>> UpdateAsync(Department department, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _dbContext.Departments
                .Where(d => d.Id == department.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.Name, department.Name)
                    .SetProperty(d => d.UpdatedAt, department.UpdatedAt), cancellationToken);

            if (rows == 0)
            {
                return Errors.Department.NotFound(department.Id);
            }

            return UnitResult.Success<Error>();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update department {DepartmentId}", department.Id);
            return Errors.General.Database("A database error occurred while updating department.");
        }
    }

    public async Task<UnitResult<Error>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _dbContext.Departments
                .Where(d => d.Id == id)
                .ExecuteDeleteAsync(cancellationToken);

            if (rows == 0)
            {
                return Errors.Department.NotFound(id);
            }

            return UnitResult.Success<Error>();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete department {DepartmentId}", id);
            return Errors.General.Database("A database error occurred while deleting department.");
        }
    }

    public async Task<Result<IReadOnlyList<Department>, Error>> GetAllAsync(CancellationToken cancellationToken)
    {
        try
        {
            var departments = await _dbContext.Departments
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return Result.Success<IReadOnlyList<Department>, Error>(departments);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch all departments.");
            return Errors.General.Database("A database error occurred while fetching departments.");
        }
    }

    public async Task<Result<Department, Error>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var department = await _dbContext.Departments
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

            if (department is null)
            {
                return Errors.Department.NotFound(id);
            }

            return Result.Success<Department, Error>(department);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch department by id {DepartmentId}", id);
            return Errors.General.Database("A database error occurred while fetching department.");
        }
    }

    public async Task<UnitResult<Error>> UpdateDepartmentNameAsync(Guid id, string name, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _dbContext.Departments
                .Where(d => d.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.Name, name)
                    .SetProperty(d => d.UpdatedAt, DateTime.UtcNow), cancellationToken);

            if (rows == 0)
            {
                return Errors.Department.NotFound(id);
            }

            return UnitResult.Success<Error>();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update department name for {DepartmentId}", id);
            return Errors.General.Database("A database error occurred while updating department name.");
        }
    }

    public async Task<Result<bool, Error>> LocationLinkExistsAsync(Guid departmentId, Guid locationId, CancellationToken cancellationToken)
    {
        try
        {
            var exists = await _dbContext.DepartmentLocations
                .AnyAsync(link => link.DepartmentId == departmentId && link.LocationId == locationId, cancellationToken);
            return Result.Success<bool, Error>(exists);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if location link exists for Department {DepartmentId} and Location {LocationId}", departmentId, locationId);
            return Errors.General.Database("A database error occurred while checking department location link.");
        }
    }

    public async Task<UnitResult<Error>> AddLocationLinkAsync(Guid departmentId, Guid locationId, CancellationToken cancellationToken)
    {
        try
        {
            var createResult = DepartmentLocation.Create(Guid.NewGuid(), departmentId, locationId, isPrimaryLocation: false);
            if (createResult.IsFailure)
            {
                return createResult.Error;
            }

            await _dbContext.DepartmentLocations.AddAsync(createResult.Value, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return UnitResult.Success<Error>();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add location link for Department {DepartmentId} and Location {LocationId}", departmentId, locationId);
            return Errors.General.Database("A database error occurred while adding department location link.");
        }
    }

    public async Task<UnitResult<Error>> RemoveLocationLinkAsync(Guid departmentId, Guid locationId, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _dbContext.DepartmentLocations
                .Where(link => link.DepartmentId == departmentId && link.LocationId == locationId)
                .ExecuteDeleteAsync(cancellationToken);

            if (rows == 0)
            {
                return Errors.Department.LocationNotLinked(departmentId, locationId);
            }

            return UnitResult.Success<Error>();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove location link for Department {DepartmentId} and Location {LocationId}", departmentId, locationId);
            return Errors.General.Database("A database error occurred while removing department location link.");
        }
    }

    public async Task<Result<IReadOnlyList<Guid>, Error>> GetLocationIdsAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        try
        {
            var locationIds = await _dbContext.DepartmentLocations
                .Where(link => link.DepartmentId == departmentId)
                .Select(link => link.LocationId)
                .ToListAsync(cancellationToken);

            return Result.Success<IReadOnlyList<Guid>, Error>(locationIds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch location ids for Department {DepartmentId}", departmentId);
            return Errors.General.Database("A database error occurred while fetching department locations.");
        }
    }
}
