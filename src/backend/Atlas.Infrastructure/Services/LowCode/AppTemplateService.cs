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

/// <summary>
/// 应用模板服务（M07 S07-4）。
///
/// kind 校验：page / component-set / pattern-A / pattern-B / pattern-C / pattern-D / industry。
/// templateJson 服务端 JsonDocument.Parse 二次校验，避免脏数据入库。
/// 共享市场：按 stars desc + updatedAt desc 排序，shareScope='public' 视为市场可见。
/// </summary>
public sealed class AppTemplateService : IAppTemplateService
{
    private static readonly HashSet<string> AllowedKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "page", "component-set", "pattern-A", "pattern-B", "pattern-C", "pattern-D", "industry"
    };
    private static readonly HashSet<string> AllowedShareScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        "private", "team", "public"
    };

    private readonly IAppTemplateRepository _repo;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;

    public AppTemplateService(IAppTemplateRepository repo, IIdGeneratorAccessor idGen, IAuditWriter auditWriter)
    {
        _repo = repo;
        _idGen = idGen;
        _auditWriter = auditWriter;
    }

    public async Task<long> UpsertAsync(TenantId tenantId, long currentUserId, AppTemplateUpsertRequest request, CancellationToken cancellationToken)
    {
        if (!AllowedKinds.Contains(request.Kind))
            throw new BusinessException(ErrorCodes.ValidationError, $"kind 仅允许 page/component-set/pattern-A..D/industry：{request.Kind}");
        if (!string.IsNullOrWhiteSpace(request.ShareScope) && !AllowedShareScopes.Contains(request.ShareScope))
            throw new BusinessException(ErrorCodes.ValidationError, $"shareScope 仅允许 private/team/public：{request.ShareScope}");
        EnsureValidJson(request.TemplateJson, "templateJson");

        if (!string.IsNullOrWhiteSpace(request.Id) && long.TryParse(request.Id, out var id))
        {
            var existing = await _repo.FindByIdAsync(tenantId, id, cancellationToken)
                ?? throw new BusinessException(ErrorCodes.NotFound, $"模板不存在：{id}");
            existing.Update(request.Name, request.Kind, request.Description, request.IndustryTag, request.TemplateJson, request.ShareScope);
            await _repo.UpdateAsync(existing, cancellationToken);
            await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.template.update", "success", $"id:{id}", null, null), cancellationToken);
            return id;
        }
        if (await _repo.ExistsCodeAsync(tenantId, request.Code, null, cancellationToken))
            throw new BusinessException(ErrorCodes.Conflict, $"模板编码已存在：{request.Code}");

        var newId = _idGen.NextId();
        var entity = new AppTemplate(tenantId, newId, request.Code, request.Name, request.Kind, request.TemplateJson, currentUserId);
        if (!string.IsNullOrWhiteSpace(request.Description) || !string.IsNullOrWhiteSpace(request.IndustryTag) || !string.IsNullOrWhiteSpace(request.ShareScope))
            entity.Update(request.Name, request.Kind, request.Description, request.IndustryTag, request.TemplateJson, request.ShareScope);
        await _repo.InsertAsync(entity, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.template.create", "success", $"code:{request.Code}:kind:{request.Kind}", null, null), cancellationToken);
        return newId;
    }

    public async Task DeleteAsync(TenantId tenantId, long currentUserId, long id, CancellationToken cancellationToken)
    {
        await _repo.DeleteAsync(tenantId, id, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.template.delete", "success", $"id:{id}", null, null), cancellationToken);
    }

    public async Task<IReadOnlyList<AppTemplateDto>> SearchAsync(TenantId tenantId, string? keyword, string? kind, string? shareScope, string? industryTag, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var list = await _repo.SearchAsync(tenantId, keyword, kind, shareScope, industryTag, pageIndex <= 0 ? 1 : pageIndex, pageSize <= 0 ? 20 : pageSize, cancellationToken);
        return list.Select(ToDto).ToList();
    }

    public async Task<AppTemplateDto?> GetAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var e = await _repo.FindByIdAsync(tenantId, id, cancellationToken);
        return e is null ? null : ToDto(e);
    }

    public async Task<AppTemplateApplyResult> ApplyAsync(TenantId tenantId, long currentUserId, long id, CancellationToken cancellationToken)
    {
        var entity = await _repo.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"模板不存在：{id}");
        entity.IncrementUse();
        await _repo.UpdateAsync(entity, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.template.apply", "success", $"id:{id}:useCount:{entity.UseCount}", null, null), cancellationToken);
        return new AppTemplateApplyResult(id.ToString(), entity.TemplateJson, entity.UseCount);
    }

    public async Task<int> StarAsync(TenantId tenantId, long currentUserId, long id, bool increment, CancellationToken cancellationToken)
    {
        var entity = await _repo.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"模板不存在：{id}");
        if (increment) entity.IncrementStars();
        else entity.DecrementStars();
        await _repo.UpdateAsync(entity, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), increment ? "lowcode.template.star" : "lowcode.template.unstar", "success", $"id:{id}:stars:{entity.Stars}", null, null), cancellationToken);
        return entity.Stars;
    }

    private static void EnsureValidJson(string json, string field)
    {
        try { using var _ = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new BusinessException(ErrorCodes.ValidationError, $"{field} 不是合法 JSON：{ex.Message}"); }
    }

    private static AppTemplateDto ToDto(AppTemplate e) => new(
        e.Id.ToString(), e.Code, e.Name, e.Kind, e.Description, e.IndustryTag, e.TemplateJson, e.ShareScope, e.Stars, e.UseCount, e.CreatedByUserId.ToString(), e.CreatedAt, e.UpdatedAt);
}
