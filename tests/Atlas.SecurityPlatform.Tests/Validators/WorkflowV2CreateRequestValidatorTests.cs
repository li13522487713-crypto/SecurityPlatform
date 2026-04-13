using Atlas.Application.AiPlatform.Models;
using Atlas.Application.AiPlatform.Validators;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.SecurityPlatform.Tests.Validators;

public sealed class WorkflowV2CreateRequestValidatorTests
{
    private readonly WorkflowV2CreateRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenNameMatchesCozeRuleAndDescriptionExists()
    {
        var request = new WorkflowV2CreateRequest(
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
        var request = new WorkflowV2CreateRequest(
            name,
            "描述存在",
            WorkflowMode.Standard);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(WorkflowV2CreateRequest.Name));
    }

    [Fact]
    public void Validate_ShouldFail_WhenDescriptionIsMissing()
    {
        var request = new WorkflowV2CreateRequest(
            "WorkflowA",
            string.Empty,
            WorkflowMode.Standard);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(WorkflowV2CreateRequest.Description));
    }
}
