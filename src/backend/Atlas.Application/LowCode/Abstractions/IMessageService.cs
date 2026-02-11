using Atlas.Application.LowCode.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface IMessageService
{
    Task<PagedResult<MessageTemplateListItem>> QueryTemplatesAsync(PagedRequest request, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<MessageTemplateDetail?> GetTemplateByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<long> CreateTemplateAsync(TenantId tenantId, long userId, MessageTemplateCreateRequest request, CancellationToken cancellationToken = default);
    Task UpdateTemplateAsync(TenantId tenantId, long id, MessageTemplateUpdateRequest request, CancellationToken cancellationToken = default);
    Task DeleteTemplateAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<PagedResult<MessageRecordListItem>> QueryRecordsAsync(PagedRequest request, TenantId tenantId, CancellationToken cancellationToken = default);
    Task SendMessageAsync(TenantId tenantId, SendMessageRequest request, CancellationToken cancellationToken = default);
    Task<System.Collections.Generic.IReadOnlyList<ChannelConfigItem>> GetChannelConfigsAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task UpdateChannelConfigAsync(TenantId tenantId, string channel, ChannelConfigUpdateRequest request, CancellationToken cancellationToken = default);
}
