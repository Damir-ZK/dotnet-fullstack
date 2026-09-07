using DirectoryService.Contracts;
using FluentValidation;

// ReSharper disable once CheckNamespace
namespace DirectoryService.Application.Locations;

public sealed class UpdateLocationCommandValidator : AbstractValidator<UpdateLocationCommand>
{
    public UpdateLocationCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Location id is required.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Location name is required.")
            .MaximumLength(AppConstants.MaxNameLength)
            .WithMessage($"Location name cannot exceed {AppConstants.MaxNameLength} characters.");

        RuleFor(x => x.Address)
            .NotEmpty()
            .WithMessage("Location address is required.")
            .MaximumLength(AppConstants.MaxAddressLength)
            .WithMessage($"Location address cannot exceed {AppConstants.MaxAddressLength} characters.");
    }
}
