using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Atlas.Core.Setup;
using Atlas.Infrastructure.Services;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Atlas.SecurityPlatform.Tests.Integration;

public sealed class AgentLegacySchemaIntegrationTests
{
    [Fact]
    public async Task Startup_ShouldRetireLegacyPromptTemplateColumn_WhenSchemaMigrationsSkipped()
    {
        using var factory = new LegacyAgentSchemaWebApplicationFactory();
        using var client = factory.CreateClient();

        await using var connection = new SqliteConnection($"Data Source={factory.DatabasePath}");
        await connection.OpenAsync();

        var columns = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(\"Agent\");";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }
        }

        Assert.DoesNotContain("PromptTemplateId", columns, StringComparer.OrdinalIgnoreCase);

        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT \"Name\" FROM \"Agent\" WHERE \"Id\" = $id;";
        query.Parameters.AddWithValue("$id", LegacyAgentSchemaWebApplicationFactory.LegacyAgentId);
        var preservedName = (string?)await query.ExecuteScalarAsync();
        Assert.Equal("LegacyAgent", preservedName);
    }

    [Fact]
    public async Task CreateAssistant_ShouldSucceed_AfterLegacySchemaAlignment_WhenSchemaMigrationsSkipped()
    {
        using var factory = new LegacyAgentSchemaWebApplicationFactory();
        using var client = factory.CreateClient();

        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(client);
        IntegrationAuthHelper.SetAuthorizationHeaders(client, accessToken);

        using var response = await client.PostAsJsonAsync("/api/v1/ai-assistants", new
        {
            name = "RegressionAgent",
            description = "legacy schema regression",
            avatarUrl = "https://example.com/avatar.png",
            systemPrompt = "system prompt",
            personaMarkdown = "persona",
            goals = "goals",
            replyLogic = "logic",
            outputFormat = "markdown",
            constraints = "constraints",
            openingMessage = "hello",
            presetQuestions = new[] { "q1", "q2" },
            knowledgeBindings = Array.Empty<object>(),
            databaseBindings = Array.Empty<object>(),
            variableBindings = Array.Empty<object>(),
            knowledgeBaseIds = Array.Empty<long>(),
            databaseBindingIds = Array.Empty<long>(),
            variableBindingIds = Array.Empty<long>(),
            modelConfigId = (long?)null,
            modelName = (string?)null,
            temperature = 0.3,
            maxTokens = 2048,
            defaultWorkflowId = (long?)null,
            defaultWorkflowName = (string?)null,
            enableMemory = true,
            enableShortTermMemory = true,
            enableLongTermMemory = true,
            longTermMemoryTopK = 3,
            workspaceId = (long?)null
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(payload);
        Assert.True(payload.Success);

        await using var connection = new SqliteConnection($"Data Source={factory.DatabasePath}");
        await connection.OpenAsync();

        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT COUNT(1) FROM \"Agent\" WHERE \"Name\" = $name;";
        query.Parameters.AddWithValue("$name", "RegressionAgent");
        var createdCount = Convert.ToInt32(await query.ExecuteScalarAsync());
        Assert.Equal(1, createdCount);
    }

    private sealed class LegacyAgentSchemaWebApplicationFactory : WebApplicationFactory<Program>
    {
        public const long LegacyAgentId = 91001L;
        private readonly string _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "atlas-agent-legacy-tests",
            Guid.NewGuid().ToString("N"));

        public LegacyAgentSchemaWebApplicationFactory()
        {
            Directory.CreateDirectory(_tempDirectory);
            DatabasePath = Path.Combine(_tempDirectory, "atlas.legacy-agent.db");
            SetupStatePath = Path.Combine(_tempDirectory, "setup-state.json");

            File.WriteAllText(
                SetupStatePath,
                JsonSerializer.Serialize(
                    new SetupStateInfo
                    {
                        Status = SetupState.Ready,
                        CompletedAt = DateTimeOffset.UtcNow,
                        PlatformSetupCompleted = true
                    }));

            CreateLegacyAgentSchema(DatabasePath);
        }

        public string DatabasePath { get; }

        private string SetupStatePath { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseDefaultServiceProvider((_, options) =>
            {
                options.ValidateScopes = false;
                options.ValidateOnBuild = false;
            });
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                var overrides = new Dictionary<string, string?>
                {
                    ["Setup:StateFilePath"] = SetupStatePath,
                    ["Security:EnforceHttps"] = "false",
                    ["Security:BootstrapAdmin:Enabled"] = "true",
                    ["Security:BootstrapAdmin:TenantId"] = IntegrationAuthHelper.DefaultTenantId,
                    ["Security:BootstrapAdmin:Username"] = IntegrationAuthHelper.DefaultUsername,
                    ["Security:BootstrapAdmin:Password"] = IntegrationAuthHelper.DefaultPassword,
                    ["Database:ConnectionString"] = $"Data Source={DatabasePath}",
                    ["DatabaseInitializer:SkipSchemaMigrations"] = "true",
                    ["DatabaseInitializer:SkipSchemaInit"] = "false",
                    ["DatabaseInitializer:SkipSeedData"] = "false"
                };

                configurationBuilder.AddInMemoryCollection(overrides);
            });
            builder.ConfigureServices(services =>
            {
                var hostedServiceDescriptors = services
                    .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
                    .ToList();

                foreach (var descriptor in hostedServiceDescriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddHostedService(sp => sp.GetRequiredService<DatabaseInitializerHostedService>());
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
            {
                return;
            }

            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, recursive: true);
                }
            }
            catch
            {
            }
        }

        private static void CreateLegacyAgentSchema(string dbPath)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using (var createCommand = connection.CreateCommand())
            {
                createCommand.CommandText =
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
                createCommand.ExecuteNonQuery();
            }

            using var insertCommand = connection.CreateCommand();
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
            insertCommand.Parameters.AddWithValue("$tenantIdValue", IntegrationAuthHelper.DefaultTenantId);
            insertCommand.Parameters.AddWithValue("$id", LegacyAgentId);
            insertCommand.ExecuteNonQuery();
        }
    }
}
