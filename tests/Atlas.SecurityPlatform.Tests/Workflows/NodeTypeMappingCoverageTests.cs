using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Services.WorkflowEngine;

namespace Atlas.SecurityPlatform.Tests.Workflows;

/// <summary>
/// M6：节点对照覆盖与变量解析稳定性。
///
/// - 校验 <see cref="WorkflowNodeType"/> 枚举的每一项都能在 Coze 兼容层 ToCozeNodeTypeCode 转成数字字符串；
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
            var code = ToCozeNodeTypeCodeViaReflection(key);
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
    /// 反射调用 <see cref="Atlas.Presentation.Shared.Controllers.Ai.CozeWorkflowCompatControllerBase"/> 中的私有
    /// <c>ToCozeNodeTypeCode</c>，避免把这块测试范围"绑死"在一个特定 controller 子类的实例上。
    /// </summary>
    private static string ToCozeNodeTypeCodeViaReflection(string nodeKey)
    {
        var type = Type.GetType(
            "Atlas.Presentation.Shared.Controllers.Ai.CozeWorkflowCompatControllerBase, Atlas.Presentation.Shared",
            throwOnError: true)!;
        var method = type.GetMethod(
            "ToCozeNodeTypeCode",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = (string)method.Invoke(null, new object[] { nodeKey })!;
        return result;
    }

    /// <summary>
    /// 防御性测试：把 ToCozeNodeTypeCode 的反射查找失败也作为可读断言失败暴露出来，
    /// 避免后续 controller 重构时"测试静默通过"。
    /// </summary>
    [Fact]
    public void ToCozeNodeTypeCode_ShouldBeAccessibleViaReflection()
    {
        var entryCode = ToCozeNodeTypeCodeViaReflection("Entry");
        Assert.False(string.IsNullOrWhiteSpace(entryCode));
        Assert.NotEqual("Entry", entryCode); // 应当是数字字符串
    }
}
