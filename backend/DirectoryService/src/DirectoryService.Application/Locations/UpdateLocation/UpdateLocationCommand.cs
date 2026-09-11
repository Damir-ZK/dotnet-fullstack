using DirectoryService.Application.Common;

// ReSharper disable once CheckNamespace
namespace DirectoryService.Application.Locations;

public sealed record UpdateLocationCommand(Guid Id, string Name, string Address) : ICommand;
