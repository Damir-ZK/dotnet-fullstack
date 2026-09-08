using System.Data;
using CSharpFunctionalExtensions;
using Dapper;
using DirectoryService.Application.Locations;
using DirectoryService.Domain;
using DirectoryService.Domain.Common;
using Microsoft.Extensions.Logging;

namespace DirectoryService.Infrastructure.Postgres.Repositories;

public class DapperLocationRepository : ILocationRepository
{
    private readonly IDbConnection _connection;
    private readonly ILogger<DapperLocationRepository> _logger;

    public DapperLocationRepository(IDbConnection connection, ILogger<DapperLocationRepository> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public Task<Result<bool, Error>> NameExistsAsync(string name, CancellationToken cancellationToken) =>
        NameExistsAsync(name, null, cancellationToken);

    public async Task<Result<bool, Error>> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT EXISTS(
                               SELECT 1
                               FROM locations
                               WHERE lower(name) = lower(@Name) AND (@ExcludeId IS NULL OR id != @ExcludeId)
                           )
                           """;

        try
        {
            var exists = await _connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(sql, new { Name = name, ExcludeId = excludeId }, cancellationToken: cancellationToken));
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
        const string sql = """
                           INSERT INTO locations (id, name, address, created_at, updated_at)
                           VALUES (@Id, @Name, @Address, @CreatedAt, @UpdatedAt)
                           """;

        try
        {
            await _connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                location.Id,
                location.Name,
                location.Address,
                location.CreatedAt,
                location.UpdatedAt
            }, cancellationToken: cancellationToken));

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
        const string sql = """
                           SELECT id, name, address, created_at AS CreatedAt, updated_at AS UpdatedAt
                           FROM locations
                           """;

        try
        {
            var locations = await _connection.QueryAsync<Location>(
                new CommandDefinition(sql, cancellationToken: cancellationToken));
            return Result.Success<IReadOnlyList<Location>, Error>(locations.AsList());
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
        const string sql = """
                           SELECT id, name, address, created_at AS CreatedAt, updated_at AS UpdatedAt
                           FROM locations
                           WHERE id = @Id
                           """;

        try
        {
            var location = await _connection.QuerySingleOrDefaultAsync<Location>(
                new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));

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
        const string updateSql = """
                                 UPDATE locations
                                 SET name = @Name, address = @Address, updated_at = @UpdatedAt
                                 WHERE id = @Id
                                 """;

        try
        {
            var rows = await _connection.ExecuteAsync(
                new CommandDefinition(
                    updateSql,
                    new { location.Id, location.Name, location.Address, location.UpdatedAt },
                    cancellationToken: cancellationToken));

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
        const string deleteSql = """
                                 DELETE FROM locations
                                 WHERE id = @Id
                                 """;

        try
        {
            var rows = await _connection.ExecuteAsync(
                new CommandDefinition(
                    deleteSql,
                    new { Id = id },
                    cancellationToken: cancellationToken));

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

    public async Task<UnitResult<Error>> UpdateLocationNameAsync(Guid id, string name,
        CancellationToken cancellationToken)
    {
        const string updateNameSql = """
                                     UPDATE locations
                                     SET name = @Name, updated_at = NOW()
                                     WHERE id = @Id
                                     """;

        try
        {
            var rows = await _connection.ExecuteAsync(
                new CommandDefinition(
                    updateNameSql,
                    new { Id = id, Name = name },
                    cancellationToken: cancellationToken));

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
            _logger.LogError(ex, "Failed to update location name for {LocationId}", id);
            return Errors.General.Database("A database error occurred while updating location name.");
        }
    }
}
