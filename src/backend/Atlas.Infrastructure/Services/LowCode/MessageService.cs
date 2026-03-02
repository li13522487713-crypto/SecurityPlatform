using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class MessageService : IMessageService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGenerator;

    public MessageService(ISqlSugarClient db, IIdGeneratorAccessor idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    public async Task<PagedResult<MessageTemplateListItem>> QueryTemplatesAsync(
        PagedRequest request, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<MessageTemplate>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            query = query.Where(x => x.Name.Contains(request.Keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToPageListAsync(request.PageIndex, request.PageSize, cancellationToken);

        var mapped = items.Select(e => new MessageTemplateListItem(
            e.Id.ToString(), e.Name, e.Channel, e.EventType, e.Description, e.IsActive, e.CreatedAt
        )).ToList();

        return new PagedResult<MessageTemplateListItem>(mapped, total, request.PageIndex, request.PageSize);
    }

    public async Task<MessageTemplateDetail?> GetTemplateByIdAsync(
        TenantId tenantId, long id, CancellationToken cancellationToken = default)
    {
        var e = await _db.Queryable<MessageTemplate>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);

        if (e is null) return null;

        return new MessageTemplateDetail(
            e.Id.ToString(), e.Name, e.Channel, e.EventType, e.ContentTemplate,
            e.SubjectTemplate, e.Description, e.IsActive, e.CreatedAt, e.UpdatedAt
        );
    }

    public async Task<long> CreateTemplateAsync(
        TenantId tenantId, long userId, MessageTemplateCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var id = _idGenerator.NextId();
        var now = DateTimeOffset.UtcNow;
        var entity = new MessageTemplate(
            tenantId, request.Name, request.Channel, request.EventType ?? "",
            request.ContentTemplate, request.SubjectTemplate, request.Description,
            userId, id, now);

        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return id;
    }

    public async Task UpdateTemplateAsync(
        TenantId tenantId, long id, MessageTemplateUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Queryable<MessageTemplate>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken)
            ?? throw new InvalidOperationException($"消息模板 ID={id} 不存在");

        var now = DateTimeOffset.UtcNow;
        entity.Update(request.Name, request.Channel, request.EventType, request.ContentTemplate,
            request.SubjectTemplate, request.Description, now);

        await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteTemplateAsync(
        TenantId tenantId, long id, CancellationToken cancellationToken = default)
    {
        await _db.Deleteable<MessageTemplate>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<PagedResult<MessageRecordListItem>> QueryRecordsAsync(
        PagedRequest request, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<MessageRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            query = query.Where(x => x.Subject != null && x.Subject.Contains(request.Keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(request.PageIndex, request.PageSize, cancellationToken);

        var mapped = items.Select(e => new MessageRecordListItem(
            e.Id.ToString(), e.Channel, e.RecipientAddress, e.Subject,
            e.Status, e.RetryCount, e.CreatedAt, e.SentAt
        )).ToList();

        return new PagedResult<MessageRecordListItem>(mapped, total, request.PageIndex, request.PageSize);
    }

    public async Task SendMessageAsync(
        TenantId tenantId, SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        var id = _idGenerator.NextId();
        var now = DateTimeOffset.UtcNow;

        var content = request.Content ?? "";

        // If template is specified, load and apply variables
        if (!string.IsNullOrWhiteSpace(request.TemplateId) && long.TryParse(request.TemplateId, out var templateId))
        {
            var template = await _db.Queryable<MessageTemplate>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.Id == templateId)
                .FirstAsync(cancellationToken);

            if (template != null)
            {
                content = template.ContentTemplate;
                if (request.Variables != null)
                {
                    foreach (var (key, value) in request.Variables)
                    {
                        content = content.Replace($"{{{{{key}}}}}", value);
                    }
                }
            }
        }

        var record = new MessageRecord(
            tenantId, null, request.Channel, request.RecipientId,
            request.RecipientAddress, request.Subject, content,
            request.EventType, id, now);

        // Mark as sent immediately (actual sending would be handled by background service)
        record.MarkSent(now);

        await _db.Insertable(record).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChannelConfigItem>> GetChannelConfigsAsync(
        TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var items = await _db.Queryable<ChannelConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .ToListAsync(cancellationToken);

        return items.Select(e => new ChannelConfigItem(
            e.Id.ToString(), e.Channel, e.ConfigJson, e.IsActive, e.UpdatedAt
        )).ToList();
    }

    public async Task UpdateChannelConfigAsync(
        TenantId tenantId, string channel, ChannelConfigUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Queryable<ChannelConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Channel == channel)
            .FirstAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        if (entity is null)
        {
            var id = _idGenerator.NextId();
            entity = new ChannelConfig(tenantId, channel, request.ConfigJson, request.IsActive, id, now);
            await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            entity.Update(request.ConfigJson, request.IsActive, now);
            await _db.Updateable(entity)
                .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
                .ExecuteCommandAsync(cancellationToken);
        }
    }
}
