using System.Data;
using DirectoryService.Application.Departments;
using DirectoryService.Application.Locations;
using DirectoryService.Infrastructure.Postgres;
using DirectoryService.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.EntityFrameworkCore.Destructurers;

namespace DirectoryService.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddSerilogLogging(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        var envName = environment?.EnvironmentName
                      ?? configuration["ASPNETCORE_ENVIRONMENT"]
                      ?? configuration["DOTNET_ENVIRONMENT"]
                      ?? "Development";

        services.AddSerilog((sp, lc) =>
        {
            lc.ReadFrom.Configuration(configuration)
                .ReadFrom.Services(sp)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithProperty("EnvironmentName", envName)
                .Enrich.WithProperty("ServiceName", "DirectoryService")
                .Enrich.WithExceptionDetails(new DestructuringOptionsBuilder()
                    .WithDefaultDestructurers()
                    .WithDestructurers([new DbUpdateExceptionDestructurer()]));

            var explicitSeqUrl = configuration["Seq:ServerUrl"] ?? configuration["SEQ_SERVER_URL"];
            if (!string.IsNullOrWhiteSpace(explicitSeqUrl))
            {
                var writeToSection = configuration.GetSection("Serilog:WriteTo");
                var hasSeqInConfig = writeToSection.GetChildren()
                    .Any(c => string.Equals(c["Name"], "Seq", StringComparison.OrdinalIgnoreCase));
                if (!hasSeqInConfig)
                {
                    lc.WriteTo.Seq(explicitSeqUrl, formatProvider: System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        });

        return services;
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres");
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IDbConnection>(_ =>
        {
            var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            return connection;
        });

        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services, IConfiguration configuration)
    {
        var repositoryImplementation = configuration["Repository:Implementation"] ?? "EFCore";

        if (repositoryImplementation.Equals("Dapper", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<ILocationRepository, DapperLocationRepository>();
            services.AddScoped<IDepartmentRepository, DapperDepartmentRepository>();
        }
        else
        {
            services.AddScoped<ILocationRepository, EfCoreLocationRepository>();
            services.AddScoped<IDepartmentRepository, EfCoreDepartmentRepository>();
        }

        return services;
    }

    public static IApplicationBuilder UseDirectoryRequestLogging(this IApplicationBuilder app)
    {
        return app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.GetLevel = static (httpContext, _, ex) => GetLogLevel(httpContext, ex);
            options.EnrichDiagnosticContext = static (diagnosticContext, httpContext) =>
                EnrichDiagnosticContext(diagnosticContext, httpContext);
        });
    }

    private static LogEventLevel GetLogLevel(HttpContext httpContext, Exception? ex)
    {
        if (ex is not null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            return LogEventLevel.Error;
        }

        if (httpContext.Response.StatusCode >= StatusCodes.Status400BadRequest)
        {
            return LogEventLevel.Warning;
        }

        return LogEventLevel.Information;
    }

    private static void EnrichDiagnosticContext(IDiagnosticContext diagnosticContext, HttpContext httpContext)
    {
        diagnosticContext.Set("RequestId", httpContext.TraceIdentifier);

        var routeData = httpContext.GetRouteData();
        var path = httpContext.Request.Path.Value ?? string.Empty;

        if (routeData.Values.TryGetValue("id", out var idVal) && idVal is not null)
        {
            if (path.StartsWith("/locations", StringComparison.OrdinalIgnoreCase))
            {
                diagnosticContext.Set("LocationId", idVal);
            }
            else if (path.StartsWith("/departments", StringComparison.OrdinalIgnoreCase))
            {
                diagnosticContext.Set("DepartmentId", idVal);
            }
        }

        if (routeData.Values.TryGetValue("locationId", out var locIdVal) && locIdVal is not null)
        {
            diagnosticContext.Set("LocationId", locIdVal);
        }

        if (routeData.Values.TryGetValue("departmentId", out var deptIdVal) && deptIdVal is not null)
        {
            diagnosticContext.Set("DepartmentId", deptIdVal);
        }
    }
}
