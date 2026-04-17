using Atlas.Application.AiPlatform.Models;
using Atlas.Application.AiPlatform.Validators;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.SecurityPlatform.Tests.Validators;

public sealed class DagWorkflowCreateRequestValidatorTests
{
    private readonly DagWorkflowCreateRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenNameMatchesCozeRuleAndDescriptionExists()
    {
        var request = new DagWorkflowCreateRequest(
            "DemoWorkflow_01",
            "用于验证 Coze 风格新建工作流的最小创建校验。",
            WorkflowMode.Standard);

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("123workflow")]
    [InlineData("工作流")]
    [InlineData("workflow-name")]
    [InlineData("workflow_name_is_far_more_than_thirty_chars")]
    public void Validate_ShouldFail_WhenNameViolatesCozeRule(string name)
    {
        var request = new DagWorkflowCreateRequest(
            name,
            "描述存在",
            WorkflowMode.Standard);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(DagWorkflowCreateRequest.Name));
    }

    [Fact]
    public void Validate_ShouldFail_WhenDescriptionIsMissing()
    {
        var request = new DagWorkflowCreateRequest(
            "WorkflowA",
            string.Empty,
            WorkflowMode.Standard);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(DagWorkflowCreateRequest.Description));
    }

    [Fact]
    public void Validate_ShouldPass_WhenNameLengthIsExactly30()
    {
        var request = new DagWorkflowCreateRequest(
            "a" + new string('b', 29),
            "30 字符边界，应当通过。",
            WorkflowMode.Standard);

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameLengthIsExactly31()
    {
        var request = new DagWorkflowCreateRequest(
            "a" + new string('b', 30),
            "31 字符越界，应当失败。",
            WorkflowMode.Standard);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(DagWorkflowCreateRequest.Name) &&
            error.ErrorCode == "DAG_WORKFLOW_NAME_LENGTH");
    }
}
