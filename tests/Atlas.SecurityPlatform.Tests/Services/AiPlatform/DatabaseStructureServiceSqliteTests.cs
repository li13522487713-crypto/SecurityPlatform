using System.Reflection;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.AiPlatform;
using Atlas.Infrastructure.Services.DatabaseStructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SqlSugar;

namespace Atlas.SecurityPlatform.Tests.Services.AiPlatform;

public sealed class DatabaseStructureServiceSqliteTests : IDisposable
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000123"));

    private readonly string _tempDir;
    private readonly SqlSugarScope _db;
    private readonly AiDatabaseRepository _repository;

    public DatabaseStructureServiceSqliteTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"atlas-structure-sqlite-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _db = new SqlSugarScope(new ConnectionConfig
        {
            ConnectionString = $"Data Source={Path.Combine(_tempDir, "metadata.db")}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = ApplyEntityService
            }
        });
        _db.CodeFirst.InitTables(typeof(AiDatabase));
        _repository = new AiDatabaseRepository(_db);
    }

    [Fact]
    public async Task SqliteProvisioner_CreatesDraftOnlineFilesAndStructureServiceUsesDraft()
    {
        var database = new AiDatabase(Tenant, "structure-demo", string.Empty, botId: null, "[]", id: 1234567890123, workspaceId: 1);
        database.SetStandaloneDriver("SQLite");
        await _repository.AddAsync(database, CancellationToken.None);

        var options = Options.Create(new AiDatabaseHostingOptions
        {
            Sqlite = new AiDatabaseSqliteHostingOptions { Root = Path.Combine(_tempDir, "physical") },
            PreviewLimit = 100,
            CommandTimeoutSeconds = 10
        });
        var encryption = Options.Create(new DatabaseEncryptionOptions { Enabled = false });
        var hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.ContentRootPath.Returns(_tempDir);
        var provisioner = new AiDatabaseProvisionService(
            _repository,
            options,
            encryption,
            hostEnvironment,
            NullLogger<AiDatabaseProvisionService>.Instance);

        await provisioner.EnsureProvisionedAsync(database, CancellationToken.None);

        Assert.Equal(AiDatabaseProvisionState.Ready, database.ProvisionState);
        Assert.True(File.Exists(ExtractSqlitePath(database.EncryptedDraftConnection)));
        Assert.True(File.Exists(ExtractSqlitePath(database.EncryptedOnlineConnection)));

        var clientFactory = new AiDatabaseClientFactory(
            _repository,
            provisioner,
            encryption,
            options,
            NullLogger<AiDatabaseClientFactory>.Instance);
        var service = new DatabaseStructureService(
            clientFactory,
            CreateRegistry(),
            new SqlSafetyValidator(),
            options,
            NullLogger<DatabaseStructureService>.Instance);

        await service.CreateTableAsync(
            Tenant,
            database.Id,
            new CreateTableRequest(
                null,
                "sys_user_demo",
                "demo table",
                [
                    new TableColumnDesignDto("id", "INTEGER", Nullable: false, PrimaryKey: true, AutoIncrement: true),
                    new TableColumnDesignDto("name", "TEXT", Nullable: false)
                ]),
            CancellationToken.None);

        var draftClient = await clientFactory.GetClientAsync(Tenant, database.Id, AiDatabaseRecordEnvironment.Draft, CancellationToken.None);
        await draftClient.Ado.ExecuteCommandAsync("INSERT INTO \"sys_user_demo\" (\"name\") VALUES ('alice')");

        var objects = await service.GetObjectsAsync(Tenant, database.Id, AiDatabaseRecordEnvironment.Draft, "table", CancellationToken.None);
        Assert.Contains(objects, item => item.Name == "sys_user_demo" && item.ObjectType == "table");

        var columns = await service.GetTableColumnsAsync(Tenant, database.Id, AiDatabaseRecordEnvironment.Draft, "sys_user_demo", null, CancellationToken.None);
        Assert.Contains(columns, column => column.Name == "id" && column.PrimaryKey);

        var ddl = await service.GetTableDdlAsync(Tenant, database.Id, AiDatabaseRecordEnvironment.Draft, "sys_user_demo", null, CancellationToken.None);
        Assert.Contains("CREATE TABLE", ddl.Ddl, StringComparison.OrdinalIgnoreCase);

        var preview = await service.PreviewTableDataAsync(Tenant, database.Id, "sys_user_demo", new PreviewDataRequest(null, 1, 20), CancellationToken.None);
        Assert.Single(preview.Rows);
    }

    [Theory]
    [InlineData("MySql", "AiDatabaseHosting:MySql:AdminConnection")]
    [InlineData("PostgreSQL", "AiDatabaseHosting:PostgreSql:AdminConnection")]
    public async Task ValidateHostingOptions_MissingAdminConnection_ReturnsClearError(string driverCode, string expectedMessage)
    {
        var hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.ContentRootPath.Returns(_tempDir);
        var provisioner = new AiDatabaseProvisionService(
            _repository,
            Options.Create(new AiDatabaseHostingOptions()),
            Options.Create(new DatabaseEncryptionOptions()),
            hostEnvironment,
            NullLogger<AiDatabaseProvisionService>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provisioner.ValidateHostingOptionsAsync(driverCode, CancellationToken.None));

        Assert.Contains(expectedMessage, exception.Message);
    }

    public void Dispose()
    {
        _db.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static DatabaseDialectRegistry CreateRegistry()
        => new(
        [
            new SqliteDatabaseDialect(),
            new MySqlDatabaseDialect(),
            new PostgreSqlDatabaseDialect(),
            new SqlServerDatabaseDialect(),
            new OracleDatabaseDialect(),
            new DmDatabaseDialect(),
            new KingbaseDatabaseDialect(),
            new OscarDatabaseDialect()
        ]);

    private static string ExtractSqlitePath(string connectionString)
        => connectionString["Data Source=".Length..].TrimEnd(';');

    private static void ApplyEntityService(PropertyInfo property, EntityColumnInfo column)
    {
        if (property.Name == nameof(AiDatabase.TenantId))
        {
            column.IsIgnore = true;
            return;
        }

        if (property.Name == nameof(AiDatabase.Id))
        {
            column.IsPrimarykey = true;
            column.IsIdentity = false;
        }

        var type = property.PropertyType;
        var nullableContext = new NullabilityInfoContext().Create(property);
        if (Nullable.GetUnderlyingType(type) != null ||
            (type == typeof(string) && nullableContext.WriteState == NullabilityState.Nullable))
        {
            column.IsNullable = true;
        }
    }
}
