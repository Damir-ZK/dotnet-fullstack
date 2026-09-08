using CSharpFunctionalExtensions;
using DirectoryService.Application.Common;
using DirectoryService.Domain;
using DirectoryService.Domain.Common;
using FluentValidation;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// ReSharper disable once CheckNamespace
namespace DirectoryService.Application.Locations;

public sealed class CreateLocationHandler : ICommandHandler<CreateLocationCommand, Guid>
{
    private readonly ILocationRepository _locationRepository;
    private readonly IValidator<CreateLocationCommand> _validator;
    private readonly ILogger<CreateLocationHandler> _logger;

    public CreateLocationHandler(
        ILocationRepository locationRepository,
        IValidator<CreateLocationCommand> validator,
        ILogger<CreateLocationHandler>? logger = null)
    {
        _locationRepository = locationRepository;
        _validator = validator;
        _logger = logger ?? NullLogger<CreateLocationHandler>.Instance;
    }

    public async Task<Result<Guid, ErrorList>> Handle(CreateLocationCommand command, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToErrorList();
            _logger.LogWarning("Validation failed while creating location {LocationName}: {@Errors}", command.Name, errors);
            return errors;
        }

        var nameExistsResult = await _locationRepository.NameExistsAsync(command.Name, cancellationToken);
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

        var locationResult = Location.Create(Guid.NewGuid(), command.Name, command.Address);
        if (locationResult.IsFailure)
        {
            _logger.LogWarning("Domain validation failed while creating location {LocationName}: {@Errors}", command.Name, locationResult.Error);
            return locationResult.Error.ToErrorList();
        }

        var addResult = await _locationRepository.AddAsync(locationResult.Value, cancellationToken);
        if (addResult.IsFailure)
        {
            _logger.LogError("Database error while adding location {LocationName}: {ErrorMessage}", command.Name, addResult.Error.Message);
            return addResult.Error.ToErrorList();
        }

        _logger.LogInformation("Location created successfully with ID {LocationId} and Name {LocationName}", locationResult.Value.Id, locationResult.Value.Name);
        return Result.Success<Guid, ErrorList>(locationResult.Value.Id);
    }
}
