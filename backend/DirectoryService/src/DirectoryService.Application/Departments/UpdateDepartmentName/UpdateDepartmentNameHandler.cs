using CSharpFunctionalExtensions;
using DirectoryService.Application.Common;
using DirectoryService.Contracts;
using DirectoryService.Domain.Common;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// ReSharper disable once CheckNamespace
namespace DirectoryService.Application.Departments;

public sealed class UpdateDepartmentNameHandler : ICommandHandler<UpdateDepartmentNameCommand, Guid>
{
    private readonly IDepartmentRepository _repository;
    private readonly IValidator<UpdateDepartmentNameCommand> _validator;
    private readonly ILogger<UpdateDepartmentNameHandler> _logger;

    public UpdateDepartmentNameHandler(
        IDepartmentRepository departmentRepository,
        IValidator<UpdateDepartmentNameCommand> validator,
        ILogger<UpdateDepartmentNameHandler>? logger = null)
    {
        _repository = departmentRepository;
        _validator = validator;
        _logger = logger ?? NullLogger<UpdateDepartmentNameHandler>.Instance;
    }

    public async Task<Result<Guid, ErrorList>> Handle(UpdateDepartmentNameCommand command, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToErrorList();
            _logger.LogWarning("Validation failed while updating name for department {DepartmentId}: {@Errors}", command.Id, errors);
            return errors;
        }

        var updateResult = await _repository.UpdateDepartmentNameAsync(command.Id, command.Name, cancellationToken);
        if (updateResult.IsFailure)
        {
            if (updateResult.Error.Type == ErrorType.NotFound)
            {
                _logger.LogWarning("Department with ID {DepartmentId} was not found for renaming", command.Id);
            }
            else
            {
                _logger.LogError("Database error while updating name for department {DepartmentId}: {ErrorMessage}", command.Id, updateResult.Error.Message);
            }

            return updateResult.Error.ToErrorList();
        }

        _logger.LogInformation("Department with ID {DepartmentId} renamed successfully to {DepartmentName}", command.Id, command.Name);
        return Result.Success<Guid, ErrorList>(command.Id);
    }

    // ReSharper disable once UnusedMember.Global
    public Task<Result<Guid, ErrorList>> Handle(UpdateDepartmentNameRequest request, CancellationToken cancellationToken = default) =>
        Handle(new UpdateDepartmentNameCommand(request.Id, request.Name), cancellationToken);
}
