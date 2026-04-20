using System.Globalization;
using System.Text.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Models;
using Atlas.Connectors.WeCom.Internal;

namespace Atlas.Connectors.WeCom;

/// <summary>
/// 企业微信审批 Provider：getapprovaltmp / approval/applyevent / getapprovaldetail / getapprovalinfo。
/// 注意：企微未暴露"三方审批同步"通用接口，SyncThirdPartyInstanceAsync 返回 false。
/// </summary>
public sealed class WeComApprovalProvider : IExternalApprovalProvider
{
    private readonly WeComApiClient _api;

    public WeComApprovalProvider(WeComApiClient api)
    {
        _api = api;
    }

    public string ProviderType => WeComConnectorMarker.ProviderType;

    public async Task<ExternalApprovalTemplate> GetTemplateAsync(ConnectorContext context, string externalTemplateId, CancellationToken cancellationToken)
    {
        var runtime = WeComApiClient.ResolveRuntime(context);
        var body = new { template_id = externalTemplateId };
        var resp = await _api.SendAuthorizedPostJsonAsync<object, WeComApprovalTemplateResponse>(context, "/cgi-bin/oa/approval/gettemplatedetail", body, null, cancellationToken).ConfigureAwait(false);

        var controls = (resp.TemplateContent?.Controls ?? Array.Empty<WeComTemplateControl>())
            .Select(c => new ExternalApprovalTemplateControl
            {
                ControlId = c.Property?.ControlId ?? string.Empty,
                ControlType = c.Property?.Control ?? "Text",
                Title = c.Property?.Title?.FirstOrDefault()?.Text ?? string.Empty,
                Required = c.Property?.Require == 1,
                Options = MapOptions(c.Config),
            })
            .ToArray();

        return new ExternalApprovalTemplate
        {
            ProviderType = ProviderType,
            ProviderTenantId = runtime.CorpId,
            ExternalTemplateId = externalTemplateId,
            Name = resp.TemplateNames?.FirstOrDefault()?.Text ?? string.Empty,
            Description = null,
            Controls = controls,
            RawJson = JsonSerializer.Serialize(resp),
        };
    }

    public Task<IReadOnlyList<ExternalApprovalTemplate>> ListTemplatesAsync(ConnectorContext context, CancellationToken cancellationToken)
    {
        // 企微无开放的"列表全部模板"接口，返回空列表。
        return Task.FromResult<IReadOnlyList<ExternalApprovalTemplate>>(Array.Empty<ExternalApprovalTemplate>());
    }

    public async Task<ExternalApprovalInstanceRef> SubmitApprovalAsync(ConnectorContext context, ExternalApprovalSubmission submission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);
        // 企微 approval/applyevent body：{ creator_userid, template_id, use_template_approver, apply_data: { contents: [...] } }
        var contents = new List<object>(submission.Fields.Count);
        foreach (var (controlId, value) in submission.Fields)
        {
            contents.Add(new
            {
                control = MapValueTypeToControl(value.ValueType),
                id = controlId,
                value = ParseValue(value),
            });
        }
        var body = new
        {
            creator_userid = submission.ApplicantExternalUserId,
            template_id = submission.ExternalTemplateId,
            use_template_approver = (submission.ApproverExternalUserIds?.Count ?? 0) == 0 ? 1 : 0,
            apply_data = new { contents },
            summary_list = string.IsNullOrEmpty(submission.SummaryText)
                ? null
                : new[] { new { summary_info = new[] { new { text = submission.SummaryText, lang = "zh_CN" } } } },
        };
        var runtime = WeComApiClient.ResolveRuntime(context);
        var resp = await _api.SendAuthorizedPostJsonAsync<object, WeComApplyEventResponse>(context, "/cgi-bin/oa/applyevent", body, null, cancellationToken).ConfigureAwait(false);
        return new ExternalApprovalInstanceRef
        {
            ProviderType = ProviderType,
            ProviderTenantId = runtime.CorpId,
            ExternalInstanceId = resp.SpNo ?? string.Empty,
            ExternalTemplateId = submission.ExternalTemplateId,
            Status = ExternalApprovalStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            RawJson = JsonSerializer.Serialize(resp),
        };
    }

    public async Task<ExternalApprovalInstanceRef?> GetInstanceAsync(ConnectorContext context, string externalInstanceId, CancellationToken cancellationToken)
    {
        var runtime = WeComApiClient.ResolveRuntime(context);
        var body = new { sp_no = externalInstanceId };
        var resp = await _api.SendAuthorizedPostJsonAsync<object, WeComApprovalDetailResponse>(context, "/cgi-bin/oa/getapprovaldetail", body, null, cancellationToken).ConfigureAwait(false);
        if (resp.Info is null)
        {
            return null;
        }
        return new ExternalApprovalInstanceRef
        {
            ProviderType = ProviderType,
            ProviderTenantId = runtime.CorpId,
            ExternalInstanceId = resp.Info.SpNo ?? externalInstanceId,
            ExternalTemplateId = resp.Info.TemplateId ?? string.Empty,
            Status = MapWeComStatus(resp.Info.SpStatus),
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(resp.Info.ApplyTime),
            RawJson = JsonSerializer.Serialize(resp),
        };
    }

    public async Task<ExternalApprovalInstanceIdPage> ListRecentInstanceIdsAsync(ConnectorContext context, ExternalApprovalInstanceIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // 企微 getapprovalinfo：POST body = { starttime, endtime, new_cursor?, size?, filters?:[{key:"template_id",value:"xxx"}] }
        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["starttime"] = query.StartTime.ToUnixTimeSeconds(),
            ["endtime"] = query.EndTime.ToUnixTimeSeconds(),
            ["size"] = Math.Clamp(query.Size, 1, 100),
        };
        if (!string.IsNullOrEmpty(query.Cursor))
        {
            body["new_cursor"] = query.Cursor;
        }
        if (!string.IsNullOrEmpty(query.TemplateId))
        {
            body["filters"] = new[] { new { key = "template_id", value = query.TemplateId } };
        }

        var resp = await _api.SendAuthorizedPostJsonAsync<object, WeComApprovalInfoResponse>(context, "/cgi-bin/oa/getapprovalinfo", body, null, cancellationToken).ConfigureAwait(false);
        return new ExternalApprovalInstanceIdPage
        {
            InstanceIds = resp.SpNoList ?? Array.Empty<string>(),
            NextCursor = string.IsNullOrEmpty(resp.NextCursor) ? null : resp.NextCursor,
        };
    }

    public Task<bool> SyncThirdPartyInstanceAsync(ConnectorContext context, ExternalThirdPartyInstancePatch patch, CancellationToken cancellationToken)
    {
        // 企微未提供原生三方审批同步接口；模式 B 场景下应通过应用消息卡片 + update_template_card 同步状态变化。
        return Task.FromResult(false);
    }

    private static string MapValueTypeToControl(string valueType) => valueType.ToLowerInvariant() switch
    {
        "string" => "Text",
        "number" => "Number",
        "date" => "Date",
        "select" => "Selector",
        "multiselect" => "Selector",
        "file" => "File",
        "image" => "Image",
        _ => "Text",
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

    private static IReadOnlyList<ExternalApprovalTemplateOption>? MapOptions(WeComControlConfig? config)
    {
        if (config?.Selector?.Options is null || config.Selector.Options.Length == 0)
        {
            return null;
        }
        return config.Selector.Options
            .Select(o => new ExternalApprovalTemplateOption
            {
                Key = o.Key ?? string.Empty,
                Text = o.Value?.FirstOrDefault()?.Text ?? string.Empty,
            })
            .ToArray();
    }

    private static ExternalApprovalStatus MapWeComStatus(int spStatus) => spStatus switch
    {
        1 => ExternalApprovalStatus.Pending,
        2 => ExternalApprovalStatus.Approved,
        3 => ExternalApprovalStatus.Rejected,
        4 => ExternalApprovalStatus.Transferred,
        6 => ExternalApprovalStatus.Canceled,
        7 => ExternalApprovalStatus.Deleted,
        10 => ExternalApprovalStatus.Reverted,
        _ => ExternalApprovalStatus.Unknown,
    };
}

internal sealed class WeComApprovalTemplateResponse : WeComApiResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("template_names")]
    public WeComTextLang[]? TemplateNames { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("template_content")]
    public WeComTemplateContent? TemplateContent { get; set; }
}

internal sealed class WeComTextLang
{
    [System.Text.Json.Serialization.JsonPropertyName("text")]
    public string? Text { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("lang")]
    public string? Lang { get; set; }
}

internal sealed class WeComTemplateContent
{
    [System.Text.Json.Serialization.JsonPropertyName("controls")]
    public WeComTemplateControl[]? Controls { get; set; }
}

internal sealed class WeComTemplateControl
{
    [System.Text.Json.Serialization.JsonPropertyName("property")]
    public WeComControlProperty? Property { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("config")]
    public WeComControlConfig? Config { get; set; }
}

internal sealed class WeComControlProperty
{
    [System.Text.Json.Serialization.JsonPropertyName("control")]
    public string? Control { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string? ControlId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("title")]
    public WeComTextLang[]? Title { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("require")]
    public int Require { get; set; }
}

internal sealed class WeComControlConfig
{
    [System.Text.Json.Serialization.JsonPropertyName("selector")]
    public WeComSelectorConfig? Selector { get; set; }
}

internal sealed class WeComSelectorConfig
{
    [System.Text.Json.Serialization.JsonPropertyName("options")]
    public WeComSelectorOption[]? Options { get; set; }
}

internal sealed class WeComSelectorOption
{
    [System.Text.Json.Serialization.JsonPropertyName("key")]
    public string? Key { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("value")]
    public WeComTextLang[]? Value { get; set; }
}

internal sealed class WeComApplyEventResponse : WeComApiResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("sp_no")]
    public string? SpNo { get; set; }
}

internal sealed class WeComApprovalDetailResponse : WeComApiResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("info")]
    public WeComApprovalInfo? Info { get; set; }
}

internal sealed class WeComApprovalInfo
{
    [System.Text.Json.Serialization.JsonPropertyName("sp_no")]
    public string? SpNo { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("template_id")]
    public string? TemplateId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("sp_status")]
    public int SpStatus { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("apply_time")]
    public long ApplyTime { get; set; }
}
