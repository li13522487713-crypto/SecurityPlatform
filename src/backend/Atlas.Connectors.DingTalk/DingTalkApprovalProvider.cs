using System.Globalization;
using System.Text.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Models;
using Atlas.Connectors.DingTalk.Internal;

namespace Atlas.Connectors.DingTalk;

/// <summary>
/// 钉钉审批 Provider：
/// - 模板：钉钉无单独"模板详情"开放接口；processCode 由企业管理员维护，这里仅返回最小信息；
/// - 创建实例：v1.0 /workflow/processInstances（带 Bearer）；
/// - 状态查询：v1.0 /workflow/processInstances/{processInstanceId}；
/// - 列出近期实例：v1.0 /workflow/processInstances/userIds/{userId}/?startTime=...&processCode=...；
/// - 钉钉无三方审批同步原生通用接口，SyncThirdPartyInstanceAsync 返回 false（建议走消息卡片同步）。
/// </summary>
public sealed class DingTalkApprovalProvider : IExternalApprovalProvider
{
    private readonly DingTalkApiClient _api;

    public DingTalkApprovalProvider(DingTalkApiClient api)
    {
        _api = api;
    }

    public string ProviderType => DingTalkConnectorMarker.ProviderType;

    public Task<ExternalApprovalTemplate> GetTemplateAsync(ConnectorContext context, string externalTemplateId, CancellationToken cancellationToken)
    {
        // 钉钉新版审批没有"取模板控件 schema"的开放接口；返回壳实体，前端字段映射设计器需手工配置。
        var template = new ExternalApprovalTemplate
        {
            ProviderType = ProviderType,
            ProviderTenantId = string.Empty,
            ExternalTemplateId = externalTemplateId,
            Name = externalTemplateId,
            Description = "DingTalk processCode without published controls schema; configure mapping manually.",
            Controls = Array.Empty<ExternalApprovalTemplateControl>(),
            RawJson = "{}",
        };
        return Task.FromResult(template);
    }

    public Task<IReadOnlyList<ExternalApprovalTemplate>> ListTemplatesAsync(ConnectorContext context, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ExternalApprovalTemplate>>(Array.Empty<ExternalApprovalTemplate>());

    public async Task<ExternalApprovalInstanceRef> SubmitApprovalAsync(ConnectorContext context, ExternalApprovalSubmission submission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);
        var formComponents = submission.Fields.Select(kv => new
        {
            name = kv.Key,
            value = ParseValueToString(kv.Value),
        }).ToArray();

        var body = new
        {
            processCode = submission.ExternalTemplateId,
            originatorUserId = submission.ApplicantExternalUserId,
            deptId = (object?)null,
            formComponentValues = formComponents,
            approvers = submission.ApproverExternalUserIds is null
                ? null
                : new[]
                {
                    new
                    {
                        actionType = "AND",
                        userIds = submission.ApproverExternalUserIds,
                    },
                },
            ccList = submission.CcExternalUserIds,
            ccPosition = "FINISH",
        };
        var runtime = DingTalkApiClient.ResolveRuntime(context);
        var resp = await _api.SendV1PostJsonAsync<object, DingTalkCreateProcessInstanceResponse>(context, "/v1.0/workflow/processInstances", body, cancellationToken).ConfigureAwait(false);

        return new ExternalApprovalInstanceRef
        {
            ProviderType = ProviderType,
            ProviderTenantId = runtime.CorpId ?? runtime.AppKey,
            ExternalInstanceId = resp.InstanceId ?? string.Empty,
            ExternalTemplateId = submission.ExternalTemplateId,
            Status = ExternalApprovalStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            RawJson = JsonSerializer.Serialize(resp),
        };
    }

    public async Task<ExternalApprovalInstanceRef?> GetInstanceAsync(ConnectorContext context, string externalInstanceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalInstanceId))
        {
            return null;
        }
        var path = $"/v1.0/workflow/processInstances?processInstanceId={Uri.EscapeDataString(externalInstanceId)}";
        try
        {
            var runtime = DingTalkApiClient.ResolveRuntime(context);
            var resp = await _api.SendV1GetAsync<DingTalkProcessInstanceDetailResponse>(context, path, cancellationToken).ConfigureAwait(false);
            if (resp.Result is null)
            {
                return null;
            }
            return new ExternalApprovalInstanceRef
            {
                ProviderType = ProviderType,
                ProviderTenantId = runtime.CorpId ?? runtime.AppKey,
                ExternalInstanceId = externalInstanceId,
                ExternalTemplateId = resp.Result.ProcessCode ?? string.Empty,
                Status = MapStatus(resp.Result.Status, resp.Result.Result),
                CreatedAt = ParseTime(resp.Result.CreateTime) ?? DateTimeOffset.UtcNow,
                RawJson = JsonSerializer.Serialize(resp),
            };
        }
        catch (ConnectorException ex) when (string.Equals(ex.Code, ConnectorErrorCodes.ApprovalInstanceNotFound, StringComparison.Ordinal))
        {
            return null;
        }
    }

    public async Task<ExternalApprovalInstanceIdPage> ListRecentInstanceIdsAsync(ConnectorContext context, ExternalApprovalInstanceIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrEmpty(query.TemplateId))
        {
            return ExternalApprovalInstanceIdPage.Empty;
        }
        var body = new
        {
            processCode = query.TemplateId,
            startTime = query.StartTime.ToUnixTimeMilliseconds(),
            endTime = query.EndTime.ToUnixTimeMilliseconds(),
            nextToken = string.IsNullOrEmpty(query.Cursor) ? 0 : long.Parse(query.Cursor, CultureInfo.InvariantCulture),
            maxResults = Math.Clamp(query.Size, 1, 20),
        };
        var resp = await _api.SendV1PostJsonAsync<object, DingTalkListInstanceIdsResponse>(context, "/v1.0/workflow/processes/instanceIds/query", body, cancellationToken).ConfigureAwait(false);
        return new ExternalApprovalInstanceIdPage
        {
            InstanceIds = resp.Result?.List ?? Array.Empty<string>(),
            NextCursor = resp.Result is not null && resp.Result.NextToken > 0
                ? resp.Result.NextToken.ToString(CultureInfo.InvariantCulture)
                : null,
        };
    }

    public Task<bool> SyncThirdPartyInstanceAsync(ConnectorContext context, ExternalThirdPartyInstancePatch patch, CancellationToken cancellationToken)
    {
        // 钉钉无原生三方审批同步通用接口；模式 B 推荐通过消息卡片 + 链接跳回本地实现状态可视化。
        return Task.FromResult(false);
    }

    private static ExternalApprovalStatus MapStatus(string? status, string? result) => (status?.ToUpperInvariant(), result?.ToUpperInvariant()) switch
    {
        ("RUNNING", _) => ExternalApprovalStatus.Pending,
        ("NEW", _) => ExternalApprovalStatus.Pending,
        ("COMPLETED", "AGREE") => ExternalApprovalStatus.Approved,
        ("COMPLETED", "REFUSE") => ExternalApprovalStatus.Rejected,
        ("TERMINATED", _) => ExternalApprovalStatus.Canceled,
        ("CANCELED", _) => ExternalApprovalStatus.Canceled,
        _ => ExternalApprovalStatus.Unknown,
    };

    private static DateTimeOffset? ParseTime(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso))
        {
            return null;
        }
        return DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto) ? dto : null;
    }

    private static string ParseValueToString(ExternalApprovalFieldValue value)
    {
        try
        {
            using var doc = JsonDocument.Parse(value.RawJson);
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.String => doc.RootElement.GetString() ?? string.Empty,
                JsonValueKind.Number => doc.RootElement.GetRawText(),
                JsonValueKind.True or JsonValueKind.False => doc.RootElement.GetRawText(),
                _ => doc.RootElement.GetRawText(),
            };
        }
        catch (JsonException)
        {
            return value.RawJson;
        }
    }
}

internal sealed class DingTalkListInstanceIdsResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("result")]
    public DingTalkListInstanceIdsResult? Result { get; set; }
}

internal sealed class DingTalkListInstanceIdsResult
{
    [System.Text.Json.Serialization.JsonPropertyName("list")]
    public string[]? List { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("nextToken")]
    public long NextToken { get; set; }
}
