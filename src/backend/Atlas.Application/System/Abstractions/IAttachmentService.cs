using Atlas.Application.System.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.System.Abstractions;

public interface IAttachmentService
{
    /// <summary>
    /// 查询指定业务实体关联的所有附件，可按 FieldKey 过滤。
    /// </summary>
    Task<IReadOnlyList<AttachmentBindingDto>> GetAttachmentsAsync(
        TenantId tenantId,
        string entityType,
        long entityId,
        string? fieldKey,
        CancellationToken ct = default);

    /// <summary>
    /// 将已上传文件绑定到业务实体，支持多态关联。
    /// </summary>
    Task<AttachmentBindingDto> BindAsync(
        TenantId tenantId,
        AttachmentBindRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// 解绑附件与业务实体的关联关系（不删除物理文件及 FileRecord）。
    /// </summary>
    Task UnbindAsync(
        TenantId tenantId,
        AttachmentUnbindRequest request,
        CancellationToken ct = default);
}
