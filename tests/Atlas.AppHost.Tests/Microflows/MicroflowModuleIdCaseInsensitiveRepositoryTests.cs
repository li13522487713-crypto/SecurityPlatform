using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Models;
using Atlas.Domain.Microflows.Entities;
using Atlas.Infrastructure.Repositories.Microflows;
using SqlSugar;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowModuleIdCaseInsensitiveRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"microflow-module-case-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task ResourceRepository_ListAsync_Matches_ModuleId_Case_Insensitively()
    {
        var db = CreateDb();
        var repository = new MicroflowResourceRepository(db);
        await db.Insertable(new MicroflowResourceEntity
        {
            Id = "mf-sales-1",
            WorkspaceId = "ws-1",
            TenantId = "tenant-1",
            ModuleId = "Sales",
            ModuleName = "Sales",
            Name = "CaseInsensitiveFlow",
            DisplayName = "CaseInsensitiveFlow",
            Version = "0.1.0",
            Status = "draft",
            PublishStatus = "neverPublished",
            LastRunStatus = "neverRun",
            LastRunAt = DateTimeOffset.UtcNow,
            SchemaId = "schema-1",
            CurrentSchemaSnapshotId = "schema-1",
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        }).ExecuteCommandAsync();

        var result = await repository.ListAsync(new MicroflowResourceQueryDto
        {
            WorkspaceId = "ws-1",
            TenantId = "tenant-1",
            ModuleId = "sales",
            PageIndex = 1,
            PageSize = 20
        }, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Sales", result[0].ModuleId);
        Assert.Equal("CaseInsensitiveFlow", result[0].Name);
    }

    [Fact]
    public async Task FolderRepository_List_And_Exists_Match_ModuleId_Case_Insensitively()
    {
        var db = CreateDb();
        var repository = new MicroflowFolderRepository(db);
        await db.Insertable(new MicroflowFolderEntity
        {
            Id = "folder-sales-1",
            WorkspaceId = "ws-1",
            TenantId = "tenant-1",
            ModuleId = "Sales",
            Name = "RootFolder",
            Path = "RootFolder",
            ParentFolderId = null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        }).ExecuteCommandAsync();

        var folders = await repository.ListByModuleAsync("ws-1", "tenant-1", "sales", CancellationToken.None);
        var exists = await repository.ExistsBySiblingNameAsync("ws-1", "tenant-1", "sales", null, "RootFolder", null, CancellationToken.None);

        Assert.Single(folders);
        Assert.Equal("Sales", folders[0].ModuleId);
        Assert.True(exists);
    }

    private ISqlSugarClient CreateDb()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={_dbPath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });
        db.CodeFirst.InitTables<MicroflowResourceEntity, MicroflowFolderEntity>();
        return db;
    }

    public void Dispose()
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
        }
    }
}
