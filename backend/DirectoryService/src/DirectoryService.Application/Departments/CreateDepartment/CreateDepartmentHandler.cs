using CSharpFunctionalExtensions;
using DirectoryService.Application.Common;
using DirectoryService.Application.Locations;
using DirectoryService.Contracts;
using DirectoryService.Domain.Common;
using DirectoryService.Domain.Departments;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// ReSharper disable once CheckNamespace
namespace DirectoryService.Application.Departments;

public sealed class CreateDepartmentHandler : ICommandHandler<CreateDepartmentCommand, DepartmentDto>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IValidator<CreateDepartmentCommand> _validator;
    private readonly ILogger<CreateDepartmentHandler> _logger;

    public CreateDepartmentHandler(
        IDepartmentRepository departmentRepository,
        ILocationRepository locationRepository,
        IValidator<CreateDepartmentCommand> validator,
        ILogger<CreateDepartmentHandler>? logger = null)
    {
        _departmentRepository = departmentRepository;
        _locationRepository = locationRepository;
        _validator = validator;
        _logger = logger ?? NullLogger<CreateDepartmentHandler>.Instance;
    }

    public async Task<Result<DepartmentDto, ErrorList>> Handle(CreateDepartmentCommand command, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToErrorList();
            _logger.LogWarning("Validation failed while creating department {DepartmentName}: {@Errors}", command.Name, errors);
            return errors;
        }

        var nameExistsResult = await _departmentRepository.NameExistsAsync(command.Name, cancellationToken);
        if (nameExistsResult.IsFailure)
        {
            _logger.LogError("Database error while checking if department name exists {DepartmentName}: {ErrorMessage}", command.Name, nameExistsResult.Error.Message);
            return nameExistsResult.Error.ToErrorList();
        }

        if (nameExistsResult.Value)
        {
            _logger.LogWarning("Department with Name {DepartmentName} already exists", command.Name);
            return Errors.Department.AlreadyExists(command.Name).ToErrorList();
        }

        Department? parentDepartment = null;
        if (command.ParentId.HasValue)
        {
            var parentResult = await _departmentRepository.GetByIdAsync(command.ParentId.Value, cancellationToken);
            if (parentResult.IsFailure)
            {
                _logger.LogWarning("Parent department with ID {ParentDepartmentId} was not found while creating department {DepartmentName}", command.ParentId.Value, command.Name);
                return Errors.Department.ParentNotFound(command.ParentId.Value).ToErrorList();
            }

            parentDepartment = parentResult.Value;
        }

        var locationIds = command.LocationIds?.Distinct().ToList() ?? [];
        foreach (var locationId in locationIds)
        {
            var locResult = await _locationRepository.GetByIdAsync(locationId, cancellationToken);
            if (locResult.IsFailure)
            {
                _logger.LogWarning("Location with ID {LocationId} was not found while creating department {DepartmentName}", locationId, command.Name);
                return Errors.Location.NotFound(locationId).ToErrorList();
            }
        }

        var deptResult = Department.Create(Guid.NewGuid(), command.Name, command.Slug, parentDepartment);
        if (deptResult.IsFailure)
        {
            _logger.LogWarning("Domain validation failed while creating department {DepartmentName}: {@Errors}", command.Name, deptResult.Error);
            return deptResult.Error.ToErrorList();
        }

        var department = deptResult.Value;
        var addResult = await _departmentRepository.AddAsync(department, locationIds, cancellationToken);
        if (addResult.IsFailure)
        {
            _logger.LogError("Database error while adding department {DepartmentName}: {ErrorMessage}", command.Name, addResult.Error.Message);
            return addResult.Error.ToErrorList();
        }

        var dto = new DepartmentDto(
            department.Id,
            department.Name,
            department.Slug,
            department.Path,
            department.ParentId,
            locationIds);

        _logger.LogInformation("Department created successfully with ID {DepartmentId}, Name {DepartmentName}, and Slug {DepartmentSlug}", department.Id, department.Name, department.Slug);
        return Result.Success<DepartmentDto, ErrorList>(dto);
    }

    public async Task<Result<Guid, ErrorList>> ExecuteAsync(CreateDepartmentDto dto, CancellationToken cancellationToken = default)
    {
        var command = new CreateDepartmentCommand(dto.Name, dto.Slug, dto.ParentId, dto.LocationIds);
        var result = await Handle(command, cancellationToken);
        return result.Map(dept => dept.Id);
    }
}

public sealed class CreateDepartment
{
    private readonly CreateDepartmentHandler _handler;

    public CreateDepartment(
        IDepartmentRepository departmentRepository,
        ILocationRepository locationRepository,
        IValidator<CreateDepartmentDto> validator)
    {
        _ = validator;
        _handler = new CreateDepartmentHandler(departmentRepository, locationRepository, new CreateDepartmentCommandValidator());
    }

    public Task<Result<Guid, ErrorList>> ExecuteAsync(CreateDepartmentDto dto, CancellationToken cancellationToken = default) =>
        _handler.ExecuteAsync(dto, cancellationToken);
}
