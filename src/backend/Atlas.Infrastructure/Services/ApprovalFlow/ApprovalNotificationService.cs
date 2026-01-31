using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 审批消息通知服务实现
/// </summary>
public sealed class ApprovalNotificationService : IApprovalNotificationService
{
    private readonly IApprovalNotificationTemplateRepository _templateRepository;
    private readonly IApprovalInboxMessageRepository _inboxRepository;
    private readonly IEnumerable<IApprovalNotificationSender> _senders;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public ApprovalNotificationService(
        IApprovalNotificationTemplateRepository templateRepository,
        IApprovalInboxMessageRepository inboxRepository,
        IEnumerable<IApprovalNotificationSender> senders,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _templateRepository = templateRepository;
        _inboxRepository = inboxRepository;
        _senders = senders;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task NotifyAsync(
        TenantId tenantId,
        ApprovalNotificationEventType eventType,
        ApprovalProcessInstance instance,
        ApprovalTask? task,
        IReadOnlyList<long> recipientUserIds,
        CancellationToken cancellationToken)
    {
        if (recipientUserIds.Count == 0)
        {
            return;
        }

        // 获取所有通知渠道的模板
        var channels = Enum.GetValues<ApprovalNotificationChannel>();
        var messages = new List<ApprovalInboxMessage>();

        foreach (var channel in channels)
        {
            // 先查找流程级模板，如果没有则查找系统级模板
            var template = await _templateRepository.GetByFlowAndEventAsync(
                tenantId, instance.DefinitionId, eventType, channel, cancellationToken)
                ?? await _templateRepository.GetSystemTemplateAsync(tenantId, eventType, channel, cancellationToken);

            if (template == null || !template.IsEnabled)
            {
                continue;
            }

            // 替换模板变量
            var title = ReplaceTemplateVariables(template.TitleTemplate, instance, task);
            var content = ReplaceTemplateVariables(template.ContentTemplate, instance, task);

            // 为每个收件人发送通知
            foreach (var recipientUserId in recipientUserIds)
            {
                // 站内信：直接落库
                if (channel == ApprovalNotificationChannel.Inbox)
                {
                    var inboxMessage = new ApprovalInboxMessage(
                        tenantId,
                        recipientUserId,
                        instance.Id,
                        task?.Id,
                        eventType,
                        title,
                        content,
                        _idGeneratorAccessor.NextId());
                    messages.Add(inboxMessage);
                }
                else
                {
                    // 外部渠道：调用发送适配器
                    var sender = _senders.FirstOrDefault(s => s.SupportedChannel == channel);
                    if (sender != null)
                    {
                        try
                        {
                            await sender.SendAsync(tenantId, recipientUserId, title, content, cancellationToken);
                        }
                        catch
                        {
                            // 发送失败不影响流程，记录日志即可
                            // TODO: 记录发送失败日志
                        }
                    }
                }
            }
        }

        // 批量保存站内信
        if (messages.Count > 0)
        {
            await _inboxRepository.AddRangeAsync(messages, cancellationToken);
        }
    }

    /// <summary>
    /// 替换模板变量
    /// </summary>
    private static string ReplaceTemplateVariables(
        string template,
        ApprovalProcessInstance instance,
        ApprovalTask? task)
    {
        var result = template;

        // 基础变量
        result = result.Replace("{InstanceId}", instance.Id.ToString());
        result = result.Replace("{BusinessKey}", instance.BusinessKey);
        result = result.Replace("{InitiatorUserId}", instance.InitiatorUserId.ToString());
        result = result.Replace("{Status}", instance.Status.ToString());

        // 任务相关变量
        if (task != null)
        {
            result = result.Replace("{TaskId}", task.Id.ToString());
            result = result.Replace("{TaskTitle}", task.Title);
            result = result.Replace("{NodeId}", task.NodeId);
            result = result.Replace("{AssigneeType}", task.AssigneeType.ToString());
        }

        // TODO: 扩展更多变量替换
        // - {FlowName} - 流程名称（需要查询流程定义）
        // - {InitiatorName} - 发起人姓名（需要查询用户信息）
        // - {CurrentTime} - 当前时间
        // - 自定义变量（从 DataJson 中提取）

        return result;
    }
}




