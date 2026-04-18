using System.Text.Json;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// 组件 manifest 服务（M06 S06-1）。
///
/// - 静态 manifest 由前端构建时输出（暂以代码内常量代替；M06 真实构建脚本由 M07 集成时落地）。
/// - 租户级 overrides 与静态 manifest 合并返回。
/// </summary>
public interface ILowCodeComponentManifestService
{
    Task<ComponentRegistryDto> GetRegistryAsync(TenantId tenantId, string? renderer, CancellationToken cancellationToken);
    Task UpsertOverrideAsync(TenantId tenantId, long currentUserId, ComponentOverrideUpsertRequest request, CancellationToken cancellationToken);
    Task DeleteOverrideAsync(TenantId tenantId, long currentUserId, string type, CancellationToken cancellationToken);
}

public sealed class LowCodeComponentManifestService : ILowCodeComponentManifestService
{
    private readonly IAppComponentOverrideRepository _repo;
    private readonly IAuditWriter _auditWriter;
    private readonly IIdGeneratorAccessor _idGen;

    public LowCodeComponentManifestService(IAppComponentOverrideRepository repo, IAuditWriter auditWriter, IIdGeneratorAccessor idGen)
    {
        _repo = repo;
        _auditWriter = auditWriter;
        _idGen = idGen;
    }

    public async Task<ComponentRegistryDto> GetRegistryAsync(TenantId tenantId, string? renderer, CancellationToken cancellationToken)
    {
        var staticManifest = LowCodeComponentStaticManifest.GetAll(renderer);
        var overrides = await _repo.ListAsync(tenantId, cancellationToken);
        var overrideDtos = overrides.Select(o => new ComponentTenantOverrideDto(o.Type, o.Hidden, o.DefaultPropsJson)).ToList();
        return new ComponentRegistryDto(staticManifest, overrideDtos);
    }

    public async Task UpsertOverrideAsync(TenantId tenantId, long currentUserId, ComponentOverrideUpsertRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.DefaultPropsJson))
        {
            EnsureValidJson(request.DefaultPropsJson, nameof(request.DefaultPropsJson));
        }
        var existing = await _repo.FindAsync(tenantId, request.Type, cancellationToken);
        if (existing is null)
        {
            var entity = new AppComponentOverride(tenantId, _idGen.NextId(), request.Type, request.Hidden, request.DefaultPropsJson);
            await _repo.InsertAsync(entity, cancellationToken);
        }
        else
        {
            existing.Update(request.Hidden, request.DefaultPropsJson);
            await _repo.UpdateAsync(existing, cancellationToken);
        }
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.components.override.upsert", "success", $"type:{request.Type}", null, null), cancellationToken);
    }

    public async Task DeleteOverrideAsync(TenantId tenantId, long currentUserId, string type, CancellationToken cancellationToken)
    {
        await _repo.DeleteAsync(tenantId, type, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.components.override.delete", "success", $"type:{type}", null, null), cancellationToken);
    }

    private static void EnsureValidJson(string json, string fieldName)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new BusinessException(ErrorCodes.ValidationError, $"{fieldName} 不是合法 JSON：{ex.Message}");
        }
    }
}

/// <summary>
/// 静态组件 manifest 镜像（与 @atlas/lowcode-components-web/src/meta/categories.ts 完全一致）。
///
/// 注：作为后端"内置 fallback manifest"维护；与前端 packages/lowcode-components-web 的 categories.ts
/// 共享类型契约。后端不强制读前端 build artifact，避免设计期 / 运行期跨工作区耦合：
///  - 新组件只在前端 categories.ts 添加 → 同时在此处补一条 Make(...)
///  - CI 守门：components-web 测试断言两边数量一致
/// </summary>
internal static class LowCodeComponentStaticManifest
{
    public static IReadOnlyList<ComponentMetaDto> GetAll(string? renderer)
    {
        var renderers = string.IsNullOrWhiteSpace(renderer) ? new[] { "web" } : new[] { renderer! };

        ComponentMetaDto Make(string type, string displayName, string category, string[] bindableProps, string[]? events = null, string? group = null, string[]? contentParams = null, string arity = "none", string[]? allowTypes = null)
            => new(type, displayName, category, group, "1.0.0", renderers, bindableProps, contentParams, events ?? Array.Empty<string>(), new ChildPolicyDto(arity, allowTypes));

        var list = new List<ComponentMetaDto>
        {
            // layout 8
            Make("Container", "容器", "layout", new[] { "className", "style" }, arity: "many"),
            Make("Row", "行", "layout", new[] { "gap", "justify", "align" }, arity: "many"),
            Make("Column", "列", "layout", new[] { "gap", "justify", "align" }, arity: "many"),
            Make("Tabs", "标签页", "layout", new[] { "activeKey" }, new[] { "onChange" }, arity: "many"),
            Make("Drawer", "抽屉", "layout", new[] { "visible", "placement", "title" }, new[] { "onChange" }, arity: "many"),
            Make("Modal", "弹窗", "layout", new[] { "visible", "title" }, new[] { "onChange", "onSubmit" }, arity: "many"),
            Make("Grid", "网格", "layout", new[] { "columns", "gap" }, arity: "many"),
            Make("Section", "段落", "layout", new[] { "title" }, arity: "many"),

            // display 13
            Make("Text", "文字", "display", new[] { "content", "color" }, contentParams: new[] { "text" }),
            Make("Markdown", "Markdown", "display", new[] { "content" }, contentParams: new[] { "text" }),
            Make("Image", "图片", "display", new[] { "src", "alt", "fit" }, contentParams: new[] { "image" }),
            Make("Video", "视频", "display", new[] { "src", "poster", "autoplay", "controls" }, contentParams: new[] { "media" }),
            Make("Avatar", "头像", "display", new[] { "src", "name", "size" }, contentParams: new[] { "image" }),
            Make("Badge", "徽标", "display", new[] { "count", "color" }),
            Make("Progress", "进度", "display", new[] { "percent", "status" }),
            Make("Rate", "评分", "display", new[] { "value", "count" }, new[] { "onChange" }),
            Make("Chart", "图表", "display", new[] { "data", "type", "options" }, contentParams: new[] { "data" }),
            Make("EmptyState", "空状态", "display", new[] { "title", "description" }),
            Make("Loading", "加载", "display", new[] { "size" }),
            Make("Error", "错误", "display", new[] { "message", "retryable" }, new[] { "onClick" }),
            Make("Toast", "提示", "display", new[] { "message", "type", "duration" }),

            // input 18
            Make("Button", "按钮", "input", new[] { "text", "disabled", "loading" }, new[] { "onClick" }),
            Make("TextInput", "文本输入", "input", new[] { "value", "placeholder", "disabled" }, new[] { "onChange" }),
            Make("NumberInput", "数字输入", "input", new[] { "value", "min", "max", "step" }, new[] { "onChange" }),
            Make("Switch", "开关", "input", new[] { "value" }, new[] { "onChange" }),
            Make("Select", "下拉", "input", new[] { "value", "options", "placeholder", "disabled" }, new[] { "onChange" }, contentParams: new[] { "data" }),
            Make("RadioGroup", "单选组", "input", new[] { "value", "options" }, new[] { "onChange" }, contentParams: new[] { "data" }),
            Make("CheckboxGroup", "多选组", "input", new[] { "value", "options" }, new[] { "onChange" }, contentParams: new[] { "data" }),
            Make("DatePicker", "日期选择", "input", new[] { "value", "format" }, new[] { "onChange" }),
            Make("TimePicker", "时间选择", "input", new[] { "value", "format" }, new[] { "onChange" }),
            Make("ColorPicker", "颜色选择", "input", new[] { "value" }, new[] { "onChange" }),
            Make("Slider", "滑动条", "input", new[] { "value", "min", "max", "step" }, new[] { "onChange" }),
            Make("FileUpload", "文件上传", "input", new[] { "value", "accept", "multiple" }, new[] { "onUploadSuccess", "onUploadError", "onChange" }),
            Make("ImageUpload", "图片上传", "input", new[] { "value", "accept", "multiple" }, new[] { "onUploadSuccess", "onUploadError", "onChange" }),
            Make("CodeEditor", "代码编辑", "input", new[] { "value", "language", "readonly" }, new[] { "onChange" }),
            Make("FormContainer", "表单容器", "input", new[] { "initialValues" }, new[] { "onSubmit", "onChange" }, arity: "many"),
            Make("FormField", "表单字段", "input", new[] { "name", "label", "required" }, arity: "one"),
            Make("SearchBox", "搜索", "input", new[] { "value", "placeholder" }, new[] { "onChange", "onSubmit" }),
            Make("Filter", "筛选", "input", new[] { "value", "options" }, new[] { "onChange" }, contentParams: new[] { "data" }),

            // ai 4
            Make("AiChat", "AI 对话", "ai", new[] { "chatflowId", "sessionId", "modelId" }, new[] { "onChange", "onSubmit" }, contentParams: new[] { "ai" }),
            Make("AiCard", "AI 卡片", "ai", new[] { "chatflowId", "cardConfig" }, new[] { "onClick" }, contentParams: new[] { "ai" }),
            Make("AiSuggestion", "AI 推荐", "ai", new[] { "suggestions", "modelId" }, new[] { "onItemClick" }, contentParams: new[] { "data" }),
            Make("AiAvatarReply", "AI 头像回复", "ai", new[] { "chatflowId", "avatarUrl" }, contentParams: new[] { "ai" }),

            // data 4
            Make("WaterfallList", "瀑布流", "data", new[] { "items", "columns" }, new[] { "onItemClick", "onScrollEnd" }, contentParams: new[] { "data" }),
            Make("Table", "表格", "data", new[] { "dataSource", "columns", "pagination" }, new[] { "onChange", "onItemClick" }, contentParams: new[] { "data" }),
            Make("List", "列表", "data", new[] { "items" }, new[] { "onItemClick" }, contentParams: new[] { "data" }),
            Make("Pagination", "分页", "data", new[] { "current", "pageSize", "total" }, new[] { "onChange" })
        };
        return list;
    }
}

// 显式 using 防止类型不可见
internal static class _Pp
{
    public static PagedResult<T>? __keep<T>(PagedResult<T>? p) => p;
}
