using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Repositories.LowCode;
using Atlas.Infrastructure.Services.LowCode;
using SqlSugar;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.LowCode;

public sealed class LowCodeAppResourceBindingServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000031"));

    [Fact]
    public async Task ListByAppAsync_ShouldReturnEmptyList_ForExistingLowcodeApp()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchemaAsync(db);
            await SeedLowcodeAppAsync(db, appId: 1001);
            var service = CreateService(db);

            var result = await service.ListByAppAsync(Tenant, 1001, resourceType: null, CancellationToken.None);

            Assert.Empty(result);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task ListByAppAsync_ShouldThrowNotFound_WhenLowcodeAppMissing()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchemaAsync(db);
            var service = CreateService(db);

            var ex = await Assert.ThrowsAsync<BusinessException>(() =>
                service.ListByAppAsync(Tenant, 404, resourceType: null, CancellationToken.None));

            Assert.Equal(ErrorCodes.NotFound, ex.Code);
            Assert.Equal("应用不存在。", ex.Message);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    private static LowCodeAppResourceBindingService CreateService(SqlSugarClient db)
    {
        var bindingRepository = new AiAppResourceBindingRepository(db);
        var appRepository = new AppDefinitionRepository(db);
        var knowledgeBaseRepository = new KnowledgeBaseRepository(db);
        var databaseRepository = new AiDatabaseRepository(db);
        return new LowCodeAppResourceBindingService(
            bindingRepository,
            appRepository,
            knowledgeBaseRepository,
            databaseRepository,
            new FixedIdGenerator());
    }

    private static async Task CreateSchemaAsync(SqlSugarClient db)
    {
        db.CodeFirst.InitTables<AppDefinition, AiAppResourceBinding>();
        await Task.CompletedTask;
    }

    private static async Task SeedLowcodeAppAsync(SqlSugarClient db, long appId)
    {
        IAppDefinitionRepository repository = new AppDefinitionRepository(db);
        var app = new AppDefinition(Tenant, appId, "demo-app", "演示应用", "web", "zh-CN");
        app.SetCreatedByUser(1);
        await repository.InsertAsync(app, CancellationToken.None);
    }

    private static SqlSugarClient CreateDb(string path)
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = (property, column) =>
                {
                    if (property.Name == nameof(Atlas.Core.Abstractions.TenantEntity.TenantId))
                    {
                        column.IsIgnore = true;
                    }
                }
            }
        });
    }

    private static string NewDb()
        => Path.Combine(Path.GetTempPath(), $"atlas-lowcode-binding-{Guid.NewGuid():N}.db");

    private static void DeleteDb(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // SQLite 在测试结束后偶发延迟释放文件句柄；该临时文件不影响测试断言。
        }
    }

    private sealed class FixedIdGenerator : IIdGeneratorAccessor
    {
        private long _next = 5000;

        public long NextId() => Interlocked.Increment(ref _next);
    }
}
