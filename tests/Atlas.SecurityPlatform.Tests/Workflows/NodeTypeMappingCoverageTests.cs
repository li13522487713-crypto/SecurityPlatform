using System.Globalization;
using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Services.WorkflowEngine;
using Atlas.Presentation.Shared.Controllers.Ai;

namespace Atlas.SecurityPlatform.Tests.Workflows;

/// <summary>
/// M6：节点对照覆盖与变量解析稳定性。
///
/// - 校验 <see cref="WorkflowNodeType"/> 枚举的每一项都能在 Coze gateway helper 的 ToCozeNodeTypeCode 转成数字字符串；
///   一旦新增节点忘记走对应路径，本测试会立刻挂掉。
/// - 校验 <see cref="VariableResolver.ParseVariableDictionary"/> 在 trace / debug 上下文下的兜底语义。
/// </summary>
public sealed class NodeTypeMappingCoverageTests
{
    [Fact]
    public void WorkflowNodeType_ShouldMapEveryEnumValueToNumericCozeCode()
    {
        var enumValues = Enum.GetValues<WorkflowNodeType>();
        Assert.NotEmpty(enumValues);

        foreach (var nodeType in enumValues)
        {
            var key = nodeType.ToString();
            var code = CozeCompatGatewaySupport.ToCozeNodeTypeCode(key);
            Assert.False(string.IsNullOrWhiteSpace(code), $"{key} 节点 ToCozeNodeTypeCode 不应返回空字符串");
            Assert.True(int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
                $"{key} 节点应映射为数字字符串（与上游 StandardNodeType 字符串数字 ID 对齐），但得到 {code}");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-json")]
    [InlineData("123")]
    [InlineData("[1, 2]")]
    public void VariableResolver_ParseVariableDictionary_ShouldFallbackOnInvalidInput(string? input)
    {
        var dict = VariableResolver.ParseVariableDictionary(input);

        Assert.NotNull(dict);
        Assert.Empty(dict);
    }

    [Fact]
    public void VariableResolver_ParseVariableDictionary_ShouldExposeStringAndNumberFields()
    {
        var json = "{\"prompt\":\"hello\",\"count\":42}";

        var dict = VariableResolver.ParseVariableDictionary(json);

        Assert.NotNull(dict);
        Assert.Equal(2, dict.Count);
        Assert.Equal("hello", dict["prompt"].GetString());
        Assert.Equal(42, dict["count"].GetInt32());
    }

    /// <summary>
    /// 防御性测试：ToCozeNodeTypeCode 由共享 helper 承担，避免 controller 删除后测试失效。
    /// </summary>
    [Fact]
    public void ToCozeNodeTypeCode_ShouldBeAccessibleViaReflection()
    {
        var entryCode = CozeCompatGatewaySupport.ToCozeNodeTypeCode("Entry");
        Assert.False(string.IsNullOrWhiteSpace(entryCode));
        Assert.NotEqual("Entry", entryCode); // 应当是数字字符串
    }
}
