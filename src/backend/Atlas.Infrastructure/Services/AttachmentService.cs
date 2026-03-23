using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Abstractions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.System.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 附件多态绑定服务：管理 FileRecord 与任意业务实体的关联关系。
/// </summary>
public sealed class AttachmentService : IAttachmentService
{
    private readonly AttachmentBindingRepository _bindingRepository;
    private readonly FileRecordRepository _fileRecordRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public AttachmentService(
        AttachmentBindingRepository bindingRepository,
        FileRecordRepository fileRecordRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _bindingRepository = bindingRepository;
        _fileRecordRepository = fileRecordRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<IReadOnlyList<AttachmentBindingDto>> GetAttachmentsAsync(
        TenantId tenantId,
        string entityType,
        long entityId,
        string? fieldKey,
        CancellationToken ct = default)
    {
        var bindings = await _bindingRepository.ListByEntityAsync(tenantId, entityType, entityId, fieldKey, ct);
        if (bindings.Count == 0)
        {
            return [];
        }

        // 批量查询 FileRecord，避免循环内查库
        var fileIds = bindings.Select(b => b.FileRecordId).Distinct().ToArray();
        var fileRecords = await _fileRecordRepository.QueryByIdsAsync(tenantId, fileIds, ct);
        var fileMap = fileRecords.ToDictionary(f => f.Id);

        var result = new List<AttachmentBindingDto>(bindings.Count);
        foreach (var binding in bindings)
        {
            if (!fileMap.TryGetValue(binding.FileRecordId, out var file))
            {
                continue;
            }

            result.Add(new AttachmentBindingDto(
                binding.Id,
                binding.FileRecordId,
                binding.EntityType,
                binding.EntityId,
                binding.FieldKey,
                binding.IsPrimary,
                file.OriginalName,
                file.ContentType,
                file.SizeBytes,
                file.VersionNumber,
                file.IsLatestVersion,
                file.UploadedAt));
        }

        return result;
    }

    public async Task<AttachmentBindingDto> BindAsync(
        TenantId tenantId,
        AttachmentBindRequest request,
        CancellationToken ct = default)
    {
        var file = await _fileRecordRepository.FindByIdAsync(tenantId, request.FileRecordId, ct)
            ?? throw new BusinessException("文件记录不存在。", ErrorCodes.NotFound);

        // 幂等：相同绑定已存在则直接返回
        var existing = await _bindingRepository.FindBindingAsync(
            tenantId, request.FileRecordId, request.EntityType, request.EntityId, request.FieldKey, ct);
        if (existing is not null)
        {
            return BuildDto(existing, file);
        }

        var id = _idGeneratorAccessor.NextId();
        var binding = new AttachmentBinding(
            tenantId,
            request.FileRecordId,
            request.EntityType,
            request.EntityId,
            request.FieldKey,
            request.IsPrimary,
            id);

        await _bindingRepository.AddAsync(binding, ct);
        return BuildDto(binding, file);
    }

    public async Task UnbindAsync(
        TenantId tenantId,
        AttachmentUnbindRequest request,
        CancellationToken ct = default)
    {
        var binding = await _bindingRepository.FindBindingAsync(
            tenantId, request.FileRecordId, request.EntityType, request.EntityId, request.FieldKey, ct)
            ?? throw new BusinessException("附件绑定关系不存在。", ErrorCodes.NotFound);

        await _bindingRepository.DeleteAsync(binding, ct);
    }

    private static AttachmentBindingDto BuildDto(AttachmentBinding binding, Atlas.Domain.System.Entities.FileRecord file)
    {
        return new AttachmentBindingDto(
            binding.Id,
            binding.FileRecordId,
            binding.EntityType,
            binding.EntityId,
            binding.FieldKey,
            binding.IsPrimary,
            file.OriginalName,
            file.ContentType,
            file.SizeBytes,
            file.VersionNumber,
            file.IsLatestVersion,
            file.UploadedAt);
    }
}
