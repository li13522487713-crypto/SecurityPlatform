using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.System.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services;

public sealed class SystemConfigCommandService : ISystemConfigCommandService
{
    private readonly SystemConfigRepository _repository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public SystemConfigCommandService(SystemConfigRepository repository, IIdGeneratorAccessor idGeneratorAccessor)
    {
        _repository = repository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<long> CreateSystemConfigAsync(
        TenantId tenantId, SystemConfigCreateRequest request, CancellationToken cancellationToken)
    {
        var exists = await _repository.ExistsByKeyAsync(tenantId, request.ConfigKey, cancellationToken);
        if (exists)
        {
            throw new BusinessException($"参数键 '{request.ConfigKey}' 已存在。", ErrorCodes.ValidationError);
        }

        var entity = new SystemConfig(tenantId, request.ConfigKey, request.ConfigValue, request.ConfigName, false, _idGeneratorAccessor.NextId(), request.ConfigType ?? "Text");
        entity.Update(request.ConfigValue, request.ConfigName, request.Remark);
        await _repository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateSystemConfigAsync(
        TenantId tenantId, long id, SystemConfigUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("参数不存在。", ErrorCodes.NotFound);

        entity.Update(request.ConfigValue, request.ConfigName, request.Remark);
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteSystemConfigAsync(
        TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("参数不存在。", ErrorCodes.NotFound);

        if (entity.IsBuiltIn)
        {
            throw new BusinessException("内置参数不允许删除。", ErrorCodes.Forbidden);
        }

        await _repository.DeleteAsync(tenantId, id, cancellationToken);
    }
}
