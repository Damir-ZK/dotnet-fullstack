using DirectoryService.Application.Common;
using DirectoryService.Application.Departments;
using DirectoryService.Presentation.Common;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryService.Presentation;

[ApiController]
[Route("departments/{departmentId:guid}/locations")]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status500InternalServerError)]
#pragma warning disable S6960 // Controllers should not have multiple responsibilities
public sealed class DepartmentLocationsController : ControllerBase
{
    private readonly ICommandHandler<LinkDepartmentLocationCommand> _linkHandler;
    private readonly ICommandHandler<UnlinkDepartmentLocationCommand> _unlinkHandler;

    public DepartmentLocationsController(
        ICommandHandler<LinkDepartmentLocationCommand> linkHandler,
        ICommandHandler<UnlinkDepartmentLocationCommand> unlinkHandler)
    {
        _linkHandler = linkHandler;
        _unlinkHandler = unlinkHandler;
    }

    [HttpPost("{locationId:guid}")]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
    public async Task<IResult> LinkLocation(
        [FromRoute] Guid departmentId,
        [FromRoute] Guid locationId,
        CancellationToken cancellationToken)
    {
        var command = new LinkDepartmentLocationCommand(departmentId, locationId);
        var result = await _linkHandler.Handle(command, cancellationToken);
        return result.ToEnvelopeResult();
    }

    [HttpDelete("{locationId:guid}")]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IResult> UnlinkLocation(
        [FromRoute] Guid departmentId,
        [FromRoute] Guid locationId,
        CancellationToken cancellationToken)
    {
        var command = new UnlinkDepartmentLocationCommand(departmentId, locationId);
        var result = await _unlinkHandler.Handle(command, cancellationToken);
        return result.ToEnvelopeResult();
    }
}
