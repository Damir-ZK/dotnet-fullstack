using CSharpFunctionalExtensions;
using DirectoryService.Application.Locations;
using DirectoryService.Domain;
using DirectoryService.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DirectoryService.Infrastructure.Postgres.Repositories;

public sealed class EfCoreLocationRepository : ILocationRepository
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<EfCoreLocationRepository> _logger;

    public EfCoreLocationRepository(AppDbContext dbContext, ILogger<EfCoreLocationRepository> logger)
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
            var query = _dbContext.Locations
                .Where(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));

            if (excludeId.HasValue)
            {
                query = query.Where(l => l.Id != excludeId.Value);
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
            _logger.LogError(ex, "Failed to check if location name {Name} exists.", name);
            return Errors.General.Database("A database error occurred while checking if location name exists.");
        }
    }

    public async Task<UnitResult<Error>> AddAsync(Location location, CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.Locations.AddAsync(location, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return UnitResult.Success<Error>();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save location {LocationId} with name {Name}", location.Id, location.Name);
            return Errors.General.Database("A database error occurred while saving location.");
        }
    }

    public async Task<Result<IReadOnlyList<Location>, Error>> GetAllAsync(CancellationToken cancellationToken)
    {
        try
        {
            var locations = await _dbContext.Locations
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            return Result.Success<IReadOnlyList<Location>, Error>(locations);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch all locations.");
            return Errors.General.Database("A database error occurred while fetching locations.");
        }
    }

    public async Task<Result<Location, Error>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var location = await _dbContext.Locations
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

            if (location is null)
            {
                return Errors.Location.NotFound(id);
            }

            return Result.Success<Location, Error>(location);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch location by id {LocationId}", id);
            return Errors.General.Database("A database error occurred while fetching location.");
        }
    }

    public async Task<UnitResult<Error>> UpdateAsync(Location location, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _dbContext.Locations
                .Where(l => l.Id == location.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(l => l.Name, location.Name)
                    .SetProperty(l => l.Address, location.Address)
                    .SetProperty(l => l.UpdatedAt, location.UpdatedAt), cancellationToken);

            if (rows == 0)
            {
                return Errors.Location.NotFound(location.Id);
            }

            return UnitResult.Success<Error>();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update location {LocationId}", location.Id);
            return Errors.General.Database("A database error occurred while updating location.");
        }
    }

    public async Task<UnitResult<Error>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _dbContext.Locations
                .Where(l => l.Id == id)
                .ExecuteDeleteAsync(cancellationToken);

            if (rows == 0)
            {
                return Errors.Location.NotFound(id);
            }

            return UnitResult.Success<Error>();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete location {LocationId}", id);
            return Errors.General.Database("A database error occurred while deleting location.");
        }
    }
}
