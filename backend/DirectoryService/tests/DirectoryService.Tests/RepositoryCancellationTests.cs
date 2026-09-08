using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using DirectoryService.Infrastructure.Postgres;
using DirectoryService.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DirectoryService.Tests;

public sealed class RepositoryCancellationTests
{
    private sealed class CancellingDbConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "dummy";
        public override string DataSource => "dummy";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            throw new OperationCanceledException();

        protected override DbCommand CreateDbCommand() =>
            throw new OperationCanceledException();
    }

    [Fact]
    public async Task DapperLocationRepository_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var connection = new CancellingDbConnection();
        var repo = new DapperLocationRepository(connection, NullLogger<DapperLocationRepository>.Instance);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repo.NameExistsAsync("Test", cts.Token));
    }

    [Fact]
    public async Task DapperDepartmentRepository_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var connection = new CancellingDbConnection();
        var repo = new DapperDepartmentRepository(connection, NullLogger<DapperDepartmentRepository>.Instance);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repo.NameExistsAsync("Test", cts.Token));
    }

    [Fact]
    public async Task DapperLocationRepository_GetAllAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var connection = new CancellingDbConnection();
        var repo = new DapperLocationRepository(connection, NullLogger<DapperLocationRepository>.Instance);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repo.GetAllAsync(cts.Token));
    }

    [Fact]
    public async Task DapperDepartmentRepository_GetAllAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var connection = new CancellingDbConnection();
        var repo = new DapperDepartmentRepository(connection, NullLogger<DapperDepartmentRepository>.Instance);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repo.GetAllAsync(cts.Token));
    }

    [Fact]
    public async Task EfCoreLocationRepository_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var connection = new CancellingDbConnection();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection)
            .Options;
        await using var dbContext = new AppDbContext(options);
        var repo = new EfCoreLocationRepository(dbContext, NullLogger<EfCoreLocationRepository>.Instance);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repo.GetAllAsync(cts.Token));
    }

    [Fact]
    public async Task EfCoreDepartmentRepository_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var connection = new CancellingDbConnection();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection)
            .Options;
        await using var dbContext = new AppDbContext(options);
        var repo = new EfCoreDepartmentRepository(dbContext, NullLogger<EfCoreDepartmentRepository>.Instance);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repo.GetAllAsync(cts.Token));
    }
}
