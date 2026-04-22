using System.Text.Json;
using AutoMapper;
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

public sealed class PageDefinitionQueryService : IPageDefinitionQueryService
{
    private readonly IPageDefinitionRepository _repo;
    private readonly IMapper _mapper;

    public PageDefinitionQueryService(IPageDefinitionRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<PageDefinitionListItem>> ListAsync(TenantId tenantId, long appId, CancellationToken cancellationToken)
    {
        var list = await _repo.ListByAppAsync(tenantId, appId, cancellationToken);
        return _mapper.Map<IReadOnlyList<PageDefinitionListItem>>(list);
    }

    public async Task<PageDefinitionDetail?> GetAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken)
    {
        var p = await _repo.FindByIdAsync(tenantId, appId, id, cancellationToken);
        return p is null ? null : _mapper.Map<PageDefinitionDetail>(p);
    }
}

public sealed class PageDefinitionCommandService : IPageDefinitionCommandService
{
    private readonly IPageDefinitionRepository _repo;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;

    public PageDefinitionCommandService(IPageDefinitionRepository repo, IIdGeneratorAccessor idGen, IAuditWriter auditWriter)
    {
        _repo = repo;
        _idGen = idGen;
        _auditWriter = auditWriter;
    }

    public async Task<long> CreateAsync(TenantId tenantId, long currentUserId, long appId, PageDefinitionCreateRequest request, CancellationToken cancellationToken)
    {
        if (await _repo.ExistsCodeAsync(tenantId, appId, request.Code, excludeId: null, cancellationToken))
        {
            throw new BusinessException(ErrorCodes.Conflict, $"页面编码已存在：{request.Code}");
        }
        var id = _idGen.NextId();
        var existing = await _repo.ListByAppAsync(tenantId, appId, cancellationToken);
        var orderNo = existing.Count;
        var page = new PageDefinition(tenantId, id, appId, request.Code, request.DisplayName, request.Path, request.TargetType ?? "web", request.Layout ?? "free", orderNo);
        await _repo.InsertAsync(page, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.page.create", "success", $"app:{appId}:page:{id}", null, null), cancellationToken);
        return id;
    }

    public async Task UpdateAsync(TenantId tenantId, long currentUserId, long appId, long id, PageDefinitionUpdateRequest request, CancellationToken cancellationToken)
    {
        var page = await _repo.FindByIdAsync(tenantId, appId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"页面不存在：{id}");
        page.UpdateMetadata(request.DisplayName, request.Path, request.TargetType, request.Layout, request.IsVisible, request.IsLocked);
        await _repo.UpdateAsync(page, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.page.update", "success", $"app:{appId}:page:{id}", null, null), cancellationToken);
    }

    public async Task ReplaceSchemaAsync(TenantId tenantId, long currentUserId, long appId, long id, PageSchemaReplaceRequest request, CancellationToken cancellationToken)
    {
        EnsureValidJson(request.SchemaJson);
        var page = await _repo.FindByIdAsync(tenantId, appId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"页面不存在：{id}");
        page.ReplaceSchema(request.SchemaJson);
        await _repo.UpdateAsync(page, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.page.schema.replace", "success", $"app:{appId}:page:{id}", null, null), cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long currentUserId, long appId, long id, CancellationToken cancellationToken)
    {
        await _repo.DeleteAsync(tenantId, appId, id, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.page.delete", "success", $"app:{appId}:page:{id}", null, null), cancellationToken);
    }

    public async Task ReorderAsync(TenantId tenantId, long currentUserId, long appId, PagesReorderRequest request, CancellationToken cancellationToken)
    {
        var dict = request.Items.ToDictionary(i => long.Parse(i.Id), i => i.OrderNo);
        await _repo.ReorderBatchAsync(tenantId, appId, dict, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.page.reorder", "success", $"app:{appId}:count:{dict.Count}", null, null), cancellationToken);
    }

    private static void EnsureValidJson(string json)
    {
        try { using var _ = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new BusinessException(ErrorCodes.ValidationError, $"schemaJson 不是合法 JSON：{ex.Message}"); }
    }
}

public sealed class AppVariableQueryService : IAppVariableQueryService
{
    private readonly IAppVariableRepository _repo;
    private readonly IMapper _mapper;

    public AppVariableQueryService(IAppVariableRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<AppVariableDto>> ListAsync(TenantId tenantId, long appId, string? scope, CancellationToken cancellationToken)
    {
        var list = await _repo.ListByAppAsync(tenantId, appId, scope, cancellationToken);
        return _mapper.Map<IReadOnlyList<AppVariableDto>>(list);
    }
}

public sealed class AppVariableCommandService : IAppVariableCommandService
{
    private readonly IAppVariableRepository _repo;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;

    public AppVariableCommandService(IAppVariableRepository repo, IIdGeneratorAccessor idGen, IAuditWriter auditWriter)
    {
        _repo = repo;
        _idGen = idGen;
        _auditWriter = auditWriter;
    }

    public async Task<long> CreateAsync(TenantId tenantId, long currentUserId, long appId, AppVariableCreateRequest request, CancellationToken cancellationToken)
    {
        if (await _repo.ExistsCodeAsync(tenantId, appId, request.Code, excludeId: null, cancellationToken))
        {
            throw new BusinessException(ErrorCodes.Conflict, $"变量编码已存在：{request.Code}");
        }
        var id = _idGen.NextId();
        var v = new AppVariable(tenantId, id, appId, request.Code, request.DisplayName, request.Scope, request.ValueType);
        v.Update(request.DisplayName, request.ValueType, isReadOnly: request.Scope == "system", request.IsPersisted, request.DefaultValueJson, request.ValidationJson, request.Description);
        await _repo.InsertAsync(v, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.variable.create", "success", $"app:{appId}:var:{id}", null, null), cancellationToken);
        return id;
    }

    public async Task UpdateAsync(TenantId tenantId, long currentUserId, long appId, long id, AppVariableUpdateRequest request, CancellationToken cancellationToken)
    {
        var v = await _repo.FindByIdAsync(tenantId, appId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"变量不存在：{id}");
            
        if (!string.Equals(v.Code, request.Code, StringComparison.Ordinal))
        {
            if (await _repo.ExistsCodeAsync(tenantId, appId, request.Code, excludeId: id, cancellationToken))
            {
                throw new BusinessException(ErrorCodes.Conflict, $"变量编码已存在：{request.Code}");
            }
            v.RenameCode(request.Code);
        }

        v.Update(request.DisplayName, request.ValueType, request.IsReadOnly, request.IsPersisted, request.DefaultValueJson, request.ValidationJson, request.Description);
        await _repo.UpdateAsync(v, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.variable.update", "success", $"app:{appId}:var:{id}", null, null), cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long currentUserId, long appId, long id, CancellationToken cancellationToken)
    {
        await _repo.DeleteAsync(tenantId, appId, id, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.variable.delete", "success", $"app:{appId}:var:{id}", null, null), cancellationToken);
    }
}
