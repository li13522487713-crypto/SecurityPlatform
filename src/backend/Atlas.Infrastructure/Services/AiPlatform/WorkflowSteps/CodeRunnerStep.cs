using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;

namespace Atlas.Infrastructure.Services.AiPlatform.WorkflowSteps;

public sealed class CodeRunnerStep : StepBodyAsync
{
    private readonly ICodeExecutionService _codeExecutionService;
    private readonly IAuditWriter _auditWriter;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public CodeRunnerStep(
        ICodeExecutionService codeExecutionService,
        IAuditWriter auditWriter,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _codeExecutionService = codeExecutionService;
        _auditWriter = auditWriter;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    public string Expression { get; set; } = string.Empty;
    public string OutputKey { get; set; } = "codeResult";
    public int? TimeoutSeconds { get; set; }

    public override async Task<ExecutionResult> RunAsync(Atlas.WorkflowCore.Abstractions.IStepExecutionContext context)
    {
        var data = WorkflowStepDataHelper.EnsureDataDictionary(context);
        var resolvedExpression = WorkflowStepDataHelper.ResolveTemplate(Expression, data);
        if (string.IsNullOrWhiteSpace(resolvedExpression))
        {
            data[OutputKey] = null;
            return ExecutionResult.Next();
        }

        var request = new CodeExecutionRequest(
            resolvedExpression,
            data,
            TimeoutSeconds ?? 0);
        var result = await _codeExecutionService.ExecuteAsync(request, context.CancellationToken);
        data[OutputKey] = result.Success ? result.Output : result.ErrorMessage;
        data[$"{OutputKey}Meta"] = new
        {
            result.Success,
            result.TimedOut,
            result.DurationMs,
            result.ErrorMessage
        };

        await WriteCodeExecutionAuditAsync(resolvedExpression, result, context.CancellationToken);

        return ExecutionResult.Next();
    }

    private async Task WriteCodeExecutionAuditAsync(
        string code,
        CodeExecutionResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantProvider.GetTenantId();
            var user = _currentUserAccessor.GetCurrentUser();
            var userId = user?.UserId.ToString() ?? "system";
            var status = result.Success ? "Success" : (result.TimedOut ? "Timeout" : "Error");
            var targetJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                codeLengthChars = code.Length,
                durationMs = result.DurationMs,
                timedOut = result.TimedOut,
                error = result.ErrorMessage
            });
            var auditRecord = new AuditRecord(
                tenantId,
                userId,
                "code_execution",
                status,
                targetJson,
                null,
                null);
            await _auditWriter.WriteAsync(auditRecord, cancellationToken);
        }
        catch
        {
            // 审计写入失败不应影响主流程
        }
    }
}
