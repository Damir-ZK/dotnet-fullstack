using DirectoryService.Application.Common;
using DirectoryService.Application.Departments;
using DirectoryService.Contracts;
using DirectoryService.Domain.Common;
using DirectoryService.Presentation.Common;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryService.Presentation;

[ApiController]
[Route("departments")]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status500InternalServerError)]
#pragma warning disable S107 // Methods should not have too many parameters
#pragma warning disable S6960 // Controllers should not have multiple responsibilities
public sealed class DepartmentsController : ControllerBase
{
    private readonly ICommandHandler<CreateDepartmentCommand, DepartmentDto> _createDepartmentHandler;
    private readonly IQueryHandler<GetDepartmentsQuery, IReadOnlyList<DepartmentDto>> _getDepartmentsHandler;
    private readonly IQueryHandler<GetDepartmentByIdQuery, DepartmentDto> _getDepartmentByIdHandler;
    private readonly ICommandHandler<UpdateDepartmentCommand> _updateDepartmentHandler;
    private readonly ICommandHandler<DeleteDepartmentCommand> _deleteDepartmentHandler;
    private readonly ICommandHandler<UpdateDepartmentNameCommand, Guid> _updateDepartmentNameHandler;
    private readonly ILogger<DepartmentsController> _logger;
    private readonly Serilog.IDiagnosticContext? _diagnosticContext;

    public DepartmentsController(
        ICommandHandler<CreateDepartmentCommand, DepartmentDto> createDepartmentHandler,
        IQueryHandler<GetDepartmentsQuery, IReadOnlyList<DepartmentDto>> getDepartmentsHandler,
        IQueryHandler<GetDepartmentByIdQuery, DepartmentDto> getDepartmentByIdHandler,
        ICommandHandler<UpdateDepartmentCommand> updateDepartmentHandler,
        ICommandHandler<DeleteDepartmentCommand> deleteDepartmentHandler,
        ICommandHandler<UpdateDepartmentNameCommand, Guid> updateDepartmentNameHandler,
        ILogger<DepartmentsController>? logger = null,
        Serilog.IDiagnosticContext? diagnosticContext = null)
    {
        _createDepartmentHandler = createDepartmentHandler;
        _getDepartmentsHandler = getDepartmentsHandler;
        _getDepartmentByIdHandler = getDepartmentByIdHandler;
        _updateDepartmentHandler = updateDepartmentHandler;
        _deleteDepartmentHandler = deleteDepartmentHandler;
        _updateDepartmentNameHandler = updateDepartmentNameHandler;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DepartmentsController>.Instance;
        _diagnosticContext = diagnosticContext;
    }

    [HttpPost]
    [ProducesResponseType(typeof(Envelope<DepartmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
    public async Task<IResult> CreateDepartment(
        [FromBody] CreateDepartmentDto departmentDto,
        CancellationToken cancellationToken)
    {
        var command = new CreateDepartmentCommand(
            departmentDto.Name,
            departmentDto.Slug,
            departmentDto.ParentId,
            departmentDto.LocationIds);

        var result = await _createDepartmentHandler.Handle(command, cancellationToken);
        if (result.IsSuccess)
        {
            _diagnosticContext?.Set("DepartmentId", result.Value.Id);
        }
        return result.ToCreatedEnvelopeResult();
    }

    [HttpGet]
    [ProducesResponseType(typeof(Envelope<IReadOnlyList<DepartmentDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> GetDepartments(
        CancellationToken cancellationToken)
    {
        var query = new GetDepartmentsQuery();
        var result = await _getDepartmentsHandler.Handle(query, cancellationToken);
        return result.ToEnvelopeResult();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Envelope<DepartmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IResult> GetDepartmentById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetDepartmentByIdQuery(id);
        var result = await _getDepartmentByIdHandler.Handle(query, cancellationToken);
        return result.ToEnvelopeResult();
    }

    [HttpPut("{id:guid}")]
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IResult> UpdateDepartment(
        [FromRoute] Guid id,
        [FromBody] UpdateDepartmentDto departmentDto,
        CancellationToken cancellationToken)
    {
        var command = new UpdateDepartmentCommand(id, departmentDto.Name);
        var result = await _updateDepartmentHandler.Handle(command, cancellationToken);
        return result.ToEnvelopeResult();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IResult> DeleteDepartment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteDepartmentCommand(id);
        var result = await _deleteDepartmentHandler.Handle(command, cancellationToken);
        return result.ToEnvelopeResult();
    }

    [HttpPatch("{id:guid}/name")]
    [ProducesResponseType(typeof(Envelope<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IResult> UpdateDepartmentName(
        [FromRoute] Guid id,
        [FromBody] UpdateDepartmentNameRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateDepartmentNameCommand(id, request.Name);
        var result = await _updateDepartmentNameHandler.Handle(command, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to update department name for {DepartmentId}: {@Errors}", id, result.Error);
        }

        return result.ToEnvelopeResult();
    }
}