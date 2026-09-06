using System.Data;
using System.Globalization;
using DirectoryService.Application;
using DirectoryService.Application.Departments;
using DirectoryService.Application.Locations;
using DirectoryService.Contracts;
using DirectoryService.Infrastructure.Postgres;
using DirectoryService.Infrastructure.Postgres.Repositories;
using DirectoryService.Presentation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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

    builder.Services.AddSerilogLogging(builder.Configuration);
    builder.Services.AddControllers();
    builder.Services.AddHealthChecks();
    builder.Services.AddOpenApi();

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));
    builder.Services.AddScoped<IDbConnection>(_ =>
    {
        var connection = new NpgsqlConnection(builder.Configuration.GetConnectionString("Postgres"));
        connection.Open();
        return connection;
    });

    var repositoryImplementation = builder.Configuration["Repository:Implementation"] ?? "EFCore";

    if (repositoryImplementation.Equals("Dapper", StringComparison.OrdinalIgnoreCase))
    {
        builder.Services.AddScoped<ILocationRepository, DapperLocationRepository>();
        builder.Services.AddScoped<IDepartmentRepository, DapperDepartmentRepository>();
    }
    else
    {
        builder.Services.AddScoped<ILocationRepository, EfCoreLocationRepository>();
        builder.Services.AddScoped<IDepartmentRepository, EfCoreDepartmentRepository>();
    }

    builder.Services.AddApplication();
    builder.Services.AddScoped<IPositionsService, StubPositionsService>();

    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

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

