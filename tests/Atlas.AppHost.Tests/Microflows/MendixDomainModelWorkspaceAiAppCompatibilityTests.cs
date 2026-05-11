using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Repositories;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Repositories.LowCode;
using Atlas.Infrastructure.Services.DatabaseStructure;
using Atlas.Infrastructure.Services.Microflows;
using NSubstitute;
using SqlSugar;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MendixDomainModelWorkspaceAiAppCompatibilityTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"mendix-domain-aiapp-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task GetMetadataCatalogAsync_Supports_WorkspaceIde_AiApp_Ids()
    {
        var tenantId = new TenantId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var db = CreateDb();
        var appId = 1503240060563099648L;
        var workspaceId = "1503227490552778752";

        await db.Ado.ExecuteCommandAsync(
            """
            INSERT INTO AiApp
            (Id, TenantId, TenantIdValue, Name, WorkspaceId, Description, Icon, AgentId, WorkflowId, PrimaryWorkflowId, EntryConversationTemplateId, PromptTemplateId, UiBuilderSchemaJson, WorkspaceLayoutJson, PublishedConnectorConfigJson, LastPublishedSnapshotJson, Status, PublishVersion, CreatedAt, UpdatedAt, PublishedAt)
            VALUES
            (@Id, @TenantId, @TenantIdValue, @Name, @WorkspaceId, @Description, @Icon, NULL, NULL, NULL, NULL, NULL, @UiBuilderSchemaJson, @WorkspaceLayoutJson, @PublishedConnectorConfigJson, @LastPublishedSnapshotJson, @Status, @PublishVersion, @CreatedAt, @UpdatedAt, NULL)
            """,
            new[]
            {
                new SugarParameter("@Id", appId),
                new SugarParameter("@TenantId", tenantId.Value.ToString()),
                new SugarParameter("@TenantIdValue", tenantId.Value),
                new SugarParameter("@Name", "E2E Mendix App"),
                new SugarParameter("@WorkspaceId", long.Parse(workspaceId)),
                new SugarParameter("@Description", "created for compatibility test"),
                new SugarParameter("@Icon", string.Empty),
                new SugarParameter("@UiBuilderSchemaJson", "{}"),
                new SugarParameter("@WorkspaceLayoutJson", "{}"),
                new SugarParameter("@PublishedConnectorConfigJson", "{}"),
                new SugarParameter("@LastPublishedSnapshotJson", "{}"),
                new SugarParameter("@Status", (int)AiAppStatus.Draft),
                new SugarParameter("@PublishVersion", 0),
                new SugarParameter("@CreatedAt", DateTime.UtcNow),
                new SugarParameter("@UpdatedAt", DateTime.UtcNow)
            });

        var accessor = Substitute.For<IMicroflowRequestContextAccessor>();
        accessor.Current.Returns(new MicroflowRequestContext
        {
            TenantId = tenantId.Value.ToString(),
            WorkspaceId = workspaceId,
            TraceId = "trace-mendix-domain-aiapp"
        });

        var service = new MendixDomainModelService(
            new MendixDomainModelDocumentRepository(db),
            Substitute.For<IAppDefinitionRepository>(),
            db,
            Substitute.For<ILowCodeAppResourceBindingService>(),
            Substitute.For<IDatabaseManagementService>(),
            Substitute.For<IDatabaseStructureService>(),
            Substitute.For<IDatabaseDialectRegistry>(),
            accessor,
            Substitute.For<IIdGeneratorAccessor>(),
            new AiDatabasePhysicalInstanceRepository(db));

        var catalog = await service.GetMetadataCatalogAsync(appId.ToString(), workspaceId, "Sales", CancellationToken.None);

        Assert.Null(catalog);
    }

    private ISqlSugarClient CreateDb()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={_dbPath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });
        db.CodeFirst.InitTables<AiApp, MendixDomainModelDocument>();
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
