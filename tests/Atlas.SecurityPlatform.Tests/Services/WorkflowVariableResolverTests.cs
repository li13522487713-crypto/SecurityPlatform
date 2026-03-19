using System.Text.Json;
using Atlas.Infrastructure.Services.WorkflowEngine;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class WorkflowVariableResolverTests
{
    [Fact]
    public void RenderTemplate_ShouldResolveNestedObjectAndArrayPath()
    {
        var variables = VariableResolver.ParseVariableDictionary("""
            {
              "user": {
                "name": "atlas",
                "scores": [88, 92]
              }
            }
            """);

        var rendered = VariableResolver.RenderTemplate(
            "name={{user.name}},firstScore={{user.scores[0]}}",
            variables);

        Assert.Equal("name=atlas,firstScore=88", rendered);
    }

    [Fact]
    public void ParseLiteral_ShouldPreservePrimitiveAndStructuredTypes()
    {
        var boolValue = VariableResolver.ParseLiteral("true");
        var numberValue = VariableResolver.ParseLiteral("123");
        var objectValue = VariableResolver.ParseLiteral("""{"name":"atlas","enabled":true}""");
        var arrayValue = VariableResolver.ParseLiteral("""[1,2,3]""");

        Assert.Equal(JsonValueKind.True, boolValue.ValueKind);
        Assert.Equal(JsonValueKind.Number, numberValue.ValueKind);
        Assert.Equal(JsonValueKind.Object, objectValue.ValueKind);
        Assert.Equal(JsonValueKind.Array, arrayValue.ValueKind);
    }

    [Fact]
    public void EvaluateCondition_ShouldSupportAndOrAndNumericComparison()
    {
        var variables = VariableResolver.ParseVariableDictionary("""
            {
              "riskScore": 82,
              "severity": "high",
              "approved": false
            }
            """);

        Assert.True(VariableResolver.EvaluateCondition("{{riskScore}} >= 80 && {{severity}} == \"high\"", variables));
        Assert.True(VariableResolver.EvaluateCondition("{{riskScore}} >= 80 && {{approved}} == false", variables));
        Assert.True(VariableResolver.EvaluateCondition("{{approved}} == false || {{severity}} == low", variables));
        Assert.False(VariableResolver.EvaluateCondition("{{riskScore}} < 60", variables));

        var vipVariables = VariableResolver.ParseVariableDictionary("""
            {
              "risk": 88,
              "isVip": true
            }
            """);
        Assert.True(VariableResolver.EvaluateCondition("{{risk}} >= 80 && {{isVip}} == true", vipVariables));
    }

    [Fact]
    public void ParseVariableDictionary_ShouldReturnCaseInsensitiveDictionary()
    {
        var variables = VariableResolver.ParseVariableDictionary("""
            {
              "Tenant": "Atlas",
              "Enabled": true
            }
            """);

        Assert.True(variables.ContainsKey("tenant"));
        Assert.True(variables.ContainsKey("ENABLED"));
        Assert.Equal("Atlas", variables["tenant"].GetString());
        Assert.True(variables["enabled"].GetBoolean());
    }

    [Fact]
    public void GetConfigString_ShouldDecodeHtmlEscapedOperators()
    {
        var config = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["condition"] = JsonSerializer.SerializeToElement("{{risk}} &gt;= 80 &amp;&amp; {{isVip}} == true")
        };

        var decoded = VariableResolver.GetConfigString(config, "condition");

        Assert.Equal("{{risk}} >= 80 && {{isVip}} == true", decoded);
    }
}
