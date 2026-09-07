using CSharpFunctionalExtensions;
using DirectoryService.Application.Common;
using DirectoryService.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// ReSharper disable once CheckNamespace
namespace DirectoryService.Application.Departments;

public sealed class DeleteDepartmentHandler : ICommandHandler<DeleteDepartmentCommand>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILogger<DeleteDepartmentHandler> _logger;

    public DeleteDepartmentHandler(
        IDepartmentRepository departmentRepository,
        ILogger<DeleteDepartmentHandler>? logger = null)
    {
        _departmentRepository = departmentRepository;
        _logger = logger ?? NullLogger<DeleteDepartmentHandler>.Instance;
    }

    public async Task<UnitResult<ErrorList>> Handle(DeleteDepartmentCommand command, CancellationToken cancellationToken = default)
    {
        var deleteResult = await _departmentRepository.DeleteAsync(command.Id, cancellationToken);
        if (deleteResult.IsFailure)
        {
            if (deleteResult.Error.Type == ErrorType.NotFound)
            {
                _logger.LogWarning("Department with ID {DepartmentId} was not found for deletion", command.Id);
            }
            else
            {
                _logger.LogError("Database error while deleting department {DepartmentId}: {ErrorMessage}", command.Id, deleteResult.Error.Message);
            }

            return deleteResult.Error.ToErrorList();
        }

        _logger.LogInformation("Department with ID {DepartmentId} deleted successfully", command.Id);
        return UnitResult.Success<ErrorList>();
    }
}
