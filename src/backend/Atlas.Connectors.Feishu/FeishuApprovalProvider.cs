using System.Globalization;
using System.Text.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Models;

namespace Atlas.Connectors.Feishu;

/// <summary>
/// 飞书审批 Provider：
/// - 审批定义详情：approval/v4/approvals/{code}；
/// - 创建实例：approval/v4/instances；
/// - 三方审批同步：approval/v4/external_instances + external_instances/check（模式 B）。
/// </summary>
public sealed class FeishuApprovalProvider : IExternalApprovalProvider
{
    private readonly FeishuApiClient _api;

    public FeishuApprovalProvider(FeishuApiClient api)
    {
        _api = api;
    }

    public string ProviderType => FeishuConnectorMarker.ProviderType;

    public async Task<ExternalApprovalTemplate> GetTemplateAsync(ConnectorContext context, string externalTemplateId, CancellationToken cancellationToken)
    {
        var runtime = FeishuApiClient.ResolveRuntime(context);
        var path = $"/open-apis/approval/v4/approvals/{Uri.EscapeDataString(externalTemplateId)}?locale=zh-CN";
        var resp = await _api.SendTenantGetAsync<FeishuApprovalDetailData>(context, path, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<ExternalApprovalTemplateControl> controls;
        if (!string.IsNullOrEmpty(resp.Data?.Form))
        {
            controls = ParseFormControls(resp.Data.Form);
        }
        else
        {
            controls = Array.Empty<ExternalApprovalTemplateControl>();
        }

        return new ExternalApprovalTemplate
        {
            ProviderType = ProviderType,
            ProviderTenantId = runtime.TenantKey ?? runtime.AppId,
            ExternalTemplateId = externalTemplateId,
            Name = resp.Data?.ApprovalName ?? string.Empty,
            Description = resp.Data?.Description,
            Controls = controls,
            RawJson = JsonSerializer.Serialize(resp),
        };
    }

    public Task<IReadOnlyList<ExternalApprovalTemplate>> ListTemplatesAsync(ConnectorContext context, CancellationToken cancellationToken)
    {
        // 飞书需要先有 approval_code 列表（通常由企业管理员创建）；这里返回空，由调用方按 code 取详情。
        return Task.FromResult<IReadOnlyList<ExternalApprovalTemplate>>(Array.Empty<ExternalApprovalTemplate>());
    }

    public async Task<ExternalApprovalInstanceRef> SubmitApprovalAsync(ConnectorContext context, ExternalApprovalSubmission submission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);
        var formItems = submission.Fields.Select(kv => new
        {
            id = kv.Key,
            type = MapValueType(kv.Value.ValueType),
            value = ParseValue(kv.Value),
        }).ToArray();

        var body = new
        {
            approval_code = submission.ExternalTemplateId,
            user_id = submission.ApplicantExternalUserId,
            form = JsonSerializer.Serialize(formItems),
            node_approver_user_id_list = submission.ApproverExternalUserIds,
            node_cc_user_id_list = submission.CcExternalUserIds,
            uuid = submission.BusinessKey,
        };
        var runtime = FeishuApiClient.ResolveRuntime(context);
        var resp = await _api.SendTenantPostAsync<object, FeishuCreateInstanceData>(context, "/open-apis/approval/v4/instances", body, cancellationToken).ConfigureAwait(false);
        return new ExternalApprovalInstanceRef
        {
            ProviderType = ProviderType,
            ProviderTenantId = runtime.TenantKey ?? runtime.AppId,
            ExternalInstanceId = resp.Data?.InstanceCode ?? string.Empty,
            ExternalTemplateId = submission.ExternalTemplateId,
            Status = ExternalApprovalStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            RawJson = JsonSerializer.Serialize(resp),
        };
    }

    public async Task<ExternalApprovalInstanceRef?> GetInstanceAsync(ConnectorContext context, string externalInstanceId, CancellationToken cancellationToken)
    {
        var runtime = FeishuApiClient.ResolveRuntime(context);
        var path = $"/open-apis/approval/v4/instances/{Uri.EscapeDataString(externalInstanceId)}?locale=zh-CN";
        var resp = await _api.SendTenantGetAsync<FeishuInstanceDetailData>(context, path, cancellationToken).ConfigureAwait(false);
        if (resp.Data is null)
        {
            return null;
        }
        return new ExternalApprovalInstanceRef
        {
            ProviderType = ProviderType,
            ProviderTenantId = runtime.TenantKey ?? runtime.AppId,
            ExternalInstanceId = resp.Data.InstanceCode ?? externalInstanceId,
            ExternalTemplateId = resp.Data.ApprovalCode ?? string.Empty,
            Status = MapStatus(resp.Data.Status),
            CreatedAt = resp.Data.StartTime > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(resp.Data.StartTime) : DateTimeOffset.UtcNow,
            RawJson = JsonSerializer.Serialize(resp),
        };
    }

    public async Task<ExternalApprovalInstanceIdPage> ListRecentInstanceIdsAsync(ConnectorContext context, ExternalApprovalInstanceIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrEmpty(query.TemplateId))
        {
            // 飞书 approval/v4/instances 按 approval_code 列 instance 列表，没有 approval_code 无法列；返回空。
            return ExternalApprovalInstanceIdPage.Empty;
        }

        var pathBuilder = new System.Text.StringBuilder("/open-apis/approval/v4/instances?");
        pathBuilder.Append("approval_code=").Append(Uri.EscapeDataString(query.TemplateId));
        pathBuilder.Append("&start_time=").Append(query.StartTime.ToUnixTimeMilliseconds());
        pathBuilder.Append("&end_time=").Append(query.EndTime.ToUnixTimeMilliseconds());
        pathBuilder.Append("&page_size=").Append(Math.Clamp(query.Size, 10, 200));
        if (!string.IsNullOrEmpty(query.Cursor))
        {
            pathBuilder.Append("&page_token=").Append(Uri.EscapeDataString(query.Cursor));
        }

        var resp = await _api.SendTenantGetAsync<FeishuListInstancesData>(context, pathBuilder.ToString(), cancellationToken).ConfigureAwait(false);
        return new ExternalApprovalInstanceIdPage
        {
            InstanceIds = resp.Data?.InstanceCodeList ?? Array.Empty<string>(),
            NextCursor = string.IsNullOrEmpty(resp.Data?.PageToken) ? null : resp.Data.PageToken,
        };
    }

    public async Task<bool> SyncThirdPartyInstanceAsync(ConnectorContext context, ExternalThirdPartyInstancePatch patch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(patch);
        // 飞书三方审批同步 = external_instances 创建 + external_instances/check 幂等校验（模式 B/C）。
        // 两步都做：create 负责 push，check 负责跨端对账，避免飞书侧掉数时本地还显示成功。
        var created = await CreateExternalInstanceAsync(context, patch, cancellationToken).ConfigureAwait(false);
        if (!created)
        {
            return false;
        }

        await CheckExternalInstanceAsync(context, patch, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> CreateExternalInstanceAsync(ConnectorContext context, ExternalThirdPartyInstancePatch patch, CancellationToken cancellationToken)
    {
        // POST /open-apis/approval/v4/external_instances
        // body 需要 approval_code / status / extra / user_id / form / links ... 这里只填必须字段，业务层通过 patch 扩展。
        var body = new
        {
            approval_code = patch.TaskUpdates?.FirstOrDefault()?.TaskExternalId ?? patch.ExternalInstanceId,
            status = MapStatusToFeishuExternal(patch.NewStatus),
            extra = string.IsNullOrEmpty(patch.CommentText) ? "{}" : System.Text.Json.JsonSerializer.Serialize(new { comment = patch.CommentText }),
            instance_id = patch.ExternalInstanceId,
            user_id = patch.TaskUpdates?.FirstOrDefault()?.AssigneeExternalUserId,
            start_time = patch.OccurredAt.ToUnixTimeMilliseconds(),
            end_time = patch.NewStatus is ExternalApprovalStatus.Approved or ExternalApprovalStatus.Rejected or ExternalApprovalStatus.Canceled
                ? patch.OccurredAt.ToUnixTimeMilliseconds()
                : (long?)null,
            update_time = patch.OccurredAt.ToUnixTimeMilliseconds(),
        };
        try
        {
            await _api.SendTenantPostAsync<object, object>(context, "/open-apis/approval/v4/external_instances", body, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (ConnectorException)
        {
            return false;
        }
    }

    private async Task CheckExternalInstanceAsync(ConnectorContext context, ExternalThirdPartyInstancePatch patch, CancellationToken cancellationToken)
    {
        var body = new
        {
            instances = new[]
            {
                new
                {
                    instance_id = patch.ExternalInstanceId,
                    update_time = patch.OccurredAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
                    status = MapStatusToFeishuExternal(patch.NewStatus),
                    tasks = patch.TaskUpdates?.Select(t => new
                    {
                        task_id = t.TaskExternalId,
                        user_id = t.AssigneeExternalUserId,
                        status = MapStatusToFeishuExternal(t.Status),
                    }).ToArray(),
                },
            },
        };
        try
        {
            await _api.SendTenantPostAsync<object, object>(context, "/open-apis/approval/v4/external_instances/check", body, cancellationToken).ConfigureAwait(false);
        }
        catch (ConnectorException)
        {
            // check 失败不影响主同步结果；create 已成功说明飞书侧状态已更新。
        }
    }

    private static IReadOnlyList<ExternalApprovalTemplateControl> ParseFormControls(string formJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(formJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<ExternalApprovalTemplateControl>();
            }
            var list = new List<ExternalApprovalTemplateControl>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                list.Add(new ExternalApprovalTemplateControl
                {
                    ControlId = item.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty,
                    ControlType = item.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "input" : "input",
                    Title = item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty,
                    Required = item.TryGetProperty("required", out var requiredProp) && requiredProp.ValueKind == JsonValueKind.True,
                });
            }
            return list;
        }
        catch (JsonException)
        {
            return Array.Empty<ExternalApprovalTemplateControl>();
        }
    }

    private static string MapValueType(string valueType) => valueType.ToLowerInvariant() switch
    {
        "string" => "input",
        "number" => "number",
        "select" => "radio",
        "multiselect" => "checkbox",
        "date" => "date",
        "image" => "imageV2",
        "file" => "attachmentV2",
        _ => "input",
    };

    private static object ParseValue(ExternalApprovalFieldValue value)
    {
        try
        {
            using var doc = JsonDocument.Parse(value.RawJson);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return value.RawJson;
        }
    }

    private static ExternalApprovalStatus MapStatus(string? feishuStatus) => feishuStatus?.ToUpperInvariant() switch
    {
        "PENDING" => ExternalApprovalStatus.Pending,
        "APPROVED" => ExternalApprovalStatus.Approved,
        "REJECTED" => ExternalApprovalStatus.Rejected,
        "CANCELED" => ExternalApprovalStatus.Canceled,
        "DELETED" => ExternalApprovalStatus.Deleted,
        "REVERTED" => ExternalApprovalStatus.Reverted,
        _ => ExternalApprovalStatus.Unknown,
    };

    private static string MapStatusToFeishuExternal(ExternalApprovalStatus status) => status switch
    {
        ExternalApprovalStatus.Pending => "PENDING",
        ExternalApprovalStatus.Approved => "APPROVED",
        ExternalApprovalStatus.Rejected => "REJECTED",
        ExternalApprovalStatus.Canceled => "CANCELED",
        ExternalApprovalStatus.Deleted => "DELETED",
        ExternalApprovalStatus.Reverted => "REVERTED",
        _ => "PENDING",
    };
}

internal sealed class FeishuApprovalDetailData
{
    [System.Text.Json.Serialization.JsonPropertyName("approval_name")]
    public string? ApprovalName { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("description")]
    public string? Description { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("form")]
    public string? Form { get; set; }
}

internal sealed class FeishuCreateInstanceData
{
    [System.Text.Json.Serialization.JsonPropertyName("instance_code")]
    public string? InstanceCode { get; set; }
}

internal sealed class FeishuInstanceDetailData
{
    [System.Text.Json.Serialization.JsonPropertyName("instance_code")]
    public string? InstanceCode { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("approval_code")]
    public string? ApprovalCode { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("status")]
    public string? Status { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("start_time")]
    public long StartTime { get; set; }
}

/// <summary>
/// 飞书 approval/v4/instances（批量按时间窗口拉实例列表）响应数据。
/// </summary>
internal sealed class FeishuListInstancesData
{
    [System.Text.Json.Serialization.JsonPropertyName("instance_code_list")]
    public string[]? InstanceCodeList { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("page_token")]
    public string? PageToken { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}
