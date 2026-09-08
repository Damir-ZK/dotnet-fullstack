using CSharpFunctionalExtensions;
using DirectoryService.Application.Common;
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

        var nameExistsResult = await _repository.NameExistsAsync(command.Name, command.Id, cancellationToken);
        if (nameExistsResult.IsFailure)
        {
            _logger.LogError("Database error while checking if department name exists {DepartmentName}: {ErrorMessage}", command.Name, nameExistsResult.Error.Message);
            return nameExistsResult.Error.ToErrorList();
        }

        if (nameExistsResult.Value)
        {
            _logger.LogWarning("Department with Name {DepartmentName} already exists", command.Name);
            return Errors.Department.AlreadyExists(command.Name).ToErrorList();
        }

        var departmentResult = await _repository.GetByIdAsync(command.Id, cancellationToken);
        if (departmentResult.IsFailure)
        {
            if (departmentResult.Error.Type == ErrorType.NotFound)
            {
                _logger.LogWarning("Department with ID {DepartmentId} was not found for renaming", command.Id);
            }
            else
            {
                _logger.LogError("Database error while retrieving department {DepartmentId}: {ErrorMessage}", command.Id, departmentResult.Error.Message);
            }

            return departmentResult.Error.ToErrorList();
        }

        var department = departmentResult.Value;
        var changeNameResult = department.ChangeName(command.Name);
        if (changeNameResult.IsFailure)
        {
            _logger.LogWarning("Domain validation failed while renaming department {DepartmentId}: {@Errors}", command.Id, changeNameResult.Error);
            return changeNameResult.Error.ToErrorList();
        }

        var updateResult = await _repository.UpdateAsync(department, cancellationToken);
        if (updateResult.IsFailure)
        {
            _logger.LogError("Database error while updating name for department {DepartmentId}: {ErrorMessage}", command.Id, updateResult.Error.Message);
            return updateResult.Error.ToErrorList();
        }

        _logger.LogInformation("Department with ID {DepartmentId} renamed successfully to {DepartmentName}", command.Id, command.Name);
        return Result.Success<Guid, ErrorList>(command.Id);
    }
}
