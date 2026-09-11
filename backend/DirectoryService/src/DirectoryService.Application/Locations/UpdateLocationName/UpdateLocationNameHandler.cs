using CSharpFunctionalExtensions;
using DirectoryService.Application.Common;
using DirectoryService.Domain.Common;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// ReSharper disable once CheckNamespace
namespace DirectoryService.Application.Locations;

public sealed class UpdateLocationNameHandler : ICommandHandler<UpdateLocationNameCommand, Guid>
{
    private readonly ILocationRepository _repository;
    private readonly IValidator<UpdateLocationNameCommand> _validator;
    private readonly ILogger<UpdateLocationNameHandler> _logger;

    public UpdateLocationNameHandler(
        ILocationRepository locationRepository,
        IValidator<UpdateLocationNameCommand> validator,
        ILogger<UpdateLocationNameHandler>? logger = null)
    {
        _repository = locationRepository;
        _validator = validator;
        _logger = logger ?? NullLogger<UpdateLocationNameHandler>.Instance;
    }

    public async Task<Result<Guid, ErrorList>> Handle(UpdateLocationNameCommand command, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToErrorList();
            _logger.LogWarning("Validation failed while updating name for location {LocationId}: {@Errors}", command.Id, errors);
            return errors;
        }

        var nameExistsResult = await _repository.NameExistsAsync(command.Name, command.Id, cancellationToken);
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

        var locationResult = await _repository.GetByIdAsync(command.Id, cancellationToken);
        if (locationResult.IsFailure)
        {
            if (locationResult.Error.Type == ErrorType.NotFound)
            {
                _logger.LogWarning("Location with ID {LocationId} was not found for renaming", command.Id);
            }
            else
            {
                _logger.LogError("Database error while retrieving location {LocationId}: {ErrorMessage}", command.Id, locationResult.Error.Message);
            }

            return locationResult.Error.ToErrorList();
        }

        var location = locationResult.Value;
        var changeNameResult = location.ChangeName(command.Name);
        if (changeNameResult.IsFailure)
        {
            _logger.LogWarning("Domain validation failed while renaming location {LocationId}: {@Errors}", command.Id, changeNameResult.Error);
            return changeNameResult.Error.ToErrorList();
        }

        var updateResult = await _repository.UpdateAsync(location, cancellationToken);
        if (updateResult.IsFailure)
        {
            _logger.LogError("Database error while updating name for location {LocationId}: {ErrorMessage}", command.Id, updateResult.Error.Message);
            return updateResult.Error.ToErrorList();
        }

        _logger.LogInformation("Location with ID {LocationId} renamed successfully to {LocationName}", command.Id, command.Name);
        return Result.Success<Guid, ErrorList>(command.Id);
    }
}
