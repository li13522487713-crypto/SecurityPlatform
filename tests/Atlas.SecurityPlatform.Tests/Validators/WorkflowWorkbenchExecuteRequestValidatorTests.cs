using Atlas.Application.AiPlatform.Models;
using Atlas.Application.AiPlatform.Validators;

namespace Atlas.SecurityPlatform.Tests.Validators;

public sealed class WorkflowWorkbenchExecuteRequestValidatorTests
{
    private readonly WorkflowWorkbenchExecuteRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenIncidentAndSourceAreSupported()
    {
        var request = new WorkflowWorkbenchExecuteRequest(
            "主机检测到可疑 PowerShell 横向移动行为，需要立即安排隔离、排查和取证。",
            "draft");

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenIncidentIsEmpty()
    {
        var request = new WorkflowWorkbenchExecuteRequest(string.Empty, "draft");

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(WorkflowWorkbenchExecuteRequest.Incident));
    }

    [Fact]
    public void Validate_ShouldFail_WhenSourceIsUnsupported()
    {
        var request = new WorkflowWorkbenchExecuteRequest("incident", "runtime");

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(WorkflowWorkbenchExecuteRequest.Source));
    }
}
