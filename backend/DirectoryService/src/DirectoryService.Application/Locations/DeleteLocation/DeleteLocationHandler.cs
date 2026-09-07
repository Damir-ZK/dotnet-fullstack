using CSharpFunctionalExtensions;
using DirectoryService.Application.Common;
using DirectoryService.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// ReSharper disable once CheckNamespace
namespace DirectoryService.Application.Locations;

public sealed class DeleteLocationHandler : ICommandHandler<DeleteLocationCommand>
{
    private readonly ILocationRepository _locationRepository;
    private readonly ILogger<DeleteLocationHandler> _logger;

    public DeleteLocationHandler(
        ILocationRepository locationRepository,
        ILogger<DeleteLocationHandler>? logger = null)
    {
        _locationRepository = locationRepository;
        _logger = logger ?? NullLogger<DeleteLocationHandler>.Instance;
    }

    public async Task<UnitResult<ErrorList>> Handle(DeleteLocationCommand command,
        CancellationToken cancellationToken = default)
    {
        var deleteResult = await _locationRepository.DeleteAsync(command.Id, cancellationToken);
        if (deleteResult.IsFailure)
        {
            if (deleteResult.Error.Type == ErrorType.NotFound)
            {
                _logger.LogWarning("Location with ID {LocationId} was not found for deletion", command.Id);
            }
            else
            {
                _logger.LogError("Database error while deleting location {LocationId}: {ErrorMessage}", command.Id,
                    deleteResult.Error.Message);
            }

            return deleteResult.Error.ToErrorList();
        }

        _logger.LogInformation("Location with ID {LocationId} deleted successfully", command.Id);
        return UnitResult.Success<ErrorList>();
    }
}
