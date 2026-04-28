using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using SqlSugar;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class SqliteSchemaAlignmentTests
{
    private static readonly Guid TenantIdValue = Guid.Parse("00000000-0000-0000-0000-000000000531");

    [Fact]
    public async Task RebuildTablePreservingIntersectionAsync_ShouldDropLegacyPromptTemplateColumn_AndKeepAgentData()
    {
        var dbPath = NewDbPath();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateLegacyAgentTableAsync(dbPath);

            await SqliteSchemaAlignment.RebuildTablePreservingIntersectionAsync<Agent>(db, CancellationToken.None);

            var columns = db.DbMaintenance.GetColumnInfosByTableName("Agent", false)
                .Select(column => column.DbColumnName)
                .ToArray();
            Assert.DoesNotContain("PromptTemplateId", columns, StringComparer.OrdinalIgnoreCase);

            var entity = (await db.Queryable<Agent>()
                .Where(x => x.Id == 91001L)
                .ToListAsync(CancellationToken.None))
                .Single();
            Assert.NotNull(entity);
            Assert.Equal("LegacyAgent", entity.Name);
            Assert.Equal(17L, entity.WorkspaceId);
            Assert.Equal("legacy prompt version", entity.PromptVersion);
            Assert.Equal("legacy-model", entity.ModelName);
            Assert.Equal(TenantIdValue, entity.TenantIdValue);
        }
        finally
        {
            DeleteDb(dbPath);
        }
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
                    if (property.Name == nameof(TenantEntity.TenantId))
                    {
                        column.IsIgnore = true;
                    }
                }
            }
        });
    }

    private static async Task CreateLegacyAgentTableAsync(string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        const string createTableSql =
            """
            CREATE TABLE "Agent"(
                "Name" TEXT NOT NULL,
                "WorkspaceId" INTEGER NOT NULL,
                "Description" TEXT NOT NULL,
                "AvatarUrl" TEXT NOT NULL,
                "SystemPrompt" TEXT NOT NULL,
                "PersonaMarkdown" TEXT NOT NULL,
                "Goals" TEXT NOT NULL,
                "ReplyLogic" TEXT NOT NULL,
                "OutputFormat" TEXT NOT NULL,
                "Constraints" TEXT NOT NULL,
                "OpeningMessage" TEXT NOT NULL,
                "PresetQuestionsJson" TEXT NOT NULL,
                "DatabaseBindingsJson" TEXT NOT NULL,
                "VariableBindingsJson" TEXT NOT NULL,
                "Mode" INTEGER NOT NULL,
                "PromptTemplateId" INTEGER NOT NULL,
                "PromptVersion" TEXT NOT NULL,
                "LayoutConfigJson" TEXT NOT NULL,
                "DebugConfigJson" TEXT NOT NULL,
                "PublishedConnectorConfigJson" TEXT NOT NULL,
                "ModelConfigId" INTEGER NOT NULL,
                "ModelName" TEXT NOT NULL,
                "Temperature" REAL NOT NULL,
                "MaxTokens" INTEGER NOT NULL,
                "DefaultWorkflowId" INTEGER NOT NULL,
                "DefaultWorkflowName" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "CreatorId" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "PublishedAt" TEXT NOT NULL,
                "PublishVersion" INTEGER NOT NULL,
                "EnableMemory" INTEGER NOT NULL,
                "EnableShortTermMemory" INTEGER NOT NULL,
                "EnableLongTermMemory" INTEGER NOT NULL,
                "LongTermMemoryTopK" INTEGER NOT NULL,
                "TenantIdValue" TEXT NOT NULL,
                "Id" INTEGER NOT NULL
            );
            """;

        await using (var createCommand = connection.CreateCommand())
        {
            createCommand.CommandText = createTableSql;
            await createCommand.ExecuteNonQueryAsync();
        }

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText =
            """
            INSERT INTO "Agent" (
                "Name","WorkspaceId","Description","AvatarUrl","SystemPrompt","PersonaMarkdown","Goals","ReplyLogic",
                "OutputFormat","Constraints","OpeningMessage","PresetQuestionsJson","DatabaseBindingsJson",
                "VariableBindingsJson","Mode","PromptTemplateId","PromptVersion","LayoutConfigJson","DebugConfigJson",
                "PublishedConnectorConfigJson","ModelConfigId","ModelName","Temperature","MaxTokens",
                "DefaultWorkflowId","DefaultWorkflowName","Status","CreatorId","CreatedAt","UpdatedAt","PublishedAt",
                "PublishVersion","EnableMemory","EnableShortTermMemory","EnableLongTermMemory","LongTermMemoryTopK",
                "TenantIdValue","Id"
            )
            VALUES (
                $name,$workspaceId,$description,$avatarUrl,$systemPrompt,$personaMarkdown,$goals,$replyLogic,
                $outputFormat,$constraints,$openingMessage,$presetQuestionsJson,$databaseBindingsJson,
                $variableBindingsJson,$mode,$promptTemplateId,$promptVersion,$layoutConfigJson,$debugConfigJson,
                $publishedConnectorConfigJson,$modelConfigId,$modelName,$temperature,$maxTokens,
                $defaultWorkflowId,$defaultWorkflowName,$status,$creatorId,$createdAt,$updatedAt,$publishedAt,
                $publishVersion,$enableMemory,$enableShortTermMemory,$enableLongTermMemory,$longTermMemoryTopK,
                $tenantIdValue,$id
            );
            """;
        insertCommand.Parameters.AddWithValue("$name", "LegacyAgent");
        insertCommand.Parameters.AddWithValue("$workspaceId", 17L);
        insertCommand.Parameters.AddWithValue("$description", "legacy description");
        insertCommand.Parameters.AddWithValue("$avatarUrl", "https://example.com/avatar.png");
        insertCommand.Parameters.AddWithValue("$systemPrompt", "legacy system prompt");
        insertCommand.Parameters.AddWithValue("$personaMarkdown", "legacy persona");
        insertCommand.Parameters.AddWithValue("$goals", "legacy goals");
        insertCommand.Parameters.AddWithValue("$replyLogic", "legacy reply");
        insertCommand.Parameters.AddWithValue("$outputFormat", "legacy format");
        insertCommand.Parameters.AddWithValue("$constraints", "legacy constraints");
        insertCommand.Parameters.AddWithValue("$openingMessage", "legacy opening");
        insertCommand.Parameters.AddWithValue("$presetQuestionsJson", "[]");
        insertCommand.Parameters.AddWithValue("$databaseBindingsJson", "[]");
        insertCommand.Parameters.AddWithValue("$variableBindingsJson", "[]");
        insertCommand.Parameters.AddWithValue("$mode", 0);
        insertCommand.Parameters.AddWithValue("$promptTemplateId", 991L);
        insertCommand.Parameters.AddWithValue("$promptVersion", "legacy prompt version");
        insertCommand.Parameters.AddWithValue("$layoutConfigJson", "{}");
        insertCommand.Parameters.AddWithValue("$debugConfigJson", "{}");
        insertCommand.Parameters.AddWithValue("$publishedConnectorConfigJson", "{}");
        insertCommand.Parameters.AddWithValue("$modelConfigId", 0L);
        insertCommand.Parameters.AddWithValue("$modelName", "legacy-model");
        insertCommand.Parameters.AddWithValue("$temperature", 0.2d);
        insertCommand.Parameters.AddWithValue("$maxTokens", 1024);
        insertCommand.Parameters.AddWithValue("$defaultWorkflowId", 0L);
        insertCommand.Parameters.AddWithValue("$defaultWorkflowName", string.Empty);
        insertCommand.Parameters.AddWithValue("$status", 0);
        insertCommand.Parameters.AddWithValue("$creatorId", 45L);
        insertCommand.Parameters.AddWithValue("$createdAt", DateTime.UtcNow);
        insertCommand.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow);
        insertCommand.Parameters.AddWithValue("$publishedAt", DateTime.UnixEpoch);
        insertCommand.Parameters.AddWithValue("$publishVersion", 0);
        insertCommand.Parameters.AddWithValue("$enableMemory", 1);
        insertCommand.Parameters.AddWithValue("$enableShortTermMemory", 1);
        insertCommand.Parameters.AddWithValue("$enableLongTermMemory", 1);
        insertCommand.Parameters.AddWithValue("$longTermMemoryTopK", 3);
        insertCommand.Parameters.AddWithValue("$tenantIdValue", TenantIdValue.ToString());
        insertCommand.Parameters.AddWithValue("$id", 91001L);
        await insertCommand.ExecuteNonQueryAsync();
    }

    private static string NewDbPath()
        => Path.Combine(Path.GetTempPath(), $"sqlite-alignment-{Guid.NewGuid():N}.db");

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
        catch
        {
        }
    }
}
