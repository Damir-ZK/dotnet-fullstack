using DirectoryService.Contracts;
using FluentValidation;

// ReSharper disable once CheckNamespace
namespace DirectoryService.Application.Departments;

public sealed class UpdateDepartmentNameCommandValidator : AbstractValidator<UpdateDepartmentNameCommand>
{
    public UpdateDepartmentNameCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Department id is required.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Department name is required.")
            .MaximumLength(AppConstants.MaxNameLength)
            .WithMessage($"Department name cannot exceed {AppConstants.MaxNameLength} characters.");
    }
}
