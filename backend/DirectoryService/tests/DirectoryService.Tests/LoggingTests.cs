using CSharpFunctionalExtensions;
using DirectoryService.Application.Departments;
using DirectoryService.Application.Locations;
using DirectoryService.Contracts;
using DirectoryService.Domain;
using DirectoryService.Domain.Common;
using DirectoryService.Domain.Departments;
using DirectoryService.Presentation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Xunit;

namespace DirectoryService.Tests;

public sealed class LoggingTests
{
    public sealed class TestLogger<T> : ILogger<T>
    {
        private readonly List<TestLogEntry> _entries = [];

        public IReadOnlyList<TestLogEntry> Entries => _entries;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            var properties = new Dictionary<string, object?>(StringComparer.Ordinal);

            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                foreach (var kvp in pairs)
                {
                    properties[kvp.Key] = kvp.Value;
                }
            }

            _entries.Add(new TestLogEntry(logLevel, message, properties, exception));
        }
    }

    public sealed record TestLogEntry(
        LogLevel Level,
        string Message,
        IReadOnlyDictionary<string, object?> Properties,
        Exception? Exception);

    private sealed class TestDiagnosticContext : IDiagnosticContext
    {
        public Dictionary<string, object?> Properties { get; } = new(StringComparer.Ordinal);

        public void Set(string propertyName, object? value, bool destructureObjects = false)
        {
            Properties[propertyName] = value;
        }

        public void SetException(Exception exception) { }
    }

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
                return Task.FromResult(Result.Failure<bool, Error>(Errors.General.Database("Database failure")));
            }

            return Task.FromResult(Result.Success<bool, Error>(Locations.Any(l => (!excludeId.HasValue || l.Id != excludeId.Value) && string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase))));
        }

        public Task<UnitResult<Error>> AddAsync(Location location, CancellationToken cancellationToken)
        {
            if (ShouldFailWithDbError)
            {
                return Task.FromResult(UnitResult.Failure(Errors.General.Database("Database failure")));
            }

            Locations.Add(location);
            return Task.FromResult(UnitResult.Success<Error>());
        }

        public Task<Result<IReadOnlyList<Location>, Error>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<IReadOnlyList<Location>, Error>(Locations));

        public Task<Result<Location, Error>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            if (ShouldFailWithDbError)
            {
                return Task.FromResult(Result.Failure<Location, Error>(Errors.General.Database("Database failure")));
            }

            var loc = Locations.FirstOrDefault(l => l.Id == id);
            return Task.FromResult(loc is not null
                ? Result.Success<Location, Error>(loc)
                : Result.Failure<Location, Error>(Errors.Location.NotFound(id)));
        }

        public Task<UnitResult<Error>> UpdateAsync(Location location, CancellationToken cancellationToken)
        {
            if (ShouldFailWithDbError)
            {
                return Task.FromResult(UnitResult.Failure(Errors.General.Database("Database failure")));
            }

            return Task.FromResult(UnitResult.Success<Error>());
        }

        public Task<UnitResult<Error>> DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            if (ShouldFailWithDbError)
            {
                return Task.FromResult(UnitResult.Failure(Errors.General.Database("Database failure")));
            }

            var removed = Locations.RemoveAll(l => l.Id == id);
            return Task.FromResult(removed > 0
                ? UnitResult.Success<Error>()
                : UnitResult.Failure(Errors.Location.NotFound(id)));
        }

        public Task<UnitResult<Error>> UpdateLocationNameAsync(Guid id, string name, CancellationToken cancellationToken)
        {
            if (ShouldFailWithDbError)
            {
                return Task.FromResult(UnitResult.Failure(Errors.General.Database("Database failure")));
            }

            var loc = Locations.FirstOrDefault(l => l.Id == id);
            if (loc is null)
            {
                return Task.FromResult(UnitResult.Failure(Errors.Location.NotFound(id)));
            }

            return Task.FromResult(UnitResult.Success<Error>());
        }
    }

    private sealed class FakeDepartmentRepository : IDepartmentRepository
    {
        public List<Department> Departments { get; } = [];
        public HashSet<(Guid, Guid)> Links { get; } = [];
        public bool ShouldFailWithDbError { get; init; }

        public Task<Result<bool, Error>> NameExistsAsync(string name, CancellationToken cancellationToken) =>
            NameExistsAsync(name, null, cancellationToken);

        public Task<Result<bool, Error>> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
        {
            if (ShouldFailWithDbError)
            {
                return Task.FromResult(Result.Failure<bool, Error>(Errors.General.Database("Database failure")));
            }

            return Task.FromResult(Result.Success<bool, Error>(Departments.Any(d => (!excludeId.HasValue || d.Id != excludeId.Value) && string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase))));
        }

        public Task<UnitResult<Error>> AddAsync(Department department, IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken)
        {
            if (ShouldFailWithDbError)
            {
                return Task.FromResult(UnitResult.Failure(Errors.General.Database("Database failure")));
            }

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
            if (ShouldFailWithDbError)
            {
                return Task.FromResult(Result.Failure<Department, Error>(Errors.General.Database("Database failure")));
            }

            var dept = Departments.FirstOrDefault(d => d.Id == id);
            return Task.FromResult(dept is not null
                ? Result.Success<Department, Error>(dept)
                : Result.Failure<Department, Error>(Errors.Department.NotFound(id)));
        }

        public Task<UnitResult<Error>> UpdateAsync(Department department, CancellationToken cancellationToken)
        {
            if (ShouldFailWithDbError)
            {
                return Task.FromResult(UnitResult.Failure(Errors.General.Database("Database failure")));
            }

            return Task.FromResult(UnitResult.Success<Error>());
        }

        public Task<UnitResult<Error>> DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            if (ShouldFailWithDbError)
            {
                return Task.FromResult(UnitResult.Failure(Errors.General.Database("Database failure")));
            }

            var removed = Departments.RemoveAll(d => d.Id == id);
            return Task.FromResult(removed > 0
                ? UnitResult.Success<Error>()
                : UnitResult.Failure(Errors.Department.NotFound(id)));
        }

        public Task<Result<bool, Error>> LocationLinkExistsAsync(Guid departmentId, Guid locationId, CancellationToken cancellationToken)
        {
            if (ShouldFailWithDbError)
            {
                return Task.FromResult(Result.Failure<bool, Error>(Errors.General.Database("Database failure")));
            }

            return Task.FromResult(Result.Success<bool, Error>(Links.Contains((departmentId, locationId))));
        }

        public Task<UnitResult<Error>> AddLocationLinkAsync(Guid departmentId, Guid locationId, CancellationToken cancellationToken)
        {
            if (ShouldFailWithDbError)
            {
                return Task.FromResult(UnitResult.Failure(Errors.General.Database("Database failure")));
            }

            Links.Add((departmentId, locationId));
            return Task.FromResult(UnitResult.Success<Error>());
        }

        public Task<UnitResult<Error>> RemoveLocationLinkAsync(Guid departmentId, Guid locationId, CancellationToken cancellationToken)
        {
            if (ShouldFailWithDbError)
            {
                return Task.FromResult(UnitResult.Failure(Errors.General.Database("Database failure")));
            }

            var removed = Links.Remove((departmentId, locationId));
            return Task.FromResult(removed
                ? UnitResult.Success<Error>()
                : UnitResult.Failure(Errors.Department.LocationNotLinked(departmentId, locationId)));
        }

        public Task<Result<IReadOnlyList<Guid>, Error>> GetLocationIdsAsync(Guid departmentId, CancellationToken cancellationToken)
        {
            if (ShouldFailWithDbError)
            {
                return Task.FromResult(Result.Failure<IReadOnlyList<Guid>, Error>(Errors.General.Database("Database failure")));
            }

            IReadOnlyList<Guid> locIds = Links.Where(l => l.Item1 == departmentId).Select(l => l.Item2).ToList();
            return Task.FromResult(Result.Success<IReadOnlyList<Guid>, Error>(locIds));
        }
    }

    [Fact]
    public async Task CreateLocation_OnSuccess_LogsInformationWithStructuredProperties()
    {
        var repo = new FakeLocationRepository();
        var logger = new TestLogger<CreateLocationHandler>();
        var handler = new CreateLocationHandler(repo, new CreateLocationCommandValidator(), logger);

        var result = await handler.Handle(new CreateLocationCommand("Berlin Hub", "Unter den Linden 1"));

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(result.Value, entry.Properties["LocationId"]);
        Assert.Equal("Berlin Hub", entry.Properties["LocationName"]);
        Assert.Contains("{LocationId}", entry.Properties["{OriginalFormat}"]?.ToString(), StringComparison.Ordinal);
        Assert.Contains("{LocationName}", entry.Properties["{OriginalFormat}"]?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateLocation_OnDuplicateName_LogsWarningWithoutErrorLevel()
    {
        var repo = new FakeLocationRepository();
        repo.Locations.Add(Location.Create(Guid.NewGuid(), "Existing Hub", "Some Street").Value);
        var logger = new TestLogger<CreateLocationHandler>();
        var handler = new CreateLocationHandler(repo, new CreateLocationCommandValidator(), logger);

        var result = await handler.Handle(new CreateLocationCommand("Existing Hub", "Another Street"));

        Assert.True(result.IsFailure);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Existing Hub", entry.Properties["LocationName"]);
    }

    [Fact]
    public async Task CreateLocation_OnDatabaseError_LogsErrorLevel()
    {
        var repo = new FakeLocationRepository { ShouldFailWithDbError = true };
        var logger = new TestLogger<CreateLocationHandler>();
        var handler = new CreateLocationHandler(repo, new CreateLocationCommandValidator(), logger);

        var result = await handler.Handle(new CreateLocationCommand("London Hub", "Piccadilly 5"));

        Assert.True(result.IsFailure);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("London Hub", entry.Properties["LocationName"]);
    }

    [Fact]
    public async Task UpdateLocation_OnSuccess_LogsInformationWithStructuredProperties()
    {
        var repo = new FakeLocationRepository();
        var loc = Location.Create(Guid.NewGuid(), "Old Name", "Old Address").Value;
        repo.Locations.Add(loc);

        var logger = new TestLogger<UpdateLocationHandler>();
        var handler = new UpdateLocationHandler(repo, new UpdateLocationCommandValidator(), logger);

        var result = await handler.Handle(new UpdateLocationCommand(loc.Id, "New Name", "New Address"));

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(loc.Id, entry.Properties["LocationId"]);
        Assert.Equal("New Name", entry.Properties["LocationName"]);
    }

    [Fact]
    public async Task UpdateLocation_WhenNotFound_LogsWarning()
    {
        var repo = new FakeLocationRepository();
        var missingId = Guid.NewGuid();
        var logger = new TestLogger<UpdateLocationHandler>();
        var handler = new UpdateLocationHandler(repo, new UpdateLocationCommandValidator(), logger);

        var result = await handler.Handle(new UpdateLocationCommand(missingId, "New Name", "New Address"));

        Assert.True(result.IsFailure);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(missingId, entry.Properties["LocationId"]);
    }

    [Fact]
    public async Task UpdateLocationName_OnSuccess_LogsInformation()
    {
        var repo = new FakeLocationRepository();
        var loc = Location.Create(Guid.NewGuid(), "Old Name", "Address").Value;
        repo.Locations.Add(loc);

        var logger = new TestLogger<UpdateLocationNameHandler>();
        var handler = new UpdateLocationNameHandler(repo, new UpdateLocationNameCommandValidator(), logger);

        var result = await handler.Handle(new UpdateLocationNameCommand(loc.Id, "Renamed Office"));

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(loc.Id, entry.Properties["LocationId"]);
        Assert.Equal("Renamed Office", entry.Properties["LocationName"]);
    }

    [Fact]
    public async Task DeleteLocation_OnSuccess_LogsInformation()
    {
        var repo = new FakeLocationRepository();
        var loc = Location.Create(Guid.NewGuid(), "Office", "Address").Value;
        repo.Locations.Add(loc);

        var logger = new TestLogger<DeleteLocationHandler>();
        var handler = new DeleteLocationHandler(repo, logger);

        var result = await handler.Handle(new DeleteLocationCommand(loc.Id));

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(loc.Id, entry.Properties["LocationId"]);
    }

    [Fact]
    public async Task CreateDepartment_OnSuccess_LogsInformationWithStructuredProperties()
    {
        var deptRepo = new FakeDepartmentRepository();
        var locRepo = new FakeLocationRepository();
        var logger = new TestLogger<CreateDepartmentHandler>();
        var handler = new CreateDepartmentHandler(deptRepo, locRepo, new CreateDepartmentCommandValidator(), logger);

        var result = await handler.Handle(new CreateDepartmentCommand("Engineering", "engineering", null, []));

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(result.Value.Id, entry.Properties["DepartmentId"]);
        Assert.Equal("Engineering", entry.Properties["DepartmentName"]);
        Assert.Equal("engineering", entry.Properties["DepartmentSlug"]);
    }

    [Fact]
    public async Task CreateDepartment_OnDuplicateName_LogsWarning()
    {
        var deptRepo = new FakeDepartmentRepository();
        deptRepo.Departments.Add(Department.Create(Guid.NewGuid(), "HR", "hr", null).Value);
        var locRepo = new FakeLocationRepository();
        var logger = new TestLogger<CreateDepartmentHandler>();
        var handler = new CreateDepartmentHandler(deptRepo, locRepo, new CreateDepartmentCommandValidator(), logger);

        var result = await handler.Handle(new CreateDepartmentCommand("HR", "hr", null, []));

        Assert.True(result.IsFailure);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("HR", entry.Properties["DepartmentName"]);
    }

    [Fact]
    public async Task CreateDepartment_OnDatabaseError_LogsErrorLevel()
    {
        var deptRepo = new FakeDepartmentRepository { ShouldFailWithDbError = true };
        var locRepo = new FakeLocationRepository();
        var logger = new TestLogger<CreateDepartmentHandler>();
        var handler = new CreateDepartmentHandler(deptRepo, locRepo, new CreateDepartmentCommandValidator(), logger);

        var result = await handler.Handle(new CreateDepartmentCommand("Finance", "finance", null, []));

        Assert.True(result.IsFailure);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("Finance", entry.Properties["DepartmentName"]);
    }

    [Fact]
    public async Task LinkDepartmentLocation_OnSuccess_LogsInformation()
    {
        var deptRepo = new FakeDepartmentRepository();
        var locRepo = new FakeLocationRepository();
        var dept = Department.Create(Guid.NewGuid(), "Sales", "sales", null).Value;
        var loc = Location.Create(Guid.NewGuid(), "Office", "Address").Value;
        deptRepo.Departments.Add(dept);
        locRepo.Locations.Add(loc);

        var logger = new TestLogger<LinkDepartmentLocationHandler>();
        var handler = new LinkDepartmentLocationHandler(deptRepo, locRepo, logger);

        var result = await handler.Handle(new LinkDepartmentLocationCommand(dept.Id, loc.Id));

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(dept.Id, entry.Properties["DepartmentId"]);
        Assert.Equal(loc.Id, entry.Properties["LocationId"]);
    }

    [Fact]
    public async Task UnlinkDepartmentLocation_OnSuccess_LogsInformation()
    {
        var deptRepo = new FakeDepartmentRepository();
        var locRepo = new FakeLocationRepository();
        var dept = Department.Create(Guid.NewGuid(), "Sales", "sales", null).Value;
        var loc = Location.Create(Guid.NewGuid(), "Office", "Address").Value;
        deptRepo.Departments.Add(dept);
        locRepo.Locations.Add(loc);
        deptRepo.Links.Add((dept.Id, loc.Id));

        var logger = new TestLogger<UnlinkDepartmentLocationHandler>();
        var handler = new UnlinkDepartmentLocationHandler(deptRepo, locRepo, logger);

        var result = await handler.Handle(new UnlinkDepartmentLocationCommand(dept.Id, loc.Id));

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(dept.Id, entry.Properties["DepartmentId"]);
        Assert.Equal(loc.Id, entry.Properties["LocationId"]);
    }

    [Fact]
    public async Task LocationsController_CreateLocationDto_EnrichesDiagnosticContextWithLocationId()
    {
        var repo = new FakeLocationRepository();
        var createLocationHandler = new CreateLocationHandler(repo, new CreateLocationCommandValidator());
        var getLocationsHandler = new GetLocationsHandler(repo);
        var getLocationByIdHandler = new GetLocationByIdHandler(repo);
        var updateLocationHandler = new UpdateLocationHandler(repo, new UpdateLocationCommandValidator());
        var deleteLocationHandler = new DeleteLocationHandler(repo);
        var updateLocationNameHandler = new UpdateLocationNameHandler(repo, new UpdateLocationNameCommandValidator());

        var diagnosticContext = new TestDiagnosticContext();
        var controller = new LocationsController(
            createLocationHandler,
            getLocationsHandler,
            getLocationByIdHandler,
            updateLocationHandler,
            deleteLocationHandler,
            updateLocationNameHandler,
            logger: null,
            diagnosticContext: diagnosticContext);

        var result = await controller.CreateLocationDto(new CreateLocationDto("Warsaw Hub", "Nowy Swiat 10"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(diagnosticContext.Properties.ContainsKey("LocationId"));
    }

    [Fact]
    public async Task DepartmentsController_CreateDepartment_EnrichesDiagnosticContextWithDepartmentId()
    {
        var deptRepo = new FakeDepartmentRepository();
        var locRepo = new FakeLocationRepository();
        var createDepartmentHandler = new CreateDepartmentHandler(deptRepo, locRepo, new CreateDepartmentCommandValidator());
        var getDepartmentsHandler = new GetDepartmentsHandler(deptRepo);
        var getDepartmentByIdHandler = new GetDepartmentByIdHandler(deptRepo);
        var updateDepartmentHandler = new UpdateDepartmentHandler(deptRepo, new UpdateDepartmentCommandValidator());
        var deleteDepartmentHandler = new DeleteDepartmentHandler(deptRepo);
        var updateDepartmentNameHandler = new UpdateDepartmentNameHandler(deptRepo, new UpdateDepartmentNameCommandValidator());

        var diagnosticContext = new TestDiagnosticContext();
        var controller = new DepartmentsController(
            createDepartmentHandler,
            getDepartmentsHandler,
            getDepartmentByIdHandler,
            updateDepartmentHandler,
            deleteDepartmentHandler,
            updateDepartmentNameHandler,
            logger: null,
            diagnosticContext: diagnosticContext);

        var result = await controller.CreateDepartment(new CreateDepartmentDto("Marketing", "marketing", null, []), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(diagnosticContext.Properties.ContainsKey("DepartmentId"));
    }

    [Fact]
    public async Task GlobalExceptionHandler_LogsErrorWithExceptionDetails_AndReturnsSafe500Envelope()
    {
        var logger = new TestLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(logger);

        var context = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() },
            Request = { Method = "GET", Path = "/locations" }
        };

        var testException = new InvalidOperationException("Database connection timeout");
        var handled = await handler.TryHandleAsync(context, testException, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

        var logEntry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, logEntry.Level);
        Assert.Same(testException, logEntry.Exception);
        Assert.Equal("GET", logEntry.Properties["RequestMethod"]);
        Assert.Equal("/locations", logEntry.Properties["RequestPath"]?.ToString());

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        Assert.DoesNotContain("Database connection timeout", responseBody, StringComparison.Ordinal);
        Assert.Contains("error", responseBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppsettingsConfiguration_ContainsSerilogSectionWithConsoleAndSeqSinks()
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddJsonFile("appsettings.json", optional: true);
        var config = configBuilder.Build();

        var serilogSection = config.GetSection("Serilog");
        Assert.True(serilogSection.Exists());

        var defaultLevel = serilogSection["MinimumLevel:Default"];
        Assert.Equal("Information", defaultLevel);

        var msOverride = serilogSection["MinimumLevel:Override:Microsoft"];
        Assert.Equal("Warning", msOverride);

        var lifetimeOverride = serilogSection["MinimumLevel:Override:Microsoft.Hosting.Lifetime"];
        Assert.Equal("Information", lifetimeOverride);

        var sinks = serilogSection.GetSection("WriteTo").GetChildren().Select(c => c["Name"]).ToList();
        Assert.Contains("Console", sinks);
        Assert.Contains("Seq", sinks);
    }
}
