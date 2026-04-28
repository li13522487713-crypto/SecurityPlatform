using System.Text.Json;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Connectors.Core.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.ExternalConnectors.Services;

/// <summary>
/// 外部协同审批 fan-out handler：作为 <see cref="IApprovalEventHandler"/> 的一个实现，
/// 把本地审批的 Started/Completed/Rejected/Canceled 事件按 IntegrationMode 路由到外部 provider。
///
/// 与 ApprovalEventPublisher 解耦：本类仅消费已发布的事件，转交给 IExternalApprovalDispatchService。
/// 这样模式 A/B/C 的差异沉淀在 dispatch service，handler 只负责做事件 → 调用映射。
/// </summary>
public sealed class ExternalApprovalFanoutHandler : IApprovalEventHandler
{
    private readonly IExternalApprovalDispatchService _dispatchService;
    private readonly ILogger<ExternalApprovalFanoutHandler> _logger;

    public ExternalApprovalFanoutHandler(
        IExternalApprovalDispatchService dispatchService,
        ILogger<ExternalApprovalFanoutHandler> logger)
    {
        _dispatchService = dispatchService;
        _logger = logger;
    }

    public async Task OnInstanceStartedAsync(ApprovalInstanceEvent e, CancellationToken cancellationToken)
    {
        try
        {
            var payload = BuildSubmissionPayload(e);
            var result = await _dispatchService.OnInstanceStartedAsync(e.InstanceId, e.DefinitionId, payload, cancellationToken).ConfigureAwait(false);
            if (result.Pushed)
            {
                _logger.LogInformation("External approval pushed: instance={InstanceId} → provider={ProviderType}/{ProviderId}, externalId={ExternalInstanceId}.",
                    e.InstanceId, result.ProviderType, result.ProviderId, result.ExternalInstanceId);
            }
            else
            {
                _logger.LogDebug("External approval not pushed: instance={InstanceId}, reason={Reason}.", e.InstanceId, result.Reason);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "ExternalApprovalFanoutHandler.OnInstanceStarted failed for instance {InstanceId}.", e.InstanceId);
        }
    }

    public Task OnInstanceCompletedAsync(ApprovalInstanceEvent e, CancellationToken cancellationToken)
        => SyncStatusAsync(e, ExternalApprovalStatus.Approved, comment: null, cancellationToken);

    public Task OnInstanceRejectedAsync(ApprovalInstanceEvent e, CancellationToken cancellationToken)
        => SyncStatusAsync(e, ExternalApprovalStatus.Rejected, comment: null, cancellationToken);

    public Task OnInstanceCanceledAsync(ApprovalInstanceEvent e, CancellationToken cancellationToken)
        => SyncStatusAsync(e, ExternalApprovalStatus.Canceled, comment: null, cancellationToken);

    public Task OnTaskApprovedAsync(ApprovalTaskEvent e, CancellationToken cancellationToken)
        => SyncTaskAsync(e, ExternalApprovalStatus.Approved, cancellationToken);

    public Task OnTaskRejectedAsync(ApprovalTaskEvent e, CancellationToken cancellationToken)
        => SyncTaskAsync(e, ExternalApprovalStatus.Rejected, cancellationToken);

    private async Task SyncStatusAsync(ApprovalInstanceEvent e, ExternalApprovalStatus newStatus, string? comment, CancellationToken cancellationToken)
    {
        try
        {
            await _dispatchService.OnInstanceStatusChangedAsync(e.InstanceId, newStatus, comment, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "ExternalApprovalFanoutHandler status sync failed: instance={InstanceId}, target={Status}.", e.InstanceId, newStatus);
        }
    }

    private async Task SyncTaskAsync(ApprovalTaskEvent e, ExternalApprovalStatus taskStatus, CancellationToken cancellationToken)
    {
        try
        {
            // 任务级状态变化也复用 dispatch service 的实例同步入口；任务粒度的细分推送由 SyncThirdPartyInstanceAsync 内部用 TaskUpdates 表达。
            await _dispatchService.OnInstanceStatusChangedAsync(e.InstanceId, MapTaskToInstance(taskStatus), e.Comment, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "ExternalApprovalFanoutHandler task sync failed: task={TaskId}, target={Status}.", e.TaskId, taskStatus);
        }
    }

    private static ExternalApprovalStatus MapTaskToInstance(ExternalApprovalStatus task) => task;

    private static ExternalApprovalSubmission BuildSubmissionPayload(ApprovalInstanceEvent e)
    {
        var fields = new Dictionary<string, ExternalApprovalFieldValue>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(e.DataJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(e.DataJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        fields[prop.Name] = new ExternalApprovalFieldValue
                        {
                            ValueType = ResolveValueType(prop.Value.ValueKind),
                            RawJson = prop.Value.GetRawText(),
                        };
                    }
                }
            }
            catch (JsonException)
            {
                // 非 JSON 数据：留空 fields，让 provider 的 SubmitApproval 处理空体（多数 provider 允许 apply_data.contents = []）
            }
        }

        return new ExternalApprovalSubmission
        {
            ApplicantExternalUserId = e.ActorUserId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ExternalTemplateId = string.Empty, // dispatch service 会从 mapping 中补齐
            BusinessKey = e.BusinessKey,
            Fields = fields,
            ApproverExternalUserIds = null,
            CcExternalUserIds = null,
            SummaryText = null,
        };
    }

    private static string ResolveValueType(JsonValueKind kind) => kind switch
    {
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Array => "array",
        JsonValueKind.Object => "object",
        _ => "string",
    };
}
