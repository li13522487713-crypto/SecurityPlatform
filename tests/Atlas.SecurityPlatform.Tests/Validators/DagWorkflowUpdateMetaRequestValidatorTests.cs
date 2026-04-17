using Atlas.Application.AiPlatform.Models;
using Atlas.Application.AiPlatform.Validators;

namespace Atlas.SecurityPlatform.Tests.Validators;

public sealed class DagWorkflowUpdateMetaRequestValidatorTests
{
    private readonly DagWorkflowUpdateMetaRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenNameMatchesCozeRule()
    {
        var request = new DagWorkflowUpdateMetaRequest(
            "DemoWorkflow_01",
            "更新后的工作流描述。");

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldPass_WhenDescriptionIsNull()
    {
        var request = new DagWorkflowUpdateMetaRequest(
            "DemoWorkflow_01",
            null);

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
        var request = new DagWorkflowUpdateMetaRequest(
            name,
            "更新后的工作流描述。");

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(DagWorkflowUpdateMetaRequest.Name));
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new DagWorkflowUpdateMetaRequest(
            string.Empty,
            "描述");

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(DagWorkflowUpdateMetaRequest.Name));
    }

    [Fact]
    public void Validate_ShouldPass_WhenNameLengthIsExactly30()
    {
        var request = new DagWorkflowUpdateMetaRequest(
            "a" + new string('b', 29),
            "30 字符边界，应当通过。");

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameLengthIsExactly31()
    {
        var request = new DagWorkflowUpdateMetaRequest(
            "a" + new string('b', 30),
            "31 字符越界，应当失败。");

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(DagWorkflowUpdateMetaRequest.Name) &&
            error.ErrorCode == "DAG_WORKFLOW_NAME_LENGTH");
    }
}
