using DirectoryService.Application;
using DirectoryService.Application.Common;
using DirectoryService.Application.Departments;
using DirectoryService.Application.Locations;
using DirectoryService.Domain.Common;
using Xunit;

namespace DirectoryService.Tests;

public sealed class ValidationTests
{
    [Fact]
    public async Task CreateLocationCommandValidator_WithEmptyNameAndAddress_ReturnsMultipleValidationErrors()
    {
        var validator = new CreateLocationCommandValidator();
        var command = new CreateLocationCommand(string.Empty, string.Empty);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);

        var errorList = result.ToErrorList();
        Assert.Equal(2, errorList.Count);
        Assert.All(errorList, e => Assert.Equal(ErrorType.Validation, e.Type));
        Assert.Contains(errorList, e => e.InvalidField == "Name");
        Assert.Contains(errorList, e => e.InvalidField == "Address");
    }

    [Fact]
    public async Task CreateDepartmentCommandValidator_WithInvalidSlugAndName_ReturnsValidationErrors()
    {
        var validator = new CreateDepartmentCommandValidator();
        var command = new CreateDepartmentCommand(string.Empty, "INVALID SLUG!", null, null);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        var errorList = result.ToErrorList();
        Assert.True(errorList.Count >= 2);
        Assert.All(errorList, e => Assert.Equal(ErrorType.Validation, e.Type));
    }

    [Fact]
    public async Task CreateDepartmentCommandValidator_WhenNameExceedsMaxLength_ReturnsExpectedErrorMessage()
    {
        var validator = new CreateDepartmentCommandValidator();
        var command =
            new CreateDepartmentCommand(new string('a', AppConstants.MaxNameLength + 1), "valid-slug", null, null);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateDepartmentCommand.Name)
                                            && e.ErrorMessage ==
                                            $"Department name cannot exceed {AppConstants.MaxNameLength} characters.");
    }

    [Fact]
    public async Task CreateLocationCommandValidator_WhenFieldsExceedMaxLength_ReturnsExpectedErrorMessages()
    {
        var validator = new CreateLocationCommandValidator();
        var command = new CreateLocationCommand(
            new string('a', AppConstants.MaxNameLength + 1),
            new string('b', AppConstants.MaxAddressLength + 1));

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateLocationCommand.Name)
                                            && e.ErrorMessage ==
                                            $"Location name cannot exceed {AppConstants.MaxNameLength} characters.");
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateLocationCommand.Address)
                                            && e.ErrorMessage ==
                                            $"Location address cannot exceed {AppConstants.MaxAddressLength} characters.");
    }
}
