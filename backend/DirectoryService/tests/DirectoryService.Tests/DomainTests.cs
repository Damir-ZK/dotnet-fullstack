using DirectoryService.Domain;
using DirectoryService.Domain.Common;
using DirectoryService.Domain.Departments;
using Xunit;

namespace DirectoryService.Tests;

public sealed class DomainTests
{
    [Fact]
    public void Location_Create_WithValidData_ReturnsSuccess()
    {
        var id = Guid.NewGuid();
        var result = Location.Create(id, "Headquarters", "123 Main St");

        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Value.Id);
        Assert.Equal("Headquarters", result.Value.Name);
        Assert.Equal("123 Main St", result.Value.Address);
    }

    [Theory]
    [InlineData("", "123 Main St")]
    [InlineData("   ", "123 Main St")]
    [InlineData("Headquarters", "")]
    [InlineData("Headquarters", "   ")]
    public void Location_Create_WithInvalidData_ReturnsFailure(string name, string address)
    {
        var result = Location.Create(Guid.NewGuid(), name, address);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Location_Create_WithEmptyId_ReturnsFailure()
    {
        var result = Location.Create(Guid.Empty, "Headquarters", "123 Main St");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("location.id.invalid", result.Error.Code);
    }

    [Fact]
    public void Location_UpdateDetails_WithInvalidData_DoesNotMutateState()
    {
        var location = Location.Create(Guid.NewGuid(), "Original Name", "Original Address").Value;

        var result = location.UpdateDetails(string.Empty, "New Address");

        Assert.True(result.IsFailure);
        Assert.Equal("Original Name", location.Name);
        Assert.Equal("Original Address", location.Address);
    }

    [Fact]
    public void Location_ChangeName_WithValidData_ReturnsSuccess()
    {
        var location = Location.Create(Guid.NewGuid(), "Original Name", "Original Address").Value;

        var result = location.ChangeName("New Name");

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", location.Name);
    }

    [Fact]
    public void Location_ChangeName_WithEmptyName_DoesNotMutateState()
    {
        var location = Location.Create(Guid.NewGuid(), "Original Name", "Original Address").Value;

        var result = location.ChangeName("   ");

        Assert.True(result.IsFailure);
        Assert.Equal("Original Name", location.Name);
    }

    [Fact]
    public void Department_Create_WithValidData_ReturnsSuccess()
    {
        var id = Guid.NewGuid();
        var result = Department.Create(id, "Engineering", "engineering", null);

        Assert.True(result.IsSuccess);
        Assert.Equal("Engineering", result.Value.Name);
        Assert.Equal("engineering", result.Value.Slug);
        Assert.Equal("engineering", result.Value.Path);
    }

    [Fact]
    public void Department_Create_WithParent_BuildsPathCorrectly()
    {
        var parent = Department.Create(Guid.NewGuid(), "Engineering", "eng", null).Value;
        var child = Department.Create(Guid.NewGuid(), "Backend", "backend", parent).Value;

        Assert.Equal("eng/backend", child.Path);
        Assert.Equal(parent.Id, child.ParentId);
    }

    [Theory]
    [InlineData("", "eng")]
    [InlineData("Engineering", "")]
    [InlineData("Engineering", "invalid_slug!")]
    [InlineData("Engineering", "Upper-Case")]
    public void Department_Create_WithInvalidData_ReturnsFailure(string name, string slug)
    {
        var result = Department.Create(Guid.NewGuid(), name, slug, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Department_ChangeName_WithEmptyName_DoesNotMutateState()
    {
        var department = Department.Create(Guid.NewGuid(), "Engineering", "engineering", null).Value;

        var result = department.ChangeName("   ");

        Assert.True(result.IsFailure);
        Assert.Equal("Engineering", department.Name);
    }

    [Fact]
    public void DepartmentLocation_Create_WithValidData_ReturnsSuccess()
    {
        var id = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var locId = Guid.NewGuid();

        var result = DepartmentLocation.Create(id, deptId, locId, isPrimaryLocation: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Value.Id);
        Assert.Equal(deptId, result.Value.DepartmentId);
        Assert.Equal(locId, result.Value.LocationId);
        Assert.True(result.Value.IsPrimaryLocation);
    }

    [Fact]
    public void DepartmentLocation_Create_WithEmptyGuids_ReturnsFailure()
    {
        var result = DepartmentLocation.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid());
        Assert.True(result.IsFailure);

        result = DepartmentLocation.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());
        Assert.True(result.IsFailure);

        result = DepartmentLocation.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Position_Create_WithValidData_ReturnsSuccess()
    {
        var id = Guid.NewGuid();
        var result = Position.Create(id, "Software Engineer");

        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Value.Id);
        Assert.Equal("Software Engineer", result.Value.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Position_Create_WithInvalidName_ReturnsFailure(string name)
    {
        var result = Position.Create(Guid.NewGuid(), name);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void DepartmentPositions_Create_WithValidData_ReturnsSuccess()
    {
        var id = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var posIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        var result = DepartmentPositions.Create(id, deptId, posIds);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.PositionIds.Count);
    }

    [Fact]
    public void DepartmentPositions_Create_WithEmptyGuidInList_ReturnsFailure()
    {
        var result = DepartmentPositions.Create(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid(), Guid.Empty]);

        Assert.True(result.IsFailure);
        Assert.Equal("department_positions.position_ids.invalid", result.Error.Code);
    }
}
