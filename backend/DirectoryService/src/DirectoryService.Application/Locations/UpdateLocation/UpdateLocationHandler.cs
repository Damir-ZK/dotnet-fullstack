using CSharpFunctionalExtensions;
using DirectoryService.Application.Common;
using DirectoryService.Domain.Common;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// ReSharper disable once CheckNamespace
namespace DirectoryService.Application.Locations;

public sealed class UpdateLocationHandler : ICommandHandler<UpdateLocationCommand>
{
    private readonly ILocationRepository _locationRepository;
    private readonly IValidator<UpdateLocationCommand> _validator;
    private readonly ILogger<UpdateLocationHandler> _logger;

    public UpdateLocationHandler(
        ILocationRepository locationRepository,
        IValidator<UpdateLocationCommand> validator,
        ILogger<UpdateLocationHandler>? logger = null)
    {
        _locationRepository = locationRepository;
        _validator = validator;
        _logger = logger ?? NullLogger<UpdateLocationHandler>.Instance;
    }

    public async Task<UnitResult<ErrorList>> Handle(UpdateLocationCommand command, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToErrorList();
            _logger.LogWarning("Validation failed while updating location {LocationId}: {@Errors}", command.Id, errors);
            return errors;
        }

        var nameExistsResult = await _locationRepository.NameExistsAsync(command.Name, command.Id, cancellationToken);
        if (nameExistsResult.IsFailure)
        {
            _logger.LogError("Database error while checking if location name exists {LocationName}: {ErrorMessage}", command.Name, nameExistsResult.Error.Message);
            return nameExistsResult.Error.ToErrorList();
        }

        if (nameExistsResult.Value)
        {
            _logger.LogWarning("Location with Name {LocationName} already exists", command.Name);
            return Errors.Location.AlreadyExists(command.Name).ToErrorList();
        }

        var locationResult = await _locationRepository.GetByIdAsync(command.Id, cancellationToken);
        if (locationResult.IsFailure)
        {
            if (locationResult.Error.Type == ErrorType.NotFound)
            {
                _logger.LogWarning("Location with ID {LocationId} was not found for update", command.Id);
            }
            else
            {
                _logger.LogError("Database error while retrieving location {LocationId}: {ErrorMessage}", command.Id, locationResult.Error.Message);
            }

            return locationResult.Error.ToErrorList();
        }

        var location = locationResult.Value;
        var updateDetailsResult = location.UpdateDetails(command.Name, command.Address);
        if (updateDetailsResult.IsFailure)
        {
            _logger.LogWarning("Domain validation failed while updating location {LocationId}: {@Errors}", command.Id, updateDetailsResult.Error);
            return updateDetailsResult.Error.ToErrorList();
        }

        var updateRepoResult = await _locationRepository.UpdateAsync(location, cancellationToken);
        if (updateRepoResult.IsFailure)
        {
            _logger.LogError("Database error while updating location {LocationId}: {ErrorMessage}", command.Id, updateRepoResult.Error.Message);
            return updateRepoResult.Error.ToErrorList();
        }

        _logger.LogInformation("Location with ID {LocationId} updated successfully to Name {LocationName}", location.Id, location.Name);
        return UnitResult.Success<ErrorList>();
    }
}
