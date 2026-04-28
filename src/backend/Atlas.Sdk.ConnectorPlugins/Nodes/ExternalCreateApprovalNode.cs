using System.Text.Json;
using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Connectors.Core.Models;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Sdk.ConnectorPlugins.Nodes;

/// <summary>
/// "创建外部审批实例" 节点：通过 ExternalApprovalDispatchService 完成。
/// inputs:
///   - localInstanceId: long
///   - flowDefinitionId: long
///   - applicantExternalUserId: string
///   - businessKey: string
///   - externalTemplateId: string
///   - fieldsJson: string (JSON 对象 { controlId: { valueType, rawJson } })
/// outputs:
///   - externalInstanceId: string?
///   - pushed: bool
///   - reason: string?
/// </summary>
public abstract class ExternalCreateApprovalNodeBase : IConnectorPluginNode
{
    private readonly IExternalApprovalDispatchService _dispatch;

    protected ExternalCreateApprovalNodeBase(IExternalApprovalDispatchService dispatch) { _dispatch = dispatch; }

    public abstract string NodeType { get; }
    public abstract string DisplayName { get; }
    public string Category => "external_collaboration";

    public async Task<ConnectorPluginNodeResult> ExecuteAsync(ConnectorPluginNodeContext context, CancellationToken cancellationToken)
    {
        try
        {
            var localInstanceId = Convert.ToInt64(context.Inputs.GetValueOrDefault("localInstanceId") ?? 0);
            var flowDefinitionId = Convert.ToInt64(context.Inputs.GetValueOrDefault("flowDefinitionId") ?? 0);
            var applicant = context.Inputs.GetValueOrDefault("applicantExternalUserId")?.ToString() ?? string.Empty;
            var businessKey = context.Inputs.GetValueOrDefault("businessKey")?.ToString() ?? Guid.NewGuid().ToString("N");
            var templateId = context.Inputs.GetValueOrDefault("externalTemplateId")?.ToString() ?? string.Empty;
            var fieldsJson = context.Inputs.GetValueOrDefault("fieldsJson")?.ToString() ?? "{}";
            var fields = ParseFields(fieldsJson);
            var payload = new ExternalApprovalSubmission
            {
                ExternalTemplateId = templateId,
                ApplicantExternalUserId = applicant,
                BusinessKey = businessKey,
                Fields = fields,
            };
            var result = await _dispatch.OnInstanceStartedAsync(localInstanceId, flowDefinitionId, payload, cancellationToken).ConfigureAwait(false);
            return new ConnectorPluginNodeResult
            {
                Success = result.Pushed,
                Outputs = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["externalInstanceId"] = result.ExternalInstanceId,
                    ["pushed"] = result.Pushed,
                    ["reason"] = result.Reason,
                    ["providerType"] = result.ProviderType,
                },
            };
        }
        catch (Exception ex)
        {
            return new ConnectorPluginNodeResult { Success = false, ErrorCode = ex.GetType().Name, ErrorMessage = ex.Message };
        }
    }

    private static IReadOnlyDictionary<string, ExternalApprovalFieldValue> ParseFields(string json)
    {
        var result = new Dictionary<string, ExternalApprovalFieldValue>(StringComparer.Ordinal);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var valueType = prop.Value.TryGetProperty("valueType", out var vt) && vt.ValueKind == JsonValueKind.String ? vt.GetString() ?? "string" : "string";
                var raw = prop.Value.TryGetProperty("rawJson", out var rj) ? rj.GetRawText() : prop.Value.GetRawText();
                result[prop.Name] = new ExternalApprovalFieldValue { ValueType = valueType, RawJson = raw };
            }
        }
        catch (JsonException) { }
        return result;
    }
}

public sealed class WeComCreateApprovalNode : ExternalCreateApprovalNodeBase
{
    public WeComCreateApprovalNode(IExternalApprovalDispatchService dispatch) : base(dispatch) { }
    public override string NodeType => "wecom_create_approval";
    public override string DisplayName => "创建企微审批";
}

public sealed class FeishuCreateApprovalNode : ExternalCreateApprovalNodeBase
{
    public FeishuCreateApprovalNode(IExternalApprovalDispatchService dispatch) : base(dispatch) { }
    public override string NodeType => "feishu_create_approval";
    public override string DisplayName => "创建飞书审批";
}

public sealed class DingTalkCreateApprovalNode : ExternalCreateApprovalNodeBase
{
    public DingTalkCreateApprovalNode(IExternalApprovalDispatchService dispatch) : base(dispatch) { }
    public override string NodeType => "dingtalk_create_approval";
    public override string DisplayName => "创建钉钉审批";
}
