using CSharpFunctionalExtensions;
using DirectoryService.Domain.Common;

namespace DirectoryService.Domain;

public class Location
{
    // ReSharper disable once UnusedMember.Local
    private Location()
    {
        Name = string.Empty;
        Address = string.Empty;
    }

    private Location(Guid id, string name, string address)
    {
        Id = id;
        Name = name;
        Address = address;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public static Result<Location, Error> Create(Guid id, string name, string address)
    {
        if (id == Guid.Empty)
        {
            return Error.Validation("location.id.invalid", "Id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("location.name.invalid", "Location name cannot be empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            return Error.Validation("location.address.invalid", "Location address cannot be empty.", nameof(address));
        }

        return new Location(id, name.Trim(), address.Trim());
    }

    // ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
    // ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
    public Guid Id { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public string Name { get; private set; } 
    public string Address { get; private set; }

    public UnitResult<Error> ChangeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("location.name.invalid", "Location name cannot be empty.", nameof(name));
        }

        Name = name.Trim();
        UpdatedAt = DateTime.UtcNow;
        return UnitResult.Success<Error>();
    }

    // ReSharper disable once UnusedMember.Global
    public UnitResult<Error> ChangeAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return Error.Validation("location.address.invalid", "Location address cannot be empty.", nameof(address));
        }

        Address = address.Trim();
        UpdatedAt = DateTime.UtcNow;
        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> UpdateDetails(string name, string address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("location.name.invalid", "Location name cannot be empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            return Error.Validation("location.address.invalid", "Location address cannot be empty.", nameof(address));
        }

        Name = name.Trim();
        Address = address.Trim();
        UpdatedAt = DateTime.UtcNow;
        return UnitResult.Success<Error>();
    }
}
