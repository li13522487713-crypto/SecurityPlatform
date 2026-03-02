using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.System.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services;

public sealed class DictCommandService : IDictCommandService
{
    private readonly DictTypeRepository _dictTypeRepository;
    private readonly DictDataRepository _dictDataRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public DictCommandService(
        DictTypeRepository dictTypeRepository,
        DictDataRepository dictDataRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork)
    {
        _dictTypeRepository = dictTypeRepository;
        _dictDataRepository = dictDataRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<long> CreateDictTypeAsync(
        TenantId tenantId, DictTypeCreateRequest request, CancellationToken cancellationToken)
    {
        var exists = await _dictTypeRepository.ExistsByCodeAsync(tenantId, request.Code, cancellationToken);
        if (exists)
        {
            throw new BusinessException($"字典类型编码 '{request.Code}' 已存在。", ErrorCodes.ValidationError);
        }

        var entity = new DictType(tenantId, request.Code, request.Name, _idGeneratorAccessor.NextId());
        entity.Update(request.Name, request.Status, request.Remark);
        await _dictTypeRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateDictTypeAsync(
        TenantId tenantId, long id, DictTypeUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await _dictTypeRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("字典类型不存在。", ErrorCodes.NotFound);

        entity.Update(request.Name, request.Status, request.Remark);
        await _dictTypeRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteDictTypeAsync(
        TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _dictTypeRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("字典类型不存在。", ErrorCodes.NotFound);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _dictDataRepository.DeleteByTypeCodeAsync(tenantId, entity.Code, cancellationToken);
            await _dictTypeRepository.DeleteAsync(tenantId, id, cancellationToken);
        }, cancellationToken);
    }

    public async Task<long> CreateDictDataAsync(
        TenantId tenantId, string typeCode, DictDataCreateRequest request, CancellationToken cancellationToken)
    {
        var typeExists = await _dictTypeRepository.ExistsByCodeAsync(tenantId, typeCode, cancellationToken);
        if (!typeExists)
        {
            throw new BusinessException($"字典类型 '{typeCode}' 不存在。", ErrorCodes.NotFound);
        }

        var entity = new DictData(tenantId, typeCode, request.Label, request.Value, _idGeneratorAccessor.NextId());
        entity.Update(request.Label, request.Value, request.SortOrder, request.Status, request.CssClass, request.ListClass);
        await _dictDataRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateDictDataAsync(
        TenantId tenantId, long id, DictDataUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await _dictDataRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("字典数据不存在。", ErrorCodes.NotFound);

        entity.Update(request.Label, request.Value, request.SortOrder, request.Status, request.CssClass, request.ListClass);
        await _dictDataRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteDictDataAsync(
        TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _dictDataRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("字典数据不存在。", ErrorCodes.NotFound);

        await _dictDataRepository.DeleteAsync(tenantId, id, cancellationToken);
    }
}
