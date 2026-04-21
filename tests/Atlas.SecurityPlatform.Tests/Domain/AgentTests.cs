using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.SecurityPlatform.Tests.Domain;

public sealed class AgentTests
{
    [Fact]
    public void CreateDuplicate_ShouldKeepStructuredConfiguration_AfterUpdate()
    {
        var tenantId = new TenantId(Guid.Parse("00000000-0000-0000-0000-000000000641"));
        var agent = new Agent(tenantId, "OpsAgent", creatorId: 9L, id: 101L, workspaceId: 5L);

        agent.Update(
            name: "OpsAgent",
            description: "operations helper",
            avatarUrl: "https://example.com/avatar.png",
            systemPrompt: "system prompt",
            personaMarkdown: "persona",
            goals: "goals",
            replyLogic: "logic",
            outputFormat: "markdown",
            constraints: "constraints",
            openingMessage: "hello",
            presetQuestionsJson: "[\"q1\"]",
            databaseBindingsJson: "[]",
            variableBindingsJson: "[]",
            modelConfigId: 13L,
            modelName: "gpt-test",
            temperature: 0.4f,
            maxTokens: 2048,
            defaultWorkflowId: 3001L,
            defaultWorkflowName: "DefaultFlow",
            enableMemory: true,
            enableShortTermMemory: false,
            enableLongTermMemory: true,
            longTermMemoryTopK: 6,
            mode: AgentMode.Workflow,
            promptVersion: "v2",
            layoutConfigJson: "{\"mode\":\"form\"}",
            debugConfigJson: "{\"enabled\":true}",
            publishedConnectorConfigJson: "{\"connector\":\"web\"}",
            workspaceId: 5L);

        var duplicate = agent.CreateDuplicate(newId: 202L, newName: "OpsAgent Copy", creatorId: 10L);

        Assert.Equal("OpsAgent Copy", duplicate.Name);
        Assert.Equal(agent.WorkspaceId, duplicate.WorkspaceId);
        Assert.Equal(agent.SystemPrompt, duplicate.SystemPrompt);
        Assert.Equal(agent.DatabaseBindingsJson, duplicate.DatabaseBindingsJson);
        Assert.Equal(agent.VariableBindingsJson, duplicate.VariableBindingsJson);
        Assert.Equal(agent.PromptVersion, duplicate.PromptVersion);
        Assert.Equal(agent.DefaultWorkflowId, duplicate.DefaultWorkflowId);
        Assert.Equal(agent.DefaultWorkflowName, duplicate.DefaultWorkflowName);
        Assert.Equal(agent.EnableShortTermMemory, duplicate.EnableShortTermMemory);
        Assert.Equal(agent.LongTermMemoryTopK, duplicate.LongTermMemoryTopK);
    }
}
