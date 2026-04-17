using System.Security.Cryptography;
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
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class RuntimeTriggerService : IRuntimeTriggerService
{
    private static readonly HashSet<string> AllowedKinds = new(StringComparer.OrdinalIgnoreCase) { "cron", "event", "webhook" };

    private readonly ILowCodeTriggerRepository _repo;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;
    private readonly ILogger<RuntimeTriggerService> _logger;

    public RuntimeTriggerService(ILowCodeTriggerRepository repo, IIdGeneratorAccessor idGen, IAuditWriter auditWriter, ILogger<RuntimeTriggerService> logger)
    {
        _repo = repo;
        _idGen = idGen;
        _auditWriter = auditWriter;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TriggerInfoDto>> ListAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var list = await _repo.ListAsync(tenantId, cancellationToken);
        return list.Select(ToDto).ToList();
    }

    public async Task<TriggerInfoDto> UpsertAsync(TenantId tenantId, long currentUserId, TriggerUpsertRequest request, CancellationToken cancellationToken)
    {
        if (!AllowedKinds.Contains(request.Kind))
            throw new BusinessException(ErrorCodes.ValidationError, $"kind 仅允许 cron/event/webhook：{request.Kind}");
        if (string.Equals(request.Kind, "cron", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(request.Cron))
            throw new BusinessException(ErrorCodes.ValidationError, "cron 触发器必须提供 cron 表达式");
        if (string.Equals(request.Kind, "event", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(request.EventName))
            throw new BusinessException(ErrorCodes.ValidationError, "event 触发器必须提供 eventName");

        LowCodeTrigger entity;
        if (!string.IsNullOrWhiteSpace(request.Id))
        {
            entity = await _repo.FindByTriggerIdAsync(tenantId, request.Id!, cancellationToken)
                ?? throw new BusinessException(ErrorCodes.NotFound, $"触发器不存在：{request.Id}");
            entity.Update(request.Name, request.Kind, request.Cron, request.EventName, request.WorkflowId, request.ChatflowId, request.Enabled ?? true);
            await _repo.UpdateAsync(entity, cancellationToken);
        }
        else
        {
            var triggerId = $"trg_{_idGen.NextId()}";
            entity = new LowCodeTrigger(tenantId, _idGen.NextId(), triggerId, request.Name, request.Kind, currentUserId);
            entity.Update(request.Name, request.Kind, request.Cron, request.EventName, request.WorkflowId, request.ChatflowId, request.Enabled ?? true);
            await _repo.InsertAsync(entity, cancellationToken);
        }
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.trigger.upsert", "success", $"trg:{entity.TriggerId}:kind:{entity.Kind}", null, null), cancellationToken);
        return ToDto(entity);
    }

    public async Task DeleteAsync(TenantId tenantId, long currentUserId, string triggerId, CancellationToken cancellationToken)
    {
        await _repo.DeleteAsync(tenantId, triggerId, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.trigger.delete", "success", $"trg:{triggerId}", null, null), cancellationToken);
    }

    public async Task PauseAsync(TenantId tenantId, long currentUserId, string triggerId, CancellationToken cancellationToken)
    {
        var t = await _repo.FindByTriggerIdAsync(tenantId, triggerId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"触发器不存在：{triggerId}");
        t.SetEnabled(false);
        await _repo.UpdateAsync(t, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.trigger.pause", "success", $"trg:{triggerId}", null, null), cancellationToken);
    }

    public async Task ResumeAsync(TenantId tenantId, long currentUserId, string triggerId, CancellationToken cancellationToken)
    {
        var t = await _repo.FindByTriggerIdAsync(tenantId, triggerId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"触发器不存在：{triggerId}");
        t.SetEnabled(true);
        await _repo.UpdateAsync(t, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.trigger.resume", "success", $"trg:{triggerId}", null, null), cancellationToken);
    }

    public async Task FireAsync(TenantId tenantId, string triggerId, CancellationToken cancellationToken)
    {
        var t = await _repo.FindByTriggerIdAsync(tenantId, triggerId, cancellationToken);
        if (t is null || !t.Enabled) return;
        t.RecordFire();
        await _repo.UpdateAsync(t, cancellationToken);
        _logger.LogInformation("LowCodeTrigger fired: tenant={Tenant} trigger={Trigger} workflow={Wf} chatflow={Cf}", tenantId.Value, triggerId, t.WorkflowId, t.ChatflowId);
        // M19 接入 Hangfire 调度链路时，FireAsync 内将通过 IRuntimeWorkflowExecutor 真实调用 workflow / chatflow。
    }

    private static TriggerInfoDto ToDto(LowCodeTrigger t) => new(
        t.TriggerId, t.Name, t.Kind, t.Cron, t.EventName, t.WorkflowId, t.ChatflowId, t.Enabled, t.CreatedAt, t.UpdatedAt, t.LastFiredAt);
}

public sealed class RuntimeWebviewDomainService : IRuntimeWebviewDomainService
{
    private static readonly HashSet<string> AllowedKinds = new(StringComparer.OrdinalIgnoreCase) { "dns_txt", "http_file" };

    private readonly ILowCodeWebviewDomainRepository _repo;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;

    public RuntimeWebviewDomainService(ILowCodeWebviewDomainRepository repo, IIdGeneratorAccessor idGen, IAuditWriter auditWriter)
    {
        _repo = repo;
        _idGen = idGen;
        _auditWriter = auditWriter;
    }

    public async Task<IReadOnlyList<WebviewDomainInfoDto>> ListAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var list = await _repo.ListAsync(tenantId, cancellationToken);
        return list.Select(ToDto).ToList();
    }

    public async Task<WebviewDomainInfoDto> AddAsync(TenantId tenantId, long currentUserId, AddWebviewDomainRequest request, CancellationToken cancellationToken)
    {
        if (!AllowedKinds.Contains(request.VerificationKind))
            throw new BusinessException(ErrorCodes.ValidationError, $"verificationKind 仅允许 dns_txt/http_file：{request.VerificationKind}");
        if (string.IsNullOrWhiteSpace(request.Domain))
            throw new BusinessException(ErrorCodes.ValidationError, "domain 不可为空");
        var existing = await _repo.FindByDomainAsync(tenantId, request.Domain, cancellationToken);
        if (existing is not null) throw new BusinessException(ErrorCodes.Conflict, $"域名已存在：{request.Domain}");

        var token = $"atlas-{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}";
        var entity = new LowCodeWebviewDomain(tenantId, _idGen.NextId(), request.Domain.ToLowerInvariant(), request.VerificationKind, token, currentUserId);
        await _repo.InsertAsync(entity, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.webview.domain.add", "success", $"domain:{entity.Domain}", null, null), cancellationToken);
        return ToDto(entity);
    }

    public async Task<WebviewDomainInfoDto> VerifyAsync(TenantId tenantId, long currentUserId, long id, CancellationToken cancellationToken)
    {
        var entity = await _repo.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"域名不存在：{id}");
        // M12 阶段：模拟验证通过。真实 DNS TXT / HTTP 文件验证由 M17 上线发布时落地（需要外部网络）。
        entity.MarkVerified();
        await _repo.UpdateAsync(entity, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.webview.domain.verify", "success", $"domain:{entity.Domain}", null, null), cancellationToken);
        return ToDto(entity);
    }

    public async Task RemoveAsync(TenantId tenantId, long currentUserId, long id, CancellationToken cancellationToken)
    {
        var existing = await _repo.FindByIdAsync(tenantId, id, cancellationToken);
        if (existing is null) return;
        await _repo.DeleteAsync(tenantId, id, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.webview.domain.remove", "success", $"domain:{existing.Domain}", null, null), cancellationToken);
    }

    private static WebviewDomainInfoDto ToDto(LowCodeWebviewDomain d) => new(d.Id.ToString(), d.Domain, d.Verified, d.VerificationKind, d.VerificationToken, d.CreatedAt, d.VerifiedAt);
}
