using DirectoryService.Contracts;
using FluentValidation;

// ReSharper disable once CheckNamespace
namespace DirectoryService.Application.Locations;

public sealed class UpdateLocationNameCommandValidator : AbstractValidator<UpdateLocationNameCommand>
{
    public UpdateLocationNameCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Location id is required.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Location name is required.")
            .MaximumLength(AppConstants.MaxNameLength)
            .WithMessage($"Location name cannot exceed {AppConstants.MaxNameLength} characters.");
    }
}
