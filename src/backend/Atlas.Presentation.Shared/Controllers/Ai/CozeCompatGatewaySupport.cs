using System.Globalization;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Platform.Models;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Presentation.Shared.Controllers.Ai;

public static class CozeCompatGatewaySupport
{
    public static object Success(object? data)
    {
        return new
        {
            code = 0,
            msg = "success",
            data,
            BaseResp = new { }
        };
    }

    public static object SuccessWithoutData()
    {
        return new
        {
            code = 0,
            msg = "success",
            BaseResp = new { }
        };
    }

    public static object Fail(string message)
    {
        return new
        {
            code = 400,
            msg = message,
            BaseResp = new { }
        };
    }

    public static object BuildTypeListPayload(IReadOnlyList<ModelConfigDto> models, int? modelScene)
    {
        var modelList = models.Select(item => new
        {
            name = item.DefaultModel,
            model_type = item.Id,
            model_name = string.IsNullOrWhiteSpace(item.ModelId) ? item.DefaultModel : item.ModelId,
            endpoint_name = item.ProviderType,
            model_class_name = item.ProviderType,
            model_brief_desc = item.SystemPrompt ?? item.Name,
            model_desc = new[]
            {
                new
                {
                    group_name = item.ProviderType,
                    desc = new[] { item.SystemPrompt ?? item.Name }
                }
            },
            model_params = BuildModelParams(item),
            model_ability = new
            {
                cot_display = item.EnableReasoning,
                function_call = item.EnableTools,
                image_understanding = item.EnableVision,
                support_multi_modal = item.EnableVision
            },
            model_status_details = new
            {
                is_free_model = true
            }
        }).ToArray();

        return new
        {
            model_list = modelList,
            voice_list = Array.Empty<object>(),
            raw_model_list = modelList,
            model_show_family_list = Array.Empty<object>(),
            default_model_id = modelList.FirstOrDefault()?.model_type ?? 0,
            total = modelList.Length,
            has_more = false,
            model_scene = modelScene
        };
    }

    public static object BuildPlaygroundFallbackData(string? path)
    {
        var normalizedPath = NormalizeFallbackPath(path);
        return normalizedPath switch
        {
            "space/info" => new { data = (object?)null },
            "space/delete" => new { success = true },
            "space/invite" => new { invite_link = string.Empty },
            "space/member/detail" => new { member = (object?)null },
            "space/member/update" or "space/member/transfer" or "space/member/remove" or "space/member/add" or "space/member/exit" => new { success = true },
            "space/member/search" => new { list = Array.Empty<object>(), total = 0 },
            "space/revocate_invite" => new { success = true },
            "space/invite_manage_list" or "space/apply_manage_list" => new { list = Array.Empty<object>(), total = 0 },
            "space/remove_publish_member" or "space/add_publish_member" or "space/operate_apply" => new { success = true },
            "space/search_addable_publish_member" or "space/publish_member_list" => new { list = Array.Empty<object>(), total = 0 },
            "space/import/confirm" => new { success = true },
            "space/import/list" or "space/import/user_list" => new { list = Array.Empty<object>(), total = 0 },
            "get_type_list" => new
            {
                model_list = Array.Empty<object>(),
                voice_list = Array.Empty<object>(),
                raw_model_list = Array.Empty<object>(),
                model_show_family_list = Array.Empty<object>(),
                default_model_id = 0
            },
            "get_voice_list" or "get_voice_list_v2" => new { list = Array.Empty<object>(), total = 0 },
            "get_support_language" => new { list = Array.Empty<object>() },
            "synchronize_voice_list" => new { success = true },
            "create_room" => new { room_id = string.Empty },
            _ => new { success = true }
        };
    }

    public static object BuildDeveloperFallbackData(string? path)
    {
        var normalizedPath = NormalizeFallbackPath(path);
        var leaf = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? normalizedPath;
        return (normalizedPath, leaf) switch
        {
            ("draftbot/get_draft_bot_list", _) or (_, "get_draft_bot_list") => new { total = 0, list = Array.Empty<object>() },
            ("bot/upload_file", _) or (_, "upload_file") => new { file_id = Guid.NewGuid().ToString("N"), file_url = string.Empty },
            _ => new { success = true }
        };
    }

    public static object MapOpenWorkspace(WorkspaceListItem item)
    {
        return new
        {
            id = item.Id,
            name = item.Name,
            icon_url = item.Icon ?? string.Empty,
            role_type = ToOpenRoleType(item.RoleCode),
            workspace_type = "team",
            enterprise_id = item.OrgId,
            joined_status = "joined",
            description = item.Description ?? string.Empty,
            owner_uid = string.Empty,
            admin_uids = Array.Empty<string>()
        };
    }

    public static object MapOpenSpaceMember(WorkspaceMemberDto item)
    {
        return new
        {
            user_id = item.UserId,
            user_nickname = item.DisplayName,
            user_unique_name = item.Username,
            avatar_url = string.Empty,
            role_type = ToOpenRoleType(item.RoleCode)
        };
    }

    public static string ToWorkspaceRoleCode(string? roleType)
    {
        if (string.Equals(roleType, "owner", StringComparison.OrdinalIgnoreCase))
        {
            return "Owner";
        }

        if (string.Equals(roleType, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return "Admin";
        }

        return "Member";
    }

    public static string ToOpenRoleType(string? roleCode)
    {
        if (string.Equals(roleCode, "Owner", StringComparison.OrdinalIgnoreCase))
        {
            return "owner";
        }

        if (string.Equals(roleCode, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return "admin";
        }

        return "member";
    }

    public static long ToUnixMilliseconds(DateTime value)
    {
        return new DateTimeOffset(value).ToUnixTimeMilliseconds();
    }

    public static string ToCozeNodeTypeCode(string nodeTypeKey)
    {
        if (Enum.TryParse<WorkflowNodeType>(nodeTypeKey, true, out var nodeType))
        {
            return ((int)nodeType).ToString(CultureInfo.InvariantCulture);
        }

        return nodeTypeKey;
    }

    public static string NormalizeFallbackPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.Trim().Trim('/').ToLowerInvariant();
    }

    private static object[] BuildModelParams(ModelConfigDto item)
    {
        var temperature = (item.Temperature ?? 1f).ToString("0.0", CultureInfo.InvariantCulture);
        var maxTokens = (item.MaxTokens ?? 4096).ToString(CultureInfo.InvariantCulture);

        return
        [
            new
            {
                name = "temperature",
                label = "Temperature",
                type = 1,
                min = "0",
                max = "2",
                precision = 1,
                default_val = BuildDefaultValue(temperature),
                param_class = new { class_id = 1, label = "Generation diversity" }
            },
            new
            {
                name = "max_tokens",
                label = "Max Tokens",
                type = 2,
                min = "1",
                max = "32000",
                precision = 0,
                default_val = BuildDefaultValue(maxTokens),
                param_class = new { class_id = 2, label = "Input and output length" }
            },
            new
            {
                name = "response_format",
                label = "Response format",
                type = 2,
                min = "1",
                max = "3",
                precision = 0,
                default_val = BuildDefaultValue("3"),
                options = new object[]
                {
                    new { label = "Text", value = 1 },
                    new { label = "Markdown", value = 2 },
                    new { label = "JSON", value = 3 }
                },
                param_class = new { class_id = 3, label = "Response format" }
            }
        ];
    }

    private static object BuildDefaultValue(string value)
    {
        return new
        {
            default_val = value,
            creative = value,
            balance = value,
            precise = value,
            customize = value
        };
    }
}
