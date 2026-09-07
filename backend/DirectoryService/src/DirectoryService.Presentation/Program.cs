using System.Globalization;
using DirectoryService.Application;
using DirectoryService.Contracts;
using DirectoryService.Presentation;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting web application");

    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilogLogging(builder.Configuration, builder.Environment);
    builder.Services.AddControllers();
    builder.Services.AddHealthChecks();
    builder.Services.AddOpenApi();

    builder.Services.AddDatabase(builder.Configuration);
    builder.Services.AddRepositories(builder.Configuration);

    builder.Services.AddApplication();
    builder.Services.AddScoped<IPositionsService, StubPositionsService>();

    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseDirectoryRequestLogging();

    app.MapGet("/", () => "Hello World!");
    app.MapHealthChecks("/health");
    app.MapControllers();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    await app.RunAsync().ConfigureAwait(false);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync().ConfigureAwait(false);
}
