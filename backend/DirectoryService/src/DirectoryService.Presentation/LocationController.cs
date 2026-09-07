using DirectoryService.Application.Common;
using DirectoryService.Application.Locations;
using DirectoryService.Contracts;
using DirectoryService.Domain.Common;
using DirectoryService.Presentation.Common;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryService.Presentation;

[ApiController]
[Route("locations")]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status500InternalServerError)]
#pragma warning disable S107 // Methods should not have too many parameters
#pragma warning disable S6960 // Controllers should not have multiple responsibilities
public sealed class LocationsController : ControllerBase
{
    private readonly ICommandHandler<CreateLocationCommand, Guid> _createLocationHandler;
    private readonly IQueryHandler<GetLocationsQuery, IReadOnlyList<LocationDto>> _getLocationsHandler;
    private readonly IQueryHandler<GetLocationByIdQuery, LocationDto> _getLocationByIdHandler;
    private readonly ICommandHandler<UpdateLocationCommand> _updateLocationHandler;
    private readonly ICommandHandler<DeleteLocationCommand> _deleteLocationHandler;
    private readonly ICommandHandler<UpdateLocationNameCommand, Guid> _updateLocationNameHandler;
    private readonly ILogger<LocationsController> _logger;
    private readonly Serilog.IDiagnosticContext? _diagnosticContext;

    public LocationsController(
        ICommandHandler<CreateLocationCommand, Guid> createLocationHandler,
        IQueryHandler<GetLocationsQuery, IReadOnlyList<LocationDto>> getLocationsHandler,
        IQueryHandler<GetLocationByIdQuery, LocationDto> getLocationByIdHandler,
        ICommandHandler<UpdateLocationCommand> updateLocationHandler,
        ICommandHandler<DeleteLocationCommand> deleteLocationHandler,
        ICommandHandler<UpdateLocationNameCommand, Guid> updateLocationNameHandler,
        ILogger<LocationsController>? logger = null,
        Serilog.IDiagnosticContext? diagnosticContext = null)
    {
        _createLocationHandler = createLocationHandler;
        _getLocationsHandler = getLocationsHandler;
        _getLocationByIdHandler = getLocationByIdHandler;
        _updateLocationHandler = updateLocationHandler;
        _deleteLocationHandler = deleteLocationHandler;
        _updateLocationNameHandler = updateLocationNameHandler;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LocationsController>.Instance;
        _diagnosticContext = diagnosticContext;
    }

    [HttpPost]
    [ProducesResponseType(typeof(Envelope<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
    public async Task<IResult> CreateLocationDto(
        [FromBody] CreateLocationDto locationDto,
        CancellationToken cancellationToken)
    {
        var command = new CreateLocationCommand(locationDto.Name, locationDto.Address);
        var result = await _createLocationHandler.Handle(command, cancellationToken);
        if (result.IsSuccess)
        {
            _diagnosticContext?.Set("LocationId", result.Value);
        }
        return result.ToCreatedEnvelopeResult();
    }

    [HttpGet]
    [ProducesResponseType(typeof(Envelope<IReadOnlyList<LocationDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> GetLocations(
        CancellationToken cancellationToken)
    {
        var query = new GetLocationsQuery();
        var result = await _getLocationsHandler.Handle(query, cancellationToken);
        return result.ToEnvelopeResult();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Envelope<LocationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IResult> GetLocationById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetLocationByIdQuery(id);
        var result = await _getLocationByIdHandler.Handle(query, cancellationToken);
        return result.ToEnvelopeResult();
    }

    [HttpPatch("{id:guid}")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IResult> UpdateLocation(
        [FromRoute] Guid id,
        [FromBody] UpdateLocationDto locationDto,
        CancellationToken cancellationToken)
    {
        var command = new UpdateLocationCommand(id, locationDto.Name, locationDto.Address);
        var result = await _updateLocationHandler.Handle(command, cancellationToken);
        return result.ToEnvelopeResult();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IResult> DeleteLocation(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteLocationCommand(id);
        var result = await _deleteLocationHandler.Handle(command, cancellationToken);
        return result.ToEnvelopeResult();
    }

    [HttpPatch("{id:guid}/name")]
    [ProducesResponseType(typeof(Envelope<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IResult> UpdateLocationName(
        [FromRoute] Guid id,
        [FromBody] UpdateLocationNameRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Id != id)
        {
            _logger.LogWarning("Route ID {RouteLocationId} does not match request ID {RequestLocationId} when updating location name", id, request.Id);
            ErrorList error = Errors.General.ValueIsInvalid(nameof(request.Id), "The route id and request id do not match.");
            return error.ToEnvelopeResult();
        }

        var command = new UpdateLocationNameCommand(request.Id, request.Name);
        var result = await _updateLocationNameHandler.Handle(command, cancellationToken);
        return result.ToEnvelopeResult();
    }
}
