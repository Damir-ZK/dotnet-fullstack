using DirectoryService.Domain.Common;
using DirectoryService.Presentation.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace DirectoryService.Presentation;

#pragma warning disable CA1515 // Consider making public types internal
public sealed class GlobalExceptionHandler : IExceptionHandler
#pragma warning restore CA1515 // Consider making public types internal
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "An unhandled exception occurred while processing {RequestMethod} {RequestPath}: {ErrorMessage}",
            httpContext.Request.Method,
            httpContext.Request.Path.Value,
            exception.Message);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var envelope = Envelope.Error(Errors.General.Failure());

        await httpContext.Response.WriteAsJsonAsync(envelope, cancellationToken);
        return true;
    }
}
