using CSharpFunctionalExtensions;
using DirectoryService.Application.Common;
using DirectoryService.Domain.Common;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// ReSharper disable once CheckNamespace
namespace DirectoryService.Application.Departments;

public sealed class UpdateDepartmentHandler : ICommandHandler<UpdateDepartmentCommand>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IValidator<UpdateDepartmentCommand> _validator;
    private readonly ILogger<UpdateDepartmentHandler> _logger;

    public UpdateDepartmentHandler(
        IDepartmentRepository departmentRepository,
        IValidator<UpdateDepartmentCommand> validator,
        ILogger<UpdateDepartmentHandler>? logger = null)
    {
        _departmentRepository = departmentRepository;
        _validator = validator;
        _logger = logger ?? NullLogger<UpdateDepartmentHandler>.Instance;
    }

    public async Task<UnitResult<ErrorList>> Handle(UpdateDepartmentCommand command, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToErrorList();
            _logger.LogWarning("Validation failed while updating department {DepartmentId}: {@Errors}", command.Id, errors);
            return errors;
        }

        var nameExistsResult = await _departmentRepository.NameExistsAsync(command.Name, command.Id, cancellationToken);
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

        var departmentResult = await _departmentRepository.GetByIdAsync(command.Id, cancellationToken);
        if (departmentResult.IsFailure)
        {
            if (departmentResult.Error.Type == ErrorType.NotFound)
            {
                _logger.LogWarning("Department with ID {DepartmentId} was not found for update", command.Id);
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
            _logger.LogWarning("Domain validation failed while updating department {DepartmentId}: {@Errors}", command.Id, changeNameResult.Error);
            return changeNameResult.Error.ToErrorList();
        }

        var updateRepoResult = await _departmentRepository.UpdateAsync(department, cancellationToken);
        if (updateRepoResult.IsFailure)
        {
            _logger.LogError("Database error while updating department {DepartmentId}: {ErrorMessage}", command.Id, updateRepoResult.Error.Message);
            return updateRepoResult.Error.ToErrorList();
        }

        _logger.LogInformation("Department with ID {DepartmentId} updated successfully to Name {DepartmentName}", department.Id, department.Name);
        return UnitResult.Success<ErrorList>();
    }
}
