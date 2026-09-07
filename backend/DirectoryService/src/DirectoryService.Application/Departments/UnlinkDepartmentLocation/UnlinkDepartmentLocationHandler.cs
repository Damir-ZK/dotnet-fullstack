using CSharpFunctionalExtensions;
using DirectoryService.Application.Common;
using DirectoryService.Application.Locations;
using DirectoryService.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// ReSharper disable once CheckNamespace
namespace DirectoryService.Application.Departments;

public sealed class UnlinkDepartmentLocationHandler : ICommandHandler<UnlinkDepartmentLocationCommand>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly ILogger<UnlinkDepartmentLocationHandler> _logger;

    public UnlinkDepartmentLocationHandler(
        IDepartmentRepository departmentRepository,
        ILocationRepository locationRepository,
        ILogger<UnlinkDepartmentLocationHandler>? logger = null)
    {
        _departmentRepository = departmentRepository;
        _locationRepository = locationRepository;
        _logger = logger ?? NullLogger<UnlinkDepartmentLocationHandler>.Instance;
    }

    public async Task<UnitResult<ErrorList>> Handle(
        UnlinkDepartmentLocationCommand command,
        CancellationToken cancellationToken = default)
    {
        var departmentResult = await _departmentRepository.GetByIdAsync(command.DepartmentId, cancellationToken);
        if (departmentResult.IsFailure)
        {
            if (departmentResult.Error.Type == ErrorType.NotFound)
            {
                _logger.LogWarning("Department with ID {DepartmentId} was not found while unlinking from location {LocationId}", command.DepartmentId, command.LocationId);
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
                _logger.LogWarning("Location with ID {LocationId} was not found while unlinking from department {DepartmentId}", command.LocationId, command.DepartmentId);
            }
            else
            {
                _logger.LogError("Database error while retrieving location {LocationId}: {ErrorMessage}", command.LocationId, locationResult.Error.Message);
            }

            return locationResult.Error.ToErrorList();
        }

        var removeLinkResult =
            await _departmentRepository.RemoveLocationLinkAsync(command.DepartmentId, command.LocationId, cancellationToken);
        if (removeLinkResult.IsFailure)
        {
            if (removeLinkResult.Error.Type == ErrorType.NotFound)
            {
                _logger.LogWarning("Department {DepartmentId} is not linked to location {LocationId}", command.DepartmentId, command.LocationId);
            }
            else
            {
                _logger.LogError("Database error while unlinking department {DepartmentId} and location {LocationId}: {ErrorMessage}", command.DepartmentId, command.LocationId, removeLinkResult.Error.Message);
            }

            return removeLinkResult.Error.ToErrorList();
        }

        _logger.LogInformation("Successfully unlinked department {DepartmentId} from location {LocationId}", command.DepartmentId, command.LocationId);
        return UnitResult.Success<ErrorList>();
    }
}
