using System.Data;
using CSharpFunctionalExtensions;
using Dapper;
using DirectoryService.Application.Departments;
using DirectoryService.Domain.Common;
using DirectoryService.Domain.Departments;
using Microsoft.Extensions.Logging;

namespace DirectoryService.Infrastructure.Postgres.Repositories;

public sealed class DapperDepartmentRepository : IDepartmentRepository
{
    private readonly IDbConnection _connection;
    private readonly ILogger<DapperDepartmentRepository> _logger;

    public DapperDepartmentRepository(IDbConnection connection, ILogger<DapperDepartmentRepository> logger)
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
            FROM departments
            WHERE lower(name) = lower(@Name) AND (@ExcludeId IS NULL OR id != @ExcludeId)
        )
        """;

        try
        {
            var exists = await _connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(sql, new { Name = name, ExcludeId = excludeId }, cancellationToken: cancellationToken));
            return Result.Success<bool, Error>(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if department name {Name} exists.", name);
            return Errors.General.Database("A database error occurred while checking if department name exists.");
        }
    }

    public async Task<UnitResult<Error>> AddAsync(Department department, IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken)
    {
        const string departmentSql = """
        INSERT INTO departments (id, name, slug, path, parent_id, created_at, updated_at)
        VALUES (@Id, @Name, @Slug, @Path, @ParentId, @CreatedAt, @UpdatedAt)
        """;

        const string departmentLocationSql = """
        INSERT INTO department_locations (id, department_id, location_id, is_primary_location)
        VALUES (@Id, @DepartmentId, @LocationId, @IsPrimaryLocation)
        """;

        var transaction = _connection.BeginTransaction();

        try
        {
            await _connection.ExecuteAsync(
                new CommandDefinition(
                    departmentSql,
                    new
                    {
                        department.Id,
                        department.Name,
                        department.Slug,
                        department.Path,
                        department.ParentId,
                        department.CreatedAt,
                        department.UpdatedAt
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            foreach (var locationId in locationIds.Distinct())
            {
                await _connection.ExecuteAsync(
                    new CommandDefinition(
                        departmentLocationSql,
                        new
                        {
                            Id = Guid.NewGuid(),
                            DepartmentId = department.Id,
                            LocationId = locationId,
                            IsPrimaryLocation = false
                        },
                        transaction: transaction,
                        cancellationToken: cancellationToken));
            }

            transaction.Commit();
            return UnitResult.Success<Error>();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Failed to save department {DepartmentId} with name {Name}", department.Id, department.Name);
            return Errors.General.Database("A database error occurred while saving department.");
        }
        finally
        {
            transaction.Dispose();
        }
    }

    public async Task<UnitResult<Error>> UpdateAsync(Department department, CancellationToken cancellationToken)
    {
        const string updateSql = """
        UPDATE departments
        SET name = @Name, updated_at = @UpdatedAt
        WHERE id = @Id
        """;

        try
        {
            var rows = await _connection.ExecuteAsync(
                new CommandDefinition(
                    updateSql,
                    new { department.Id, department.Name, department.UpdatedAt },
                    cancellationToken: cancellationToken));

            if (rows == 0)
            {
                return Errors.Department.NotFound(department.Id);
            }

            return UnitResult.Success<Error>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update department {DepartmentId}", department.Id);
            return Errors.General.Database("A database error occurred while updating department.");
        }
    }

    public async Task<UnitResult<Error>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        const string deleteSql = """
        DELETE FROM departments
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
                return Errors.Department.NotFound(id);
            }

            return UnitResult.Success<Error>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete department {DepartmentId}", id);
            return Errors.General.Database("A database error occurred while deleting department.");
        }
    }

    public async Task<Result<IReadOnlyList<Department>, Error>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = """
        SELECT id, name, slug, path, parent_id AS ParentId, created_at AS CreatedAt, updated_at AS UpdatedAt
        FROM departments
        """;

        try
        {
            var departments = await _connection.QueryAsync<Department>(
                new CommandDefinition(sql, cancellationToken: cancellationToken));

            return Result.Success<IReadOnlyList<Department>, Error>(departments.AsList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch all departments.");
            return Errors.General.Database("A database error occurred while fetching departments.");
        }
    }

    public async Task<Result<Department, Error>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
        SELECT id, name, slug, path, parent_id AS ParentId, created_at AS CreatedAt, updated_at AS UpdatedAt
        FROM departments
        WHERE id = @Id
        """;

        try
        {
            var department = await _connection.QuerySingleOrDefaultAsync<Department>(
                new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));

            if (department is null)
            {
                return Errors.Department.NotFound(id);
            }

            return Result.Success<Department, Error>(department);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch department by id {DepartmentId}", id);
            return Errors.General.Database("A database error occurred while fetching department.");
        }
    }

    public async Task<UnitResult<Error>> UpdateDepartmentNameAsync(Guid id, string name, CancellationToken cancellationToken)
    {
        const string updateNameSql = """
        UPDATE departments
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
                return Errors.Department.NotFound(id);
            }

            return UnitResult.Success<Error>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update department name for {DepartmentId}", id);
            return Errors.General.Database("A database error occurred while updating department name.");
        }
    }

    public async Task<Result<bool, Error>> LocationLinkExistsAsync(Guid departmentId, Guid locationId, CancellationToken cancellationToken)
    {
        const string sql = """
        SELECT EXISTS(
            SELECT 1
            FROM department_locations
            WHERE department_id = @DepartmentId AND location_id = @LocationId
        )
        """;

        try
        {
            var exists = await _connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(sql, new { DepartmentId = departmentId, LocationId = locationId }, cancellationToken: cancellationToken));
            return Result.Success<bool, Error>(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if location link exists for Department {DepartmentId} and Location {LocationId}", departmentId, locationId);
            return Errors.General.Database("A database error occurred while checking department location link.");
        }
    }

    public async Task<UnitResult<Error>> AddLocationLinkAsync(Guid departmentId, Guid locationId, CancellationToken cancellationToken)
    {
        const string sql = """
        INSERT INTO department_locations (id, department_id, location_id, is_primary_location)
        VALUES (@Id, @DepartmentId, @LocationId, FALSE)
        """;

        try
        {
            await _connection.ExecuteAsync(
                new CommandDefinition(
                    sql,
                    new { Id = Guid.NewGuid(), DepartmentId = departmentId, LocationId = locationId },
                    cancellationToken: cancellationToken));
            return UnitResult.Success<Error>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add location link for Department {DepartmentId} and Location {LocationId}", departmentId, locationId);
            return Errors.General.Database("A database error occurred while adding department location link.");
        }
    }

    public async Task<UnitResult<Error>> RemoveLocationLinkAsync(Guid departmentId, Guid locationId, CancellationToken cancellationToken)
    {
        const string sql = """
        DELETE FROM department_locations
        WHERE department_id = @DepartmentId AND location_id = @LocationId
        """;

        try
        {
            var rows = await _connection.ExecuteAsync(
                new CommandDefinition(sql, new { DepartmentId = departmentId, LocationId = locationId }, cancellationToken: cancellationToken));

            if (rows == 0)
            {
                return Errors.Department.LocationNotLinked(departmentId, locationId);
            }

            return UnitResult.Success<Error>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove location link for Department {DepartmentId} and Location {LocationId}", departmentId, locationId);
            return Errors.General.Database("A database error occurred while removing department location link.");
        }
    }

    public async Task<Result<IReadOnlyList<Guid>, Error>> GetLocationIdsAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        const string sql = """
        SELECT location_id
        FROM department_locations
        WHERE department_id = @DepartmentId
        """;

        try
        {
            var locationIds = await _connection.QueryAsync<Guid>(
                new CommandDefinition(sql, new { DepartmentId = departmentId }, cancellationToken: cancellationToken));

            return Result.Success<IReadOnlyList<Guid>, Error>(locationIds.AsList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch location ids for Department {DepartmentId}", departmentId);
            return Errors.General.Database("A database error occurred while fetching department locations.");
        }
    }
}
