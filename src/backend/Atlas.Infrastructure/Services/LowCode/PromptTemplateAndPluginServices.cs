using System.Text.Json;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class PromptTemplateService : IPromptTemplateService
{
    private readonly IPromptTemplateRepository _repo;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;

    public PromptTemplateService(IPromptTemplateRepository repo, IIdGeneratorAccessor idGen, IAuditWriter auditWriter)
    {
        _repo = repo;
        _idGen = idGen;
        _auditWriter = auditWriter;
    }

    public async Task<long> UpsertAsync(TenantId tenantId, long currentUserId, PromptTemplateUpsertRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Id) && long.TryParse(request.Id, out var id))
        {
            var existing = await _repo.FindByIdAsync(tenantId, id, cancellationToken)
                ?? throw new BusinessException(ErrorCodes.NotFound, $"提示词模板不存在：{id}");
            existing.Update(request.Name, request.Body, request.Mode, request.Description, request.ShareScope);
            await _repo.UpdateAsync(existing, cancellationToken);
            await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.prompt-template.update", "success", $"id:{id}", null, null), cancellationToken);
            return id;
        }
        if (await _repo.ExistsCodeAsync(tenantId, request.Code, null, cancellationToken))
            throw new BusinessException(ErrorCodes.Conflict, $"模板编码已存在：{request.Code}");
        var newId = _idGen.NextId();
        var entity = new AppPromptTemplate(tenantId, newId, request.Code, request.Name, request.Body, request.Mode, currentUserId);
        if (!string.IsNullOrWhiteSpace(request.Description) || !string.IsNullOrWhiteSpace(request.ShareScope))
            entity.Update(request.Name, request.Body, request.Mode, request.Description, request.ShareScope);
        await _repo.InsertAsync(entity, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.prompt-template.create", "success", $"code:{request.Code}", null, null), cancellationToken);
        return newId;
    }

    public async Task DeleteAsync(TenantId tenantId, long currentUserId, long id, CancellationToken cancellationToken)
    {
        await _repo.DeleteAsync(tenantId, id, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.prompt-template.delete", "success", $"id:{id}", null, null), cancellationToken);
    }

    public async Task<IReadOnlyList<PromptTemplateDto>> SearchAsync(TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var list = await _repo.SearchAsync(tenantId, keyword, pageIndex <= 0 ? 1 : pageIndex, pageSize <= 0 ? 20 : pageSize, cancellationToken);
        return list.Select(ToDto).ToList();
    }

    public async Task<PromptTemplateDto?> GetAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var e = await _repo.FindByIdAsync(tenantId, id, cancellationToken);
        return e is null ? null : ToDto(e);
    }

    private static PromptTemplateDto ToDto(AppPromptTemplate e) => new(
        e.Id.ToString(), e.Code, e.Name, e.Body, e.Mode, e.Version, e.Description, e.ShareScope, e.CreatedByUserId.ToString(), e.CreatedAt, e.UpdatedAt);
}

public sealed class LowCodePluginService : ILowCodePluginService
{
    private static readonly HashSet<string> AllowedAuthKinds = new(StringComparer.OrdinalIgnoreCase) { "api_key", "oauth", "basic", "none" };
    private static readonly HashSet<string> AllowedShareScopes = new(StringComparer.OrdinalIgnoreCase) { "private", "team", "public" };

    private readonly ILowCodePluginRepository _repo;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;
    private readonly LowCodeCredentialProtector _credentialProtector;

    public LowCodePluginService(ILowCodePluginRepository repo, IIdGeneratorAccessor idGen, IAuditWriter auditWriter, LowCodeCredentialProtector credentialProtector)
    {
        _repo = repo;
        _idGen = idGen;
        _auditWriter = auditWriter;
        _credentialProtector = credentialProtector;
    }

    public async Task<long> UpsertDefAsync(TenantId tenantId, long currentUserId, PluginUpsertRequest request, CancellationToken cancellationToken)
    {
        EnsureValidJson(request.ToolsJson, "toolsJson");
        if (!string.IsNullOrWhiteSpace(request.ShareScope) && !AllowedShareScopes.Contains(request.ShareScope))
            throw new BusinessException(ErrorCodes.ValidationError, $"shareScope 仅允许 private/team/public：{request.ShareScope}");

        if (!string.IsNullOrWhiteSpace(request.Id) && long.TryParse(request.Id, out var id))
        {
            var existing = await _repo.FindDefByIdAsync(tenantId, id, cancellationToken)
                ?? throw new BusinessException(ErrorCodes.NotFound, $"插件不存在：{id}");
            existing.Update(request.Name, request.Description, request.ToolsJson, request.ShareScope);
            await _repo.UpdateDefAsync(existing, cancellationToken);
            await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.plugin.update", "success", $"id:{id}", null, null), cancellationToken);
            return id;
        }
        var pluginId = $"plg_{_idGen.NextId()}";
        var entity = new LowCodePluginDefinition(tenantId, _idGen.NextId(), pluginId, request.Name, request.Description, currentUserId);
        entity.Update(request.Name, request.Description, request.ToolsJson, request.ShareScope);
        await _repo.InsertDefAsync(entity, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.plugin.create", "success", $"plugin:{pluginId}", null, null), cancellationToken);
        return entity.Id;
    }

    public async Task DeleteDefAsync(TenantId tenantId, long currentUserId, long id, CancellationToken cancellationToken)
    {
        await _repo.DeleteDefAsync(tenantId, id, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.plugin.delete", "success", $"id:{id}", null, null), cancellationToken);
    }

    public async Task<IReadOnlyList<PluginDefinitionDto>> SearchDefsAsync(TenantId tenantId, string? keyword, string? shareScope, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var list = await _repo.SearchDefsAsync(tenantId, keyword, shareScope, pageIndex <= 0 ? 1 : pageIndex, pageSize <= 0 ? 20 : pageSize, cancellationToken);
        return list.Select(ToDto).ToList();
    }

    public async Task<long> PublishVersionAsync(TenantId tenantId, long currentUserId, long defId, PluginPublishVersionRequest request, CancellationToken cancellationToken)
    {
        var def = await _repo.FindDefByIdAsync(tenantId, defId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"插件不存在：{defId}");
        var versionId = _idGen.NextId();
        var entity = new LowCodePluginVersion(tenantId, versionId, def.PluginId, request.Version, def.ToolsJson, currentUserId);
        await _repo.InsertVersionAsync(entity, cancellationToken);
        def.BumpVersion(request.Version);
        await _repo.UpdateDefAsync(def, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.plugin.version.publish", "success", $"plugin:{def.PluginId}:version:{request.Version}", null, null), cancellationToken);
        return versionId;
    }

    public async Task<long> AuthorizeAsync(TenantId tenantId, long currentUserId, string pluginId, PluginAuthorizeRequest request, CancellationToken cancellationToken)
    {
        if (!AllowedAuthKinds.Contains(request.AuthKind))
            throw new BusinessException(ErrorCodes.ValidationError, $"authKind 仅允许 api_key/oauth/basic/none：{request.AuthKind}");
        // M18 收尾：base64 占位 → AES-CBC 强加密 + 'lcp:' 前缀幂等；写入数据库的字段始终带前缀。
        var encrypted = string.IsNullOrEmpty(request.Credential) ? null : _credentialProtector.Encrypt(request.Credential!);
        var entity = new LowCodePluginAuthorization(tenantId, _idGen.NextId(), pluginId, request.AuthKind, encrypted, currentUserId);
        await _repo.InsertAuthAsync(entity, cancellationToken);
        // 审计中绝不写明文 / 密文：仅写 mask 摘要。
        var mask = string.IsNullOrEmpty(request.Credential) ? "(empty)" : LowCodeCredentialProtector.Mask(request.Credential);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.plugin.authorize", "success", $"plugin:{pluginId}:kind:{request.AuthKind}:cred:{mask}", null, null), cancellationToken);
        return entity.Id;
    }

    public async Task<PluginInvokeResult> InvokeAsync(TenantId tenantId, long currentUserId, PluginInvokeRequest request, CancellationToken cancellationToken)
    {
        var def = await _repo.FindDefByPluginIdAsync(tenantId, request.PluginId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"插件不存在：{request.PluginId}");
        // M18 阶段：仅做调用计量与回声（实际 HTTP 调用与 tool 路由由 PluginRegistry 在工作流 N10 节点中处理；本端点为 Studio 调试通道）。
        var day = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        var usage = await _repo.FindUsageAsync(tenantId, request.PluginId, day, cancellationToken);
        if (usage is null)
        {
            usage = new LowCodePluginUsage(tenantId, _idGen.NextId(), request.PluginId, day);
            await _repo.InsertUsageAsync(usage, cancellationToken);
        }
        var ok = !string.IsNullOrWhiteSpace(request.ToolName);
        usage.RecordInvocation(ok);
        await _repo.UpdateUsageAsync(usage, cancellationToken);

        var outputs = new Dictionary<string, JsonElement>
        {
            ["echo"] = JsonSerializer.SerializeToElement(new { request.ToolName, args = request.Args, plugin = def.Name, latestVersion = def.LatestVersion })
        };
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.plugin.invoke", ok ? "success" : "failed", $"plugin:{request.PluginId}:tool:{request.ToolName}", null, null), cancellationToken);
        return new PluginInvokeResult(request.PluginId, request.ToolName, ok ? "success" : "failed", outputs, ok ? null : "toolName 不可为空");
    }

    public async Task<PluginUsageDto?> GetUsageAsync(TenantId tenantId, string pluginId, string day, CancellationToken cancellationToken)
    {
        var u = await _repo.FindUsageAsync(tenantId, pluginId, day, cancellationToken);
        return u is null ? null : new PluginUsageDto(u.PluginId, u.Day, u.InvocationCount, u.ErrorCount);
    }

    private static void EnsureValidJson(string json, string field)
    {
        try { using var _ = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new BusinessException(ErrorCodes.ValidationError, $"{field} 不是合法 JSON：{ex.Message}"); }
    }

    private static PluginDefinitionDto ToDto(LowCodePluginDefinition e) => new(
        e.Id.ToString(), e.PluginId, e.Name, e.Description, e.ToolsJson, e.LatestVersion, e.ShareScope, e.CreatedByUserId.ToString(), e.CreatedAt, e.UpdatedAt);
}
