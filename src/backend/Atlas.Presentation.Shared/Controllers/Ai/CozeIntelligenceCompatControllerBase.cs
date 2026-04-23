using System.Globalization;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Presentation.Shared.Controllers.Ai;

public abstract class CozeIntelligenceCompatControllerBase : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, string> PublishConnectorNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["1001"] = "Web SDK",
            ["2001"] = "Generic Endpoint",
            ["3001"] = "OAuth Demo"
        };
    protected readonly IWorkspacePortalService _workspacePortalService;
    protected readonly ITenantProvider _tenantProvider;
    protected readonly ICurrentUserAccessor _currentUserAccessor;

    protected CozeIntelligenceCompatControllerBase(
        IWorkspacePortalService workspacePortalService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _workspacePortalService = workspacePortalService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpPost("/api/intelligence_api/ping")]
    [Authorize]
    public ActionResult<object> Ping()
    {
        return Ok(SuccessWithoutData());
    }

    [HttpPost("/api/intelligence_api/search/get_draft_intelligence_list")]
    [Authorize]
    public async Task<ActionResult<object>> GetDraftIntelligenceList(
        [FromBody] CozeGetDraftIntelligenceListRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureWorkspaceAccessibleAsync(request.space_id, cancellationToken);

        // Atlas 当前尚未提供完整的 Coze intelligence 域模型。
        // 这里先返回稳定空列表，消除前端 404，并保持现有列表页/选择器可继续工作。
        return Ok(Success(new
        {
            intelligences = Array.Empty<object>(),
            total = 0,
            has_more = false,
            next_cursor_id = string.Empty
        }));
    }

    [HttpPost("/api/intelligence_api/search/get_draft_intelligence_info")]
    [Authorize]
    public async Task<ActionResult<object>> GetDraftIntelligenceInfo(
        [FromBody] CozeGetDraftIntelligenceInfoRequest? request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var workspace = await ResolveWorkspaceAsync(null, cancellationToken);
        var intelligenceId = string.IsNullOrWhiteSpace(request?.intelligence_id)
            ? "compat-project"
            : request!.intelligence_id!;
        var intelligenceType = request?.intelligence_type is 1 or 2 or 3
            ? request.intelligence_type!.Value
            : 2;
        var displayName = intelligenceType == 1 ? $"Compat Bot {intelligenceId}" : $"Compat Project {intelligenceId}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

        return Ok(Success(new
        {
            intelligence_type = intelligenceType,
            basic_info = new
            {
                id = intelligenceId,
                name = displayName,
                description = "Atlas compatibility placeholder",
                icon_uri = string.Empty,
                icon_url = string.Empty,
                space_id = workspace?.Id ?? string.Empty,
                owner_id = currentUser.UserId.ToString(CultureInfo.InvariantCulture),
                create_time = now,
                update_time = now,
                status = 1
            },
            publish_info = new
            {
                publish_time = string.Empty,
                has_published = false,
                connectors = Array.Empty<object>()
            },
            owner_info = BuildUserInfo(currentUser)
        }));
    }

    [HttpPost("/api/intelligence_api/search/get_publish_intelligence_list")]
    [Authorize]
    public async Task<ActionResult<object>> GetPublishIntelligenceList(
        [FromBody] CozeGetPublishIntelligenceListRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureWorkspaceAccessibleAsync(request.space_id, cancellationToken);

        if (request.intelligence_type != 1)
        {
            return Ok(Success(new
            {
                intelligences = Array.Empty<object>(),
                total = 0,
                has_more = false,
                next_cursor_id = string.Empty
            }));
        }

        var queryService = HttpContext.RequestServices.GetService<IAgentQueryService>();
        if (queryService is null)
        {
            return Ok(Success(new
            {
                intelligences = Array.Empty<object>(),
                total = 0,
                has_more = false,
                next_cursor_id = string.Empty
            }));
        }

        var workspaceId = long.TryParse(request.space_id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedWorkspaceId)
            ? parsedWorkspaceId
            : (long?)null;
        var pageSize = request.size > 0 ? Math.Min((int)request.size, 50) : 20;
        var paged = await queryService.GetPagedAsync(
            _tenantProvider.GetTenantId(),
            request.name,
            status: null,
            workspaceId,
            pageIndex: 1,
            pageSize: pageSize,
            cancellationToken);

        var idFilter = request.intelligence_ids?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var publishItems = paged.Items
            .Where(item => item.PublishVersion > 0)
            .Where(item => idFilter is null || idFilter.Count == 0 || idFilter.Contains(item.Id.ToString(CultureInfo.InvariantCulture)))
            .Select(item => new
            {
                basic_info = new
                {
                    id = item.Id.ToString(CultureInfo.InvariantCulture),
                    name = item.Name,
                    description = item.Description ?? string.Empty,
                    icon_uri = item.AvatarUrl ?? string.Empty,
                    icon_url = item.AvatarUrl ?? string.Empty,
                    space_id = request.space_id,
                    owner_id = string.Empty,
                    create_time = CozeCompatGatewaySupport.ToUnixMilliseconds(item.CreatedAt).ToString(CultureInfo.InvariantCulture),
                    update_time = CozeCompatGatewaySupport.ToUnixMilliseconds(item.CreatedAt).ToString(CultureInfo.InvariantCulture),
                    status = 1,
                    publish_time = item.PublishVersion.ToString(CultureInfo.InvariantCulture)
                },
                user_info = BuildUserInfo(_currentUserAccessor.GetCurrentUserOrThrow()),
                connectors = new object[]
                {
                    new
                    {
                        id = "1001",
                        name = "Web SDK",
                        icon = string.Empty,
                        connector_status = 0,
                        share_link = $"/space/{Uri.EscapeDataString(request.space_id)}/bot/{item.Id.ToString(CultureInfo.InvariantCulture)}"
                    }
                },
                total_token = "0",
                permission_type = 3,
                trigger = false
            })
            .ToArray();

        return Ok(Success(new
        {
            intelligences = publishItems,
            total = publishItems.Length,
            has_more = false,
            next_cursor_id = string.Empty
        }));
    }

    [HttpPost("/api/intelligence_api/publish/publish_record_detail")]
    [Authorize]
    public async Task<ActionResult<object>> GetPublishRecordDetail(
        [FromBody] CozeGetPublishRecordDetailRequest request,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(request.project_id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var botId) || botId <= 0)
        {
            return Ok(Success(data: null));
        }

        var queryService = HttpContext.RequestServices.GetService<IAgentQueryService>();
        if (queryService is null)
        {
            return Ok(Success(data: null));
        }

        var detail = await queryService.GetByIdAsync(_tenantProvider.GetTenantId(), botId, cancellationToken);
        if (detail is null || detail.PublishVersion <= 0)
        {
            return Ok(Success(data: null));
        }

        var recordId = BuildPublishRecordId(detail.Id, detail.PublishVersion);
        if (!string.IsNullOrWhiteSpace(request.publish_record_id)
            && !string.Equals(request.publish_record_id, recordId, StringComparison.OrdinalIgnoreCase))
        {
            return Ok(Success(data: null));
        }

        return Ok(Success(BuildPublishRecordDetailPayload(detail)));
    }

    [HttpPost("/api/intelligence_api/publish/publish_record_list")]
    [Authorize]
    public async Task<ActionResult<object>> GetPublishRecordList(
        [FromBody] CozeGetPublishRecordListRequest request,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(request.project_id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var botId) || botId <= 0)
        {
            return Ok(Success(Array.Empty<object>()));
        }

        var queryService = HttpContext.RequestServices.GetService<IAgentQueryService>();
        if (queryService is null)
        {
            return Ok(Success(Array.Empty<object>()));
        }

        var detail = await queryService.GetByIdAsync(_tenantProvider.GetTenantId(), botId, cancellationToken);
        if (detail is null || detail.PublishVersion <= 0)
        {
            return Ok(Success(Array.Empty<object>()));
        }

        return Ok(Success(new[]
        {
            BuildPublishRecordDetailPayload(detail)
        }));
    }

    [HttpPost("/api/intelligence_api/draft_project/create")]
    [Authorize]
    public async Task<ActionResult<object>> DraftProjectCreate(
        [FromBody] CozeDraftProjectCreateRequest? request,
        CancellationToken cancellationToken)
    {
        await EnsureWorkspaceAccessibleAsync(request?.space_id, cancellationToken);
        var projectId = $"compat-project-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}";

        return Ok(Success(new
        {
            project_id = projectId,
            audit_data = BuildAuditData()
        }));
    }

    [HttpPost("/api/intelligence_api/draft_project/update")]
    [Authorize]
    public ActionResult<object> DraftProjectUpdate([FromBody] CozeDraftProjectUpdateRequest? _request)
    {
        return Ok(Success(new
        {
            audit_data = BuildAuditData()
        }));
    }

    [HttpPost("/api/intelligence_api/draft_project/copy")]
    [Authorize]
    public async Task<ActionResult<object>> DraftProjectCopy(
        [FromBody] CozeDraftProjectCopyRequest? request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        await EnsureWorkspaceAccessibleAsync(request?.to_space_id, cancellationToken);
        var copiedProjectId = string.IsNullOrWhiteSpace(request?.project_id)
            ? $"compat-project-copy-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}"
            : $"{request!.project_id}-copy";

        return Ok(Success(new
        {
            basic_info = new
            {
                id = copiedProjectId,
                name = string.IsNullOrWhiteSpace(request?.name) ? "Compat Copied Project" : request!.name,
                description = request?.description ?? string.Empty,
                icon_uri = request?.icon_uri ?? string.Empty,
                icon_url = string.Empty,
                status = 1
            },
            audit_data = BuildAuditData(),
            user_info = BuildUserInfo(currentUser)
        }));
    }

    [HttpPost("/api/intelligence_api/draft_project/delete")]
    [Authorize]
    public ActionResult<object> DraftProjectDelete([FromBody] CozeDraftProjectDeleteRequest? _request)
    {
        return Ok(SuccessWithoutData());
    }

    [HttpPost("/api/intelligence_api/draft_project/inner_task_list")]
    [Authorize]
    public ActionResult<object> DraftProjectInnerTaskList([FromBody] CozeDraftProjectInnerTaskListRequest? _request)
    {
        return Ok(Success(new
        {
            task_list = Array.Empty<object>()
        }));
    }

    [HttpPost("/api/intelligence_api/entity_task/search")]
    [Authorize]
    public ActionResult<object> EntityTaskSearch([FromBody] CozeEntityTaskSearchRequest? request)
    {
        var entityTaskMap = (request?.task_list ?? Array.Empty<CozeTaskStruct>())
            .Where(item => !string.IsNullOrWhiteSpace(item.entity_id))
            .ToDictionary(
                item => item.entity_id!,
                item => (object)new
                {
                    entity_id = item.entity_id,
                    entity_status = 1
                },
                StringComparer.OrdinalIgnoreCase);

        return Ok(Success(new
        {
            entity_task_map = entityTaskMap
        }));
    }

    [HttpPost("/api/intelligence_api/entity_task/process")]
    [Authorize]
    public ActionResult<object> ProcessEntityTask([FromBody] CozeProcessEntityTaskRequest? request)
    {
        return Ok(Success(new
        {
            entity_task = new
            {
                entity_id = request?.entity_id ?? string.Empty,
                entity_status = 1
            }
        }));
    }

    private async Task EnsureWorkspaceAccessibleAsync(string? workspaceId, CancellationToken cancellationToken)
    {
        _ = await ResolveWorkspaceAsync(workspaceId, cancellationToken);
    }

    private async Task<WorkspaceListItem?> ResolveWorkspaceAsync(string? workspaceId, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var workspaces = await _workspacePortalService.ListWorkspacesAsync(
            _tenantProvider.GetTenantId(),
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return workspaces.FirstOrDefault();
        }

        return workspaces.FirstOrDefault(item => string.Equals(item.Id, workspaceId, StringComparison.OrdinalIgnoreCase));
    }

    private static object BuildUserInfo(CurrentUserInfo currentUser)
    {
        return new
        {
            user_id = currentUser.UserId.ToString(CultureInfo.InvariantCulture),
            nickname = string.IsNullOrWhiteSpace(currentUser.DisplayName) ? currentUser.Username : currentUser.DisplayName,
            avatar_url = string.Empty,
            user_unique_name = currentUser.Username
        };
    }

    private static object BuildAuditData()
    {
        return new
        {
            check_not_pass = false,
            check_not_pass_msg = string.Empty
        };
    }

    private static object BuildPublishRecordDetailPayload(AgentDetail detail)
    {
        var publishTime = detail.PublishedAt ?? detail.UpdatedAt ?? detail.CreatedAt;
        var connectorPublishResult = BuildConnectorPublishResults(detail);
        return new
        {
            publish_record_id = BuildPublishRecordId(detail.Id, detail.PublishVersion),
            version_number = $"v{detail.PublishVersion}",
            publish_status = 5,
            publish_status_msg = string.Empty,
            connector_publish_result = connectorPublishResult,
            publish_status_detail = new
            {
                pack_failed_detail = Array.Empty<object>()
            },
            publish_time = CozeCompatGatewaySupport.ToUnixMilliseconds(publishTime).ToString(CultureInfo.InvariantCulture)
        };
    }

    private static object[] BuildConnectorPublishResults(AgentDetail detail)
    {
        if (string.IsNullOrWhiteSpace(detail.PublishedConnectorConfigJson))
        {
            return [BuildDefaultConnectorPublishResult(detail)];
        }

        try
        {
            using var document = JsonDocument.Parse(detail.PublishedConnectorConfigJson);
            if (!document.RootElement.TryGetProperty("connectors", out var connectorsElement)
                || connectorsElement.ValueKind != JsonValueKind.Object)
            {
                return [BuildDefaultConnectorPublishResult(detail)];
            }

            var results = new List<object>();
            foreach (var connectorProperty in connectorsElement.EnumerateObject())
            {
                var connectorId = connectorProperty.Name;
                var connectorState = connectorProperty.Value;
                if (connectorState.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var configStatus = TryGetInt32(connectorState, "ConfigStatus");
                var connectorStatus = TryGetInt32(connectorState, "ConnectorStatus");
                var isLastPublished = TryGetBoolean(connectorState, "IsLastPublished", defaultValue: true);
                var shareLink = TryGetString(connectorState, "ShareLink");
                var bindInfo = TryGetStringDictionary(connectorState, "Detail");

                if (!isLastPublished
                    && string.IsNullOrWhiteSpace(shareLink)
                    && bindInfo.Count == 0
                    && configStatus <= 0
                    && connectorStatus <= 0)
                {
                    continue;
                }

                results.Add(new
                {
                    connector_id = connectorId,
                    connector_name = ResolveConnectorName(connectorId),
                    connector_icon_url = string.Empty,
                    connector_publish_status = MapConnectorPublishStatus(configStatus, connectorStatus, isLastPublished),
                    connector_publish_status_msg = string.Empty,
                    share_link = string.IsNullOrWhiteSpace(shareLink)
                        ? BuildDefaultConnectorShareLink(detail, connectorId)
                        : shareLink,
                    download_link = string.Empty,
                    connector_publish_config = new
                    {
                        selected_workflows = Array.Empty<object>()
                    },
                    connector_bind_info = bindInfo
                });
            }

            return results.Count > 0 ? results.ToArray() : [BuildDefaultConnectorPublishResult(detail)];
        }
        catch
        {
            return [BuildDefaultConnectorPublishResult(detail)];
        }
    }

    private static object BuildDefaultConnectorPublishResult(AgentDetail detail)
        => new
        {
            connector_id = "1001",
            connector_name = ResolveConnectorName("1001"),
            connector_icon_url = string.Empty,
            connector_publish_status = 2,
            connector_publish_status_msg = string.Empty,
            share_link = BuildDefaultConnectorShareLink(detail, "1001"),
            download_link = string.Empty,
            connector_publish_config = new
            {
                selected_workflows = Array.Empty<object>()
            },
            connector_bind_info = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

    private static string BuildDefaultConnectorShareLink(AgentDetail detail, string connectorId)
    {
        if (!string.Equals(connectorId, "1001", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return detail.WorkspaceId.HasValue
            ? $"/space/{detail.WorkspaceId.Value.ToString(CultureInfo.InvariantCulture)}/bot/{detail.Id.ToString(CultureInfo.InvariantCulture)}"
            : string.Empty;
    }

    private static string ResolveConnectorName(string connectorId)
        => PublishConnectorNames.TryGetValue(connectorId, out var name) ? name : connectorId;

    private static int MapConnectorPublishStatus(int configStatus, int connectorStatus, bool isLastPublished)
    {
        if (connectorStatus == 1)
        {
            return 1;
        }

        if (connectorStatus == 2)
        {
            return 4;
        }

        if (configStatus is 2 or 5)
        {
            return 3;
        }

        return isLastPublished ? 2 : 0;
    }

    private static int TryGetInt32(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out var value))
        {
            return value;
        }

        return 0;
    }

    private static bool TryGetBoolean(JsonElement element, string propertyName, bool defaultValue)
    {
        if (element.TryGetProperty(propertyName, out var property)
            && (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False))
        {
            return property.GetBoolean();
        }

        return defaultValue;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        return null;
    }

    private static Dictionary<string, string> TryGetStringDictionary(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in property.EnumerateObject())
        {
            result[item.Name] = item.Value.ValueKind == JsonValueKind.String
                ? item.Value.GetString() ?? string.Empty
                : item.Value.ToString();
        }

        return result;
    }

    private static string BuildPublishRecordId(long botId, int publishVersion)
        => $"bot-{botId.ToString(CultureInfo.InvariantCulture)}-v{publishVersion.ToString(CultureInfo.InvariantCulture)}";

    protected static object Success(object? data)
    {
        return new
        {
            code = 0,
            msg = "success",
            data
        };
    }

    protected static object SuccessWithoutData()
    {
        return new
        {
            code = 0,
            msg = "success"
        };
    }
}

public sealed record CozeGetDraftIntelligenceListRequest(
    string space_id,
    string? name,
    bool? has_published,
    int[]? status,
    int[]? types,
    int? search_scope,
    string? folder_id,
    bool? folder_include_children,
    int? order_type,
    bool? is_fav,
    bool? recently_open,
    object? option,
    int? order_by,
    string? cursor_id,
    int? size);

public sealed record CozeGetDraftIntelligenceInfoRequest(
    string? intelligence_id,
    int? intelligence_type,
    string? version);

public sealed record CozeGetPublishIntelligenceListRequest(
    int intelligence_type,
    string space_id,
    string? owner_id,
    string? name,
    int? order_last_publish_time,
    int? order_total_token,
    long size,
    string? cursor_id,
    IReadOnlyList<string>? intelligence_ids);

public sealed record CozeGetPublishRecordDetailRequest(
    string project_id,
    string? publish_record_id);

public sealed record CozeGetPublishRecordListRequest(string project_id);

public sealed record CozeDraftProjectCreateRequest(
    string? space_id,
    string? name,
    string? description,
    string? icon_uri,
    object? monetization_conf,
    string? create_from,
    string? folder_id);

public sealed record CozeDraftProjectUpdateRequest(
    string? project_id,
    string? name,
    string? description,
    string? icon_uri);

public sealed record CozeDraftProjectCopyRequest(
    string? project_id,
    string? to_space_id,
    string? name,
    string? description,
    string? icon_uri);

public sealed record CozeDraftProjectDeleteRequest(string? project_id);

public sealed record CozeDraftProjectInnerTaskListRequest(string? project_id);

public sealed record CozeTaskStruct(string? entity_id, int? task_type);

public sealed record CozeEntityTaskSearchRequest(IReadOnlyList<CozeTaskStruct>? task_list);

public sealed record CozeProcessEntityTaskRequest(
    string? entity_id,
    int? action,
    IReadOnlyList<string>? task_id_list);
