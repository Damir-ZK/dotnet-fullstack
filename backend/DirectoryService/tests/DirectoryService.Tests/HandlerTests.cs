using CSharpFunctionalExtensions;
using DirectoryService.Application.Departments;
using DirectoryService.Application.Locations;
using DirectoryService.Contracts;
using DirectoryService.Domain;
using DirectoryService.Domain.Common;
using DirectoryService.Domain.Departments;
using Xunit;

namespace DirectoryService.Tests;

public sealed class HandlerTests
{
    private sealed class FakeLocationRepository : ILocationRepository
    {
        public List<Location> Locations { get; } = [];
        public bool ShouldFailWithDbError { get; init; }

        public Task<Result<bool, Error>> NameExistsAsync(string name, CancellationToken cancellationToken) =>
            NameExistsAsync(name, null, cancellationToken);

        public Task<Result<bool, Error>> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
        {
            if (ShouldFailWithDbError)
            {
                return Task.FromResult(Result.Failure<bool, Error>(Error.Failure("database.error", "DB failure")));
            }

            return Task.FromResult(Result.Success<bool, Error>(Locations.Any(l => (!excludeId.HasValue || l.Id != excludeId.Value) && string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase))));
        }

        public Task<UnitResult<Error>> AddAsync(Location location, CancellationToken cancellationToken)
        {
            if (ShouldFailWithDbError)
            {
                return Task.FromResult(UnitResult.Failure(Error.Failure("database.error", "DB failure")));
            }

            Locations.Add(location);
            return Task.FromResult(UnitResult.Success<Error>());
        }

        public Task<Result<IReadOnlyList<Location>, Error>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<IReadOnlyList<Location>, Error>(Locations));

        public Task<Result<Location, Error>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            var loc = Locations.FirstOrDefault(l => l.Id == id);
            return Task.FromResult(loc is not null
                ? Result.Success<Location, Error>(loc)
                : Result.Failure<Location, Error>(Errors.Location.NotFound(id)));
        }

        public Task<UnitResult<Error>> UpdateAsync(Location location, CancellationToken cancellationToken) =>
            Task.FromResult(UnitResult.Success<Error>());

        public Task<UnitResult<Error>> DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            Locations.RemoveAll(l => l.Id == id);
            return Task.FromResult(UnitResult.Success<Error>());
        }

        public Task<UnitResult<Error>> UpdateLocationNameAsync(Guid id, string name, CancellationToken cancellationToken) =>
            Task.FromResult(UnitResult.Success<Error>());
    }

    private sealed class FakeDepartmentRepository : IDepartmentRepository
    {
        public List<Department> Departments { get; } = [];
        public HashSet<(Guid, Guid)> Links { get; } = [];

        public Task<Result<bool, Error>> NameExistsAsync(string name, CancellationToken cancellationToken) =>
            NameExistsAsync(name, null, cancellationToken);

        public Task<Result<bool, Error>> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<bool, Error>(Departments.Any(d => (!excludeId.HasValue || d.Id != excludeId.Value) && string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase))));

        public Task<UnitResult<Error>> AddAsync(Department department, IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken)
        {
            Departments.Add(department);
            foreach (var locId in locationIds)
            {
                Links.Add((department.Id, locId));
            }
            return Task.FromResult(UnitResult.Success<Error>());
        }

        public Task<Result<IReadOnlyList<Department>, Error>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<IReadOnlyList<Department>, Error>(Departments));

        public Task<Result<Department, Error>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            var dept = Departments.FirstOrDefault(d => d.Id == id);
            return Task.FromResult(dept is not null
                ? Result.Success<Department, Error>(dept)
                : Result.Failure<Department, Error>(Errors.Department.NotFound(id)));
        }

        public Task<UnitResult<Error>> UpdateAsync(Department department, CancellationToken cancellationToken) =>
            Task.FromResult(UnitResult.Success<Error>());

        public Task<UnitResult<Error>> DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            Departments.RemoveAll(d => d.Id == id);
            return Task.FromResult(UnitResult.Success<Error>());
        }

        public Task<UnitResult<Error>> UpdateDepartmentNameAsync(Guid id, string name, CancellationToken cancellationToken) =>
            Task.FromResult(UnitResult.Success<Error>());

        public Task<Result<bool, Error>> LocationLinkExistsAsync(Guid departmentId, Guid locationId, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<bool, Error>(Links.Contains((departmentId, locationId))));

        public Task<UnitResult<Error>> AddLocationLinkAsync(Guid departmentId, Guid locationId, CancellationToken cancellationToken)
        {
            Links.Add((departmentId, locationId));
            return Task.FromResult(UnitResult.Success<Error>());
        }

        public Task<UnitResult<Error>> RemoveLocationLinkAsync(Guid departmentId, Guid locationId, CancellationToken cancellationToken)
        {
            var removed = Links.Remove((departmentId, locationId));
            return Task.FromResult(removed
                ? UnitResult.Success<Error>()
                : UnitResult.Failure(Errors.Department.LocationNotLinked(departmentId, locationId)));
        }

        public Task<Result<IReadOnlyList<Guid>, Error>> GetLocationIdsAsync(Guid departmentId, CancellationToken cancellationToken)
        {
            IReadOnlyList<Guid> locIds = Links.Where(l => l.Item1 == departmentId).Select(l => l.Item2).ToList();
            return Task.FromResult(Result.Success<IReadOnlyList<Guid>, Error>(locIds));
        }
    }

    [Fact]
    public async Task CreateLocation_WithDuplicateName_ReturnsConflictError()
    {
        var repo = new FakeLocationRepository();
        repo.Locations.Add(Location.Create(Guid.NewGuid(), "Existing Office", "Address").Value);

        var handler = new CreateLocationHandler(repo, new CreateLocationCommandValidator());
        var result = await handler.Handle(new CreateLocationCommand("Existing Office", "New Address"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error[0].Type);
        Assert.Equal("location.already.exists", result.Error[0].Code);
    }

    [Fact]
    public async Task CreateLocation_WhenDatabaseFails_ReturnsDatabaseError()
    {
        var repo = new FakeLocationRepository { ShouldFailWithDbError = true };

        var handler = new CreateLocationHandler(repo, new CreateLocationCommandValidator());
        var result = await handler.Handle(new CreateLocationCommand("New Office", "Address"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Failure, result.Error[0].Type);
        Assert.Equal("database.error", result.Error[0].Code);
    }

    [Fact]
    public async Task CreateDepartment_WithNonExistentLocation_ReturnsNotFoundError()
    {
        var deptRepo = new FakeDepartmentRepository();
        var locRepo = new FakeLocationRepository();
        var nonExistentLocId = Guid.NewGuid();

        var handler = new CreateDepartmentHandler(deptRepo, locRepo, new CreateDepartmentCommandValidator());
        var result = await handler.Handle(
            new CreateDepartmentCommand("Engineering", "engineering", null, [nonExistentLocId]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error[0].Type);
        Assert.Equal("location.not.found", result.Error[0].Code);
    }

    [Fact]
    public async Task CreateDepartment_WithNonExistentParent_ReturnsNotFoundError()
    {
        var deptRepo = new FakeDepartmentRepository();
        var locRepo = new FakeLocationRepository();
        var nonExistentParentId = Guid.NewGuid();

        var handler = new CreateDepartmentHandler(deptRepo, locRepo, new CreateDepartmentCommandValidator());
        var result = await handler.Handle(
            new CreateDepartmentCommand("Engineering", "engineering", nonExistentParentId, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error[0].Type);
        Assert.Equal("department.parent.not.found", result.Error[0].Code);
    }

    [Fact]
    public async Task LinkDepartmentLocationHandler_WhenAlreadyLinked_ReturnsConflict()
    {
        var deptRepo = new FakeDepartmentRepository();
        var locRepo = new FakeLocationRepository();

        var dept = Department.Create(Guid.NewGuid(), "Finance", "finance", null).Value;
        var loc = Location.Create(Guid.NewGuid(), "HQ", "Main St").Value;
        deptRepo.Departments.Add(dept);
        locRepo.Locations.Add(loc);
        deptRepo.Links.Add((dept.Id, loc.Id));

        var handler = new LinkDepartmentLocationHandler(deptRepo, locRepo);
        var result = await handler.Handle(new LinkDepartmentLocationCommand(dept.Id, loc.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error[0].Type);
        Assert.Equal("department.location.already.linked", result.Error[0].Code);
    }

    [Fact]
    public async Task UnlinkDepartmentLocationHandler_WhenNotLinked_ReturnsNotFound()
    {
        var deptRepo = new FakeDepartmentRepository();
        var locRepo = new FakeLocationRepository();

        var dept = Department.Create(Guid.NewGuid(), "Finance", "finance", null).Value;
        var loc = Location.Create(Guid.NewGuid(), "HQ", "Main St").Value;
        deptRepo.Departments.Add(dept);
        locRepo.Locations.Add(loc);

        var handler = new UnlinkDepartmentLocationHandler(deptRepo, locRepo);
        var result = await handler.Handle(new UnlinkDepartmentLocationCommand(dept.Id, loc.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error[0].Type);
        Assert.Equal("department.location.not.linked", result.Error[0].Code);
    }

    [Fact]
    public async Task GetLocationsHandler_ReturnsAllLocations()
    {
        var repo = new FakeLocationRepository();
        repo.Locations.Add(Location.Create(Guid.NewGuid(), "HQ", "Main St").Value);

        var handler = new GetLocationsHandler(repo);
        var result = await handler.Handle(new GetLocationsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("HQ", result.Value[0].Name);
    }

    [Fact]
    public async Task GetLocationByIdHandler_WhenExists_ReturnsLocation()
    {
        var repo = new FakeLocationRepository();
        var loc = Location.Create(Guid.NewGuid(), "Branch", "Side St").Value;
        repo.Locations.Add(loc);

        var handler = new GetLocationByIdHandler(repo);
        var result = await handler.Handle(new GetLocationByIdQuery(loc.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(loc.Id, result.Value.Id);
        Assert.Equal("Branch", result.Value.Name);
    }

    [Fact]
    public async Task UpdateLocationHandler_WithValidData_UpdatesSuccessfully()
    {
        var repo = new FakeLocationRepository();
        var loc = Location.Create(Guid.NewGuid(), "Branch", "Old Address").Value;
        repo.Locations.Add(loc);

        var handler = new UpdateLocationHandler(repo, new UpdateLocationCommandValidator());
        var result = await handler.Handle(new UpdateLocationCommand(loc.Id, "Updated Branch", "New Address"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Branch", loc.Name);
    }

    [Fact]
    public async Task DeleteLocationHandler_DeletesSuccessfully()
    {
        var repo = new FakeLocationRepository();
        var loc = Location.Create(Guid.NewGuid(), "Branch", "Address").Value;
        repo.Locations.Add(loc);

        var handler = new DeleteLocationHandler(repo);
        var result = await handler.Handle(new DeleteLocationCommand(loc.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(repo.Locations);
    }

    [Fact]
    public async Task UpdateLocationNameHandler_UpdatesNameSuccessfully()
    {
        var repo = new FakeLocationRepository();
        var locId = Guid.NewGuid();

        var handler = new UpdateLocationNameHandler(repo, new UpdateLocationNameCommandValidator());
        var result = await handler.Handle(new UpdateLocationNameCommand(locId, "New Name"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(locId, result.Value);
    }

    [Fact]
    public async Task GetDepartmentsHandler_ReturnsAllDepartments()
    {
        var deptRepo = new FakeDepartmentRepository();
        deptRepo.Departments.Add(Department.Create(Guid.NewGuid(), "Eng", "eng", null).Value);

        var handler = new GetDepartmentsHandler(deptRepo);
        var result = await handler.Handle(new GetDepartmentsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Eng", result.Value[0].Name);
    }

    [Fact]
    public async Task GetDepartmentByIdHandler_WhenExists_ReturnsDepartment()
    {
        var deptRepo = new FakeDepartmentRepository();
        var dept = Department.Create(Guid.NewGuid(), "Sales", "sales", null).Value;
        deptRepo.Departments.Add(dept);

        var handler = new GetDepartmentByIdHandler(deptRepo);
        var result = await handler.Handle(new GetDepartmentByIdQuery(dept.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(dept.Id, result.Value.Id);
        Assert.Equal("Sales", result.Value.Name);
    }

    [Fact]
    public async Task UpdateDepartmentHandler_WithValidData_UpdatesSuccessfully()
    {
        var deptRepo = new FakeDepartmentRepository();
        var dept = Department.Create(Guid.NewGuid(), "Old Name", "slug", null).Value;
        deptRepo.Departments.Add(dept);

        var handler = new UpdateDepartmentHandler(deptRepo, new UpdateDepartmentCommandValidator());
        var result = await handler.Handle(new UpdateDepartmentCommand(dept.Id, "New Name"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", dept.Name);
    }

    [Fact]
    public async Task DeleteDepartmentHandler_DeletesSuccessfully()
    {
        var deptRepo = new FakeDepartmentRepository();
        var dept = Department.Create(Guid.NewGuid(), "Dept", "dept", null).Value;
        deptRepo.Departments.Add(dept);

        var handler = new DeleteDepartmentHandler(deptRepo);
        var result = await handler.Handle(new DeleteDepartmentCommand(dept.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(deptRepo.Departments);
    }

    [Fact]
    public async Task UpdateDepartmentNameHandler_UpdatesNameSuccessfully()
    {
        var deptRepo = new FakeDepartmentRepository();
        var deptId = Guid.NewGuid();

        var handler = new UpdateDepartmentNameHandler(deptRepo, new UpdateDepartmentNameCommandValidator());
        var result = await handler.Handle(new UpdateDepartmentNameCommand(deptId, "New Name"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(deptId, result.Value);
    }

    [Fact]
    public async Task GetDepartmentByIdHandler_WhenDepartmentHasLocations_ReturnsActualLocationIds()
    {
        var deptRepo = new FakeDepartmentRepository();
        var dept = Department.Create(Guid.NewGuid(), "Engineering", "engineering", null).Value;
        var locId1 = Guid.NewGuid();
        var locId2 = Guid.NewGuid();
        deptRepo.Departments.Add(dept);
        deptRepo.Links.Add((dept.Id, locId1));
        deptRepo.Links.Add((dept.Id, locId2));

        var handler = new GetDepartmentByIdHandler(deptRepo);
        var result = await handler.Handle(new GetDepartmentByIdQuery(dept.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.LocationIds);
        Assert.Equal(2, result.Value.LocationIds.Count);
        Assert.Contains(locId1, result.Value.LocationIds);
        Assert.Contains(locId2, result.Value.LocationIds);
    }

    [Fact]
    public async Task UpdateDepartmentHandler_WithDuplicateName_ReturnsConflictError()
    {
        var deptRepo = new FakeDepartmentRepository();
        var dept1 = Department.Create(Guid.NewGuid(), "HR", "hr", null).Value;
        var dept2 = Department.Create(Guid.NewGuid(), "Finance", "finance", null).Value;
        deptRepo.Departments.Add(dept1);
        deptRepo.Departments.Add(dept2);

        var handler = new UpdateDepartmentHandler(deptRepo, new UpdateDepartmentCommandValidator());
        var result = await handler.Handle(new UpdateDepartmentCommand(dept2.Id, "HR"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error[0].Type);
        Assert.Equal("department.already.exists", result.Error[0].Code);
    }

    [Fact]
    public async Task UpdateLocationHandler_WithDuplicateName_ReturnsConflictError()
    {
        var locRepo = new FakeLocationRepository();
        var loc1 = Location.Create(Guid.NewGuid(), "HQ", "123 Main St").Value;
        var loc2 = Location.Create(Guid.NewGuid(), "Branch", "456 Side St").Value;
        locRepo.Locations.Add(loc1);
        locRepo.Locations.Add(loc2);

        var handler = new UpdateLocationHandler(locRepo, new UpdateLocationCommandValidator());
        var result = await handler.Handle(new UpdateLocationCommand(loc2.Id, "HQ", "789 Other St"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error[0].Type);
        Assert.Equal("location.already.exists", result.Error[0].Code);
    }
}
