using CSharpFunctionalExtensions;
using DirectoryService.Application;
using DirectoryService.Application.Common;
using DirectoryService.Application.Departments;
using DirectoryService.Application.Locations;
using DirectoryService.Contracts;
using DirectoryService.Domain;
using DirectoryService.Domain.Common;
using DirectoryService.Domain.Departments;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DirectoryService.Tests;

public sealed class DependencyInjectionTests
{
    private sealed class DummyLocationRepository : ILocationRepository
    {
        public Task<Result<bool, Error>> NameExistsAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<bool, Error>(false));

        public Task<Result<bool, Error>> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<bool, Error>(false));

        public Task<UnitResult<Error>> AddAsync(Location location, CancellationToken cancellationToken) =>
            Task.FromResult(UnitResult.Success<Error>());

        public Task<Result<IReadOnlyList<Location>, Error>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<IReadOnlyList<Location>, Error>([]));

        public Task<Result<Location, Error>> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure<Location, Error>(Errors.Location.NotFound(id)));

        public Task<UnitResult<Error>> UpdateAsync(Location location, CancellationToken cancellationToken) =>
            Task.FromResult(UnitResult.Success<Error>());

        public Task<UnitResult<Error>> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(UnitResult.Success<Error>());
    }

    private sealed class DummyDepartmentRepository : IDepartmentRepository
    {
        public Task<Result<bool, Error>> NameExistsAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<bool, Error>(false));

        public Task<Result<bool, Error>> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<bool, Error>(false));

        public Task<UnitResult<Error>> AddAsync(Department department, IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken) =>
            Task.FromResult(UnitResult.Success<Error>());

        public Task<Result<IReadOnlyList<Department>, Error>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<IReadOnlyList<Department>, Error>([]));

        public Task<Result<Department, Error>> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure<Department, Error>(Errors.Department.NotFound(id)));

        public Task<UnitResult<Error>> UpdateAsync(Department department, CancellationToken cancellationToken) =>
            Task.FromResult(UnitResult.Success<Error>());

        public Task<UnitResult<Error>> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(UnitResult.Success<Error>());

        public Task<Result<bool, Error>> LocationLinkExistsAsync(Guid departmentId, Guid locationId, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<bool, Error>(false));

        public Task<UnitResult<Error>> AddLocationLinkAsync(Guid departmentId, Guid locationId, CancellationToken cancellationToken) =>
            Task.FromResult(UnitResult.Success<Error>());

        public Task<UnitResult<Error>> RemoveLocationLinkAsync(Guid departmentId, Guid locationId, CancellationToken cancellationToken) =>
            Task.FromResult(UnitResult.Success<Error>());

        public Task<Result<IReadOnlyList<Guid>, Error>> GetLocationIdsAsync(Guid departmentId, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<IReadOnlyList<Guid>, Error>([]));
    }

    [Fact]
    public void AddApplication_RegistersAllCommandAndQueryHandlersAndValidators()
    {
        var services = new ServiceCollection();
        services.AddScoped<ILocationRepository, DummyLocationRepository>();
        services.AddScoped<IDepartmentRepository, DummyDepartmentRepository>();

        services.AddApplication();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        // Location command handlers
        Assert.NotNull(sp.GetService<ICommandHandler<CreateLocationCommand, Guid>>());
        Assert.NotNull(sp.GetService<CreateLocationHandler>());
        Assert.NotNull(sp.GetService<ICommandHandler<UpdateLocationCommand>>());
        Assert.NotNull(sp.GetService<UpdateLocationHandler>());
        Assert.NotNull(sp.GetService<ICommandHandler<DeleteLocationCommand>>());
        Assert.NotNull(sp.GetService<DeleteLocationHandler>());
        Assert.NotNull(sp.GetService<ICommandHandler<UpdateLocationNameCommand, Guid>>());
        Assert.NotNull(sp.GetService<UpdateLocationNameHandler>());

        // Location query handlers
        Assert.NotNull(sp.GetService<IQueryHandler<GetLocationsQuery, IReadOnlyList<LocationDto>>>());
        Assert.NotNull(sp.GetService<GetLocationsHandler>());
        Assert.NotNull(sp.GetService<IQueryHandler<GetLocationByIdQuery, LocationDto>>());
        Assert.NotNull(sp.GetService<GetLocationByIdHandler>());

        // Department command handlers
        Assert.NotNull(sp.GetService<ICommandHandler<CreateDepartmentCommand, DepartmentDto>>());
        Assert.NotNull(sp.GetService<CreateDepartmentHandler>());
        Assert.NotNull(sp.GetService<ICommandHandler<UpdateDepartmentCommand>>());
        Assert.NotNull(sp.GetService<UpdateDepartmentHandler>());
        Assert.NotNull(sp.GetService<ICommandHandler<DeleteDepartmentCommand>>());
        Assert.NotNull(sp.GetService<DeleteDepartmentHandler>());
        Assert.NotNull(sp.GetService<ICommandHandler<UpdateDepartmentNameCommand, Guid>>());
        Assert.NotNull(sp.GetService<UpdateDepartmentNameHandler>());
        Assert.NotNull(sp.GetService<ICommandHandler<LinkDepartmentLocationCommand>>());
        Assert.NotNull(sp.GetService<LinkDepartmentLocationHandler>());
        Assert.NotNull(sp.GetService<ICommandHandler<UnlinkDepartmentLocationCommand>>());
        Assert.NotNull(sp.GetService<UnlinkDepartmentLocationHandler>());

        // Department query handlers
        Assert.NotNull(sp.GetService<IQueryHandler<GetDepartmentsQuery, IReadOnlyList<DepartmentDto>>>());
        Assert.NotNull(sp.GetService<GetDepartmentsHandler>());
        Assert.NotNull(sp.GetService<IQueryHandler<GetDepartmentByIdQuery, DepartmentDto>>());
        Assert.NotNull(sp.GetService<GetDepartmentByIdHandler>());

        // Validators
        Assert.NotNull(sp.GetService<IValidator<CreateLocationCommand>>());
        Assert.NotNull(sp.GetService<IValidator<UpdateLocationCommand>>());
        Assert.NotNull(sp.GetService<IValidator<UpdateLocationNameCommand>>());
        Assert.NotNull(sp.GetService<IValidator<CreateDepartmentCommand>>());
        Assert.NotNull(sp.GetService<IValidator<UpdateDepartmentCommand>>());
        Assert.NotNull(sp.GetService<IValidator<UpdateDepartmentNameCommand>>());
    }
}
