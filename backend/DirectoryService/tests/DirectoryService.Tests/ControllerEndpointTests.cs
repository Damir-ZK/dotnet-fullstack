using System.Text.Json;
using CSharpFunctionalExtensions;
using DirectoryService.Application.Departments;
using DirectoryService.Application.Locations;
using DirectoryService.Contracts;
using DirectoryService.Domain;
using DirectoryService.Domain.Common;
using DirectoryService.Domain.Departments;
using DirectoryService.Presentation;
using DirectoryService.Presentation.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DirectoryService.Tests;

public sealed class ControllerEndpointTests
{
    private sealed class FakeLocationRepository : ILocationRepository
    {
        public List<Location> Locations { get; } = [];

        public Task<Result<bool, Error>> NameExistsAsync(string name, CancellationToken cancellationToken) =>
            NameExistsAsync(name, null, cancellationToken);

        public Task<Result<bool, Error>> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<bool, Error>(Locations.Any(l => (!excludeId.HasValue || l.Id != excludeId.Value) && string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase))));

        public Task<UnitResult<Error>> AddAsync(Location location, CancellationToken cancellationToken)
        {
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

        public Task<UnitResult<Error>> UpdateAsync(Location location, CancellationToken cancellationToken)
        {
            var index = Locations.FindIndex(l => l.Id == location.Id);
            if (index >= 0)
            {
                Locations[index] = location;
            }
            return Task.FromResult(UnitResult.Success<Error>());
        }

        public Task<UnitResult<Error>> DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            Locations.RemoveAll(l => l.Id == id);
            return Task.FromResult(UnitResult.Success<Error>());
        }
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

        public Task<UnitResult<Error>> UpdateAsync(Department department, CancellationToken cancellationToken)
        {
            var index = Departments.FindIndex(d => d.Id == department.Id);
            if (index >= 0)
            {
                Departments[index] = department;
            }
            return Task.FromResult(UnitResult.Success<Error>());
        }

        public Task<UnitResult<Error>> DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            Departments.RemoveAll(d => d.Id == id);
            return Task.FromResult(UnitResult.Success<Error>());
        }

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

    private static LocationsController CreateLocationsController(ILocationRepository locRepo)
    {
        var createLocationHandler = new CreateLocationHandler(locRepo, new CreateLocationCommandValidator());
        var getLocationsHandler = new GetLocationsHandler(locRepo);
        var getLocationByIdHandler = new GetLocationByIdHandler(locRepo);
        var updateLocationHandler = new UpdateLocationHandler(locRepo, new UpdateLocationCommandValidator());
        var deleteLocationHandler = new DeleteLocationHandler(locRepo);
        var updateLocationNameHandler = new UpdateLocationNameHandler(locRepo, new UpdateLocationNameCommandValidator());

        return new LocationsController(
            createLocationHandler,
            getLocationsHandler,
            getLocationByIdHandler,
            updateLocationHandler,
            deleteLocationHandler,
            updateLocationNameHandler);
    }

    private static DepartmentsController CreateDepartmentsController(IDepartmentRepository deptRepo, ILocationRepository locRepo)
    {
        var createDepartmentHandler = new CreateDepartmentHandler(deptRepo, locRepo, new CreateDepartmentCommandValidator());
        var getDepartmentsHandler = new GetDepartmentsHandler(deptRepo);
        var getDepartmentByIdHandler = new GetDepartmentByIdHandler(deptRepo);
        var updateDepartmentHandler = new UpdateDepartmentHandler(deptRepo, new UpdateDepartmentCommandValidator());
        var deleteDepartmentHandler = new DeleteDepartmentHandler(deptRepo);
        var updateDepartmentNameHandler = new UpdateDepartmentNameHandler(deptRepo, new UpdateDepartmentNameCommandValidator());

        return new DepartmentsController(
            createDepartmentHandler,
            getDepartmentsHandler,
            getDepartmentByIdHandler,
            updateDepartmentHandler,
            deleteDepartmentHandler,
            updateDepartmentNameHandler);
    }

    private static DepartmentLocationsController CreateDepartmentLocationsController(IDepartmentRepository deptRepo, ILocationRepository locRepo)
    {
        var linkHandler = new LinkDepartmentLocationHandler(deptRepo, locRepo);
        var unlinkHandler = new UnlinkDepartmentLocationHandler(deptRepo, locRepo);

        return new DepartmentLocationsController(linkHandler, unlinkHandler);
    }

    [Fact]
    public async Task PostLocations_WithValidData_Returns201AndEnvelopeWithCreatedId()
    {
        var locRepo = new FakeLocationRepository();
        var controller = CreateLocationsController(locRepo);

        var result = await controller.CreateLocationDto(new CreateLocationDto("Headquarters", "123 Main St"), CancellationToken.None);

        var envelopeResult = Assert.IsType<EnvelopeResult<Guid>>(result);
        Assert.Equal(StatusCodes.Status201Created, envelopeResult.StatusCode);
        Assert.NotEqual(Guid.Empty, envelopeResult.Envelope.Result);
        Assert.Null(envelopeResult.Envelope.Errors);
    }

    [Fact]
    public async Task PostLocations_WithEmptyNameAndInvalidAddress_Returns400AndEnvelopeWithValidationErrors()
    {
        var locRepo = new FakeLocationRepository();
        var controller = CreateLocationsController(locRepo);

        var result = await controller.CreateLocationDto(new CreateLocationDto(string.Empty, string.Empty), CancellationToken.None);

        var envelopeResult = Assert.IsType<EnvelopeResult<Guid>>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, envelopeResult.StatusCode);
        Assert.Equal(Guid.Empty, envelopeResult.Envelope.Result);
        Assert.NotNull(envelopeResult.Envelope.Errors);
        Assert.NotEmpty(envelopeResult.Envelope.Errors);
        Assert.All(envelopeResult.Envelope.Errors, err => Assert.Equal(ErrorType.Validation, err.Type));
    }

    [Fact]
    public async Task GetDepartmentById_ForNonExistentDepartment_Returns404AndEnvelopeWithNotFoundError()
    {
        var deptRepo = new FakeDepartmentRepository();
        var locRepo = new FakeLocationRepository();
        var controller = CreateDepartmentsController(deptRepo, locRepo);
        var missingId = Guid.NewGuid();

        var result = await controller.GetDepartmentById(missingId, CancellationToken.None);

        var envelopeResult = Assert.IsType<EnvelopeResult<DepartmentDto>>(result);
        Assert.Equal(StatusCodes.Status404NotFound, envelopeResult.StatusCode);
        Assert.Null(envelopeResult.Envelope.Result);
        Assert.NotNull(envelopeResult.Envelope.Errors);
        var error = Assert.Single(envelopeResult.Envelope.Errors);
        Assert.Equal("department.not.found", error.Code);
    }

    [Fact]
    public async Task DepartmentLocations_LinkAlreadyLinkedLocation_Returns409AndEnvelopeWithConflictError()
    {
        var deptRepo = new FakeDepartmentRepository();
        var locRepo = new FakeLocationRepository();

        var dept = Department.Create(Guid.NewGuid(), "Finance", "finance", null).Value;
        var loc = Location.Create(Guid.NewGuid(), "HQ", "Main St").Value;
        deptRepo.Departments.Add(dept);
        locRepo.Locations.Add(loc);
        deptRepo.Links.Add((dept.Id, loc.Id));

        var controller = CreateDepartmentLocationsController(deptRepo, locRepo);

        var result = await controller.LinkLocation(dept.Id, loc.Id, CancellationToken.None);

        var envelopeResult = Assert.IsType<EnvelopeResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, envelopeResult.StatusCode);
        var envelope = Assert.IsType<Envelope>(envelopeResult.Envelope);
        Assert.Null(envelope.Result);
        Assert.NotNull(envelope.Errors);
        var error = Assert.Single(envelope.Errors);
        Assert.Equal("department.location.already.linked", error.Code);
    }

    [Fact]
    public async Task GlobalExceptionHandler_UnhandledException_Returns500AndEnvelopeFormatWithoutLeakingDetails()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var context = new DefaultHttpContext();
        var stream = new MemoryStream();
        context.Response.Body = stream;

        var exception = new InvalidOperationException("Secret database connection string or internal trace details");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

        stream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("result", out var resultProp));
        Assert.Equal(JsonValueKind.Null, resultProp.ValueKind);

        Assert.True(root.TryGetProperty("errors", out var errorsProp));
        Assert.Equal(JsonValueKind.Array, errorsProp.ValueKind);
        Assert.Equal(1, errorsProp.GetArrayLength());

        var firstError = errorsProp[0];
        Assert.Equal("internal.server.error", firstError.GetProperty("code").GetString());
        Assert.Equal("An unexpected error occurred.", firstError.GetProperty("message").GetString());
        Assert.DoesNotContain("Secret", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PatchLocationName_WithValidName_Returns200AndEnvelopeWithId()
    {
        var locRepo = new FakeLocationRepository();
        var loc = Location.Create(Guid.NewGuid(), "Old Location", "123 Main St").Value;
        locRepo.Locations.Add(loc);

        var controller = CreateLocationsController(locRepo);
        var result = await controller.UpdateLocationName(loc.Id, new UpdateLocationNameRequest("New Location"), CancellationToken.None);

        var envelopeResult = Assert.IsType<EnvelopeResult<Guid>>(result);
        Assert.Equal(StatusCodes.Status200OK, envelopeResult.StatusCode);
        Assert.Equal(loc.Id, envelopeResult.Envelope.Result);
        Assert.Null(envelopeResult.Envelope.Errors);
        Assert.Equal("New Location", locRepo.Locations[0].Name);
    }

    [Fact]
    public async Task PatchDepartmentName_WithValidName_Returns200AndEnvelopeWithId()
    {
        var deptRepo = new FakeDepartmentRepository();
        var locRepo = new FakeLocationRepository();
        var dept = Department.Create(Guid.NewGuid(), "Old Dept", "old-dept", null).Value;
        deptRepo.Departments.Add(dept);

        var controller = CreateDepartmentsController(deptRepo, locRepo);
        var result = await controller.UpdateDepartmentName(dept.Id, new UpdateDepartmentNameRequest("New Dept"), CancellationToken.None);

        var envelopeResult = Assert.IsType<EnvelopeResult<Guid>>(result);
        Assert.Equal(StatusCodes.Status200OK, envelopeResult.StatusCode);
        Assert.Equal(dept.Id, envelopeResult.Envelope.Result);
        Assert.Null(envelopeResult.Envelope.Errors);
        Assert.Equal("New Dept", deptRepo.Departments[0].Name);
    }
}
