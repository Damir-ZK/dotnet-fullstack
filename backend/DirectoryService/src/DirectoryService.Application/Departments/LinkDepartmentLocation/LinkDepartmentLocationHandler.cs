using CSharpFunctionalExtensions;
using DirectoryService.Application.Common;
using DirectoryService.Application.Locations;
using DirectoryService.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// ReSharper disable once CheckNamespace
namespace DirectoryService.Application.Departments;

public sealed class LinkDepartmentLocationHandler : ICommandHandler<LinkDepartmentLocationCommand>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly ILogger<LinkDepartmentLocationHandler> _logger;

    public LinkDepartmentLocationHandler(
        IDepartmentRepository departmentRepository,
        ILocationRepository locationRepository,
        ILogger<LinkDepartmentLocationHandler>? logger = null)
    {
        _departmentRepository = departmentRepository;
        _locationRepository = locationRepository;
        _logger = logger ?? NullLogger<LinkDepartmentLocationHandler>.Instance;
    }

    public async Task<UnitResult<ErrorList>> Handle(LinkDepartmentLocationCommand command, CancellationToken cancellationToken = default)
    {
        var departmentResult = await _departmentRepository.GetByIdAsync(command.DepartmentId, cancellationToken);
        if (departmentResult.IsFailure)
        {
            if (departmentResult.Error.Type == ErrorType.NotFound)
            {
                _logger.LogWarning("Department with ID {DepartmentId} was not found while linking to location {LocationId}", command.DepartmentId, command.LocationId);
            }
            else
            {
                _logger.LogError("Database error while retrieving department {DepartmentId}: {ErrorMessage}", command.DepartmentId, departmentResult.Error.Message);
            }

            return departmentResult.Error.ToErrorList();
        }

        var locationResult = await _locationRepository.GetByIdAsync(command.LocationId, cancellationToken);
        if (locationResult.IsFailure)
        {
            if (locationResult.Error.Type == ErrorType.NotFound)
            {
                _logger.LogWarning("Location with ID {LocationId} was not found while linking to department {DepartmentId}", command.LocationId, command.DepartmentId);
            }
            else
            {
                _logger.LogError("Database error while retrieving location {LocationId}: {ErrorMessage}", command.LocationId, locationResult.Error.Message);
            }

            return locationResult.Error.ToErrorList();
        }

        var linkExistsResult = await _departmentRepository.LocationLinkExistsAsync(command.DepartmentId, command.LocationId, cancellationToken);
        if (linkExistsResult.IsFailure)
        {
            _logger.LogError("Database error while checking link between department {DepartmentId} and location {LocationId}: {ErrorMessage}", command.DepartmentId, command.LocationId, linkExistsResult.Error.Message);
            return linkExistsResult.Error.ToErrorList();
        }

        if (linkExistsResult.Value)
        {
            _logger.LogWarning("Department {DepartmentId} is already linked to location {LocationId}", command.DepartmentId, command.LocationId);
            return Errors.Department.LocationAlreadyLinked(command.DepartmentId, command.LocationId).ToErrorList();
        }

        var addLinkResult = await _departmentRepository.AddLocationLinkAsync(command.DepartmentId, command.LocationId, cancellationToken);
        if (addLinkResult.IsFailure)
        {
            _logger.LogError("Database error while linking department {DepartmentId} and location {LocationId}: {ErrorMessage}", command.DepartmentId, command.LocationId, addLinkResult.Error.Message);
            return addLinkResult.Error.ToErrorList();
        }

        _logger.LogInformation("Successfully linked department {DepartmentId} with location {LocationId}", command.DepartmentId, command.LocationId);
        return UnitResult.Success<ErrorList>();
    }
}
