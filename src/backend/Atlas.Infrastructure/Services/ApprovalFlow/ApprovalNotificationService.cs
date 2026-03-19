using System.Text.Json;
using Atlas.Application.Abstractions;
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
    private readonly IApprovalFlowRepository _flowRepository;
    private readonly IUserAccountRepository? _userAccountRepository;
    private readonly IApprovalNotificationRetryRepository? _retryRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public ApprovalNotificationService(
        IApprovalNotificationTemplateRepository templateRepository,
        IApprovalInboxMessageRepository inboxRepository,
        IEnumerable<IApprovalNotificationSender> senders,
        IApprovalFlowRepository flowRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUserAccountRepository? userAccountRepository = null,
        IApprovalNotificationRetryRepository? retryRepository = null)
    {
        _templateRepository = templateRepository;
        _inboxRepository = inboxRepository;
        _senders = senders;
        _flowRepository = flowRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _userAccountRepository = userAccountRepository;
        _retryRepository = retryRepository;
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

        var flowTemplates = await _templateRepository.GetByFlowAsync(
            tenantId,
            instance.DefinitionId,
            cancellationToken);
        var systemTemplates = await _templateRepository.GetByFlowAsync(
            tenantId,
            0,
            cancellationToken);

        var flowTemplateMap = flowTemplates
            .Where(x => x.IsEnabled && x.EventType == eventType)
            .GroupBy(x => x.Channel)
            .ToDictionary(x => x.Key, x => x.First());
        var systemTemplateMap = systemTemplates
            .Where(x => x.IsEnabled && x.EventType == eventType)
            .GroupBy(x => x.Channel)
            .ToDictionary(x => x.Key, x => x.First());

        // 预加载模板变量上下文（避免在循环内查询数据库）
        var variableContext = await BuildVariableContextAsync(tenantId, instance, task, cancellationToken);

        // 获取所有通知渠道的模板
        var channels = Enum.GetValues<ApprovalNotificationChannel>();
        var messages = new List<ApprovalInboxMessage>();

        foreach (var channel in channels)
        {
            // 先查找流程级模板，如果没有则查找系统级模板
            flowTemplateMap.TryGetValue(channel, out var template);
            template ??= systemTemplateMap.GetValueOrDefault(channel);

            if (template == null || !template.IsEnabled)
            {
                continue;
            }

            // 替换模板变量
            var title = ReplaceTemplateVariables(template.TitleTemplate, variableContext);
            var content = ReplaceTemplateVariables(template.ContentTemplate, variableContext);

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
                            // 发送失败：写入重试表由后台 Job 兜底重试
                            if (_retryRepository != null)
                            {
                                var retryRecord = new ApprovalNotificationRetry(
                                    tenantId,
                                    recipientUserId,
                                    channel,
                                    title,
                                    content,
                                    _idGeneratorAccessor.NextId());
                                await _retryRepository.AddAsync(retryRecord, cancellationToken);
                            }
                            // 发送失败不影响流程主链路
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
    /// 构建模板变量上下文（在循环外一次性加载所有需要的数据）
    /// </summary>
    private async Task<Dictionary<string, string>> BuildVariableContextAsync(
        TenantId tenantId,
        ApprovalProcessInstance instance,
        ApprovalTask? task,
        CancellationToken cancellationToken)
    {
        var context = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 基础实例变量
        context["{InstanceId}"] = instance.Id.ToString();
        context["{BusinessKey}"] = instance.BusinessKey;
        context["{InitiatorUserId}"] = instance.InitiatorUserId.ToString();
        context["{Status}"] = instance.Status.ToString();

        // 任务相关变量
        if (task != null)
        {
            context["{TaskId}"] = task.Id.ToString();
            context["{TaskTitle}"] = task.Title;
            context["{NodeId}"] = task.NodeId;
            context["{AssigneeType}"] = task.AssigneeType.ToString();
        }

        // 当前时间
        context["{CurrentTime}"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        // 流程名称（查询流程定义）
        try
        {
            var flowDef = await _flowRepository.GetByIdAsync(tenantId, instance.DefinitionId, cancellationToken);
            context["{FlowName}"] = flowDef?.Name ?? string.Empty;
        }
        catch
        {
            context["{FlowName}"] = string.Empty;
        }

        // 发起人姓名（查询用户信息）
        if (_userAccountRepository != null)
        {
            try
            {
                var initiator = await _userAccountRepository.FindByIdAsync(tenantId, instance.InitiatorUserId, cancellationToken);
                context["{InitiatorName}"] = initiator?.DisplayName ?? instance.InitiatorUserId.ToString();
            }
            catch
            {
                context["{InitiatorName}"] = instance.InitiatorUserId.ToString();
            }
        }
        else
        {
            context["{InitiatorName}"] = instance.InitiatorUserId.ToString();
        }

        // 从 DataJson 中提取自定义变量（支持 {DataJson.fieldName} 语法）
        if (!string.IsNullOrEmpty(instance.DataJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(instance.DataJson);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var key = $"{{DataJson.{prop.Name}}}";
                    context[key] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                        JsonValueKind.Number => prop.Value.GetRawText(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        JsonValueKind.Null => string.Empty,
                        _ => prop.Value.GetRawText()
                    };
                }
            }
            catch
            {
                // DataJson 解析失败不影响通知发送
            }
        }

        return context;
    }

    /// <summary>
    /// 替换模板变量（使用预构建的上下文字典）
    /// </summary>
    private static string ReplaceTemplateVariables(
        string template,
        Dictionary<string, string> variableContext)
    {
        var result = template;

        foreach (var (key, value) in variableContext)
        {
            result = result.Replace(key, value);
        }

        return result;
    }
}
