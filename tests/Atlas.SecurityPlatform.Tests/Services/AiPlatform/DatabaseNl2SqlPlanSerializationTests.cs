using System.Text.Json;
using Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

namespace Atlas.SecurityPlatform.Tests.Services.AiPlatform;

public sealed class DatabaseNl2SqlPlanSerializationTests
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void ClauseList_ShouldSerializeWithCamelCase_ForWorkflowConfig()
    {
        var clauses = new DatabaseNl2SqlClause[]
        {
            new("orderId", "eq", "NO-1", "and")
        };
        var element = JsonSerializer.SerializeToElement(clauses, CamelCase);
        var first = element[0];
        Assert.Equal("orderId", first.GetProperty("field").GetString());
        Assert.Equal("eq", first.GetProperty("op").GetString());
    }
}
