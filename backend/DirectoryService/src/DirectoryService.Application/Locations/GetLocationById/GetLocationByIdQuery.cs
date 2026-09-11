using DirectoryService.Application.Common;
using DirectoryService.Contracts;

// ReSharper disable once CheckNamespace
namespace DirectoryService.Application.Locations;

public sealed record GetLocationByIdQuery(Guid Id) : IQuery<LocationDto>;
