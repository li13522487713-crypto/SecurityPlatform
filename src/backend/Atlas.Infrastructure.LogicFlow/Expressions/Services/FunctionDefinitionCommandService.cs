using Atlas.Application.LogicFlow.Expressions.Abstractions;
using Atlas.Application.LogicFlow.Expressions.Models;
using Atlas.Application.LogicFlow.Expressions.Repositories;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Expressions;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Services;

public sealed class FunctionDefinitionCommandService : IFunctionDefinitionCommandService
{
    private readonly IFunctionDefinitionRepository _repo;

    public FunctionDefinitionCommandService(IFunctionDefinitionRepository repo)
    {
        _repo = repo;
    }

    public async Task<long> CreateAsync(
        FunctionDefinitionCreateRequest request,
        TenantId tenantId,
        string operatorName,
        CancellationToken cancellationToken)
    {
        var existing = await _repo.GetByNameAsync(request.Name, tenantId, cancellationToken);
        if (existing != null) throw new BusinessException("DUPLICATE_NAME", $"函数名 '{request.Name}' 已存在");

        var entity = new FunctionDefinition(tenantId, request.Name, request.Category)
        {
            DisplayName = request.DisplayName,
            Description = request.Description,
            ParametersJson = request.ParametersJson,
            ReturnType = request.ReturnType,
            BodyExpression = request.BodyExpression,
            SortOrder = request.SortOrder,
            CreatedBy = operatorName,
        };
        return await _repo.InsertAsync(entity, cancellationToken);
    }

    public async Task UpdateAsync(
        FunctionDefinitionUpdateRequest request,
        TenantId tenantId,
        string operatorName,
        CancellationToken cancellationToken)
    {
        var entity = await _repo.GetByIdAsync(request.Id, tenantId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", $"函数定义 {request.Id} 不存在");
        if (entity.IsBuiltin) throw new BusinessException("FORBIDDEN", "内置函数不可修改");

        var nameConflict = await _repo.GetByNameAsync(request.Name, tenantId, cancellationToken);
        if (nameConflict != null && nameConflict.Id != request.Id)
            throw new BusinessException("DUPLICATE_NAME", $"函数名 '{request.Name}' 已存在");

        entity.Name = request.Name;
        entity.DisplayName = request.DisplayName;
        entity.Description = request.Description;
        entity.Category = request.Category;
        entity.ParametersJson = request.ParametersJson;
        entity.ReturnType = request.ReturnType;
        entity.BodyExpression = request.BodyExpression;
        entity.IsEnabled = request.IsEnabled;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = operatorName;

        await _repo.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var entity = await _repo.GetByIdAsync(id, tenantId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", $"函数定义 {id} 不存在");
        if (entity.IsBuiltin) throw new BusinessException("FORBIDDEN", "内置函数不可删除");
        await _repo.DeleteAsync(id, tenantId, cancellationToken);
    }
}
