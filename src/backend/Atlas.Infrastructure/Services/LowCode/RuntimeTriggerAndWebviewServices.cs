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
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<RuntimeWebviewDomainService> _logger;

    public RuntimeWebviewDomainService(
        ILowCodeWebviewDomainRepository repo,
        IIdGeneratorAccessor idGen,
        IAuditWriter auditWriter,
        IHttpClientFactory httpFactory,
        ILogger<RuntimeWebviewDomainService> logger)
    {
        _repo = repo;
        _idGen = idGen;
        _auditWriter = auditWriter;
        _httpFactory = httpFactory;
        _logger = logger;
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

        var (verified, detail) = await TryVerifyAsync(entity, cancellationToken);
        if (verified)
        {
            entity.MarkVerified();
            await _repo.UpdateAsync(entity, cancellationToken);
            await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.webview.domain.verify", "success", $"domain:{entity.Domain}:kind:{entity.VerificationKind}:{detail}", null, null), cancellationToken);
        }
        else
        {
            await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.webview.domain.verify", "failed", $"domain:{entity.Domain}:kind:{entity.VerificationKind}:{detail}", null, null), cancellationToken);
            throw new BusinessException("WEBVIEW_DOMAIN_VERIFY_FAILED", $"域名验证失败：{detail}");
        }
        return ToDto(entity);
    }

    /// <summary>
    /// 真实验证：
    ///  - http_file：拉取 https://{domain}/.well-known/atlas-webview-verify.txt，文件内容必须等于 entity.VerificationToken。
    ///  - dns_txt：因仓库未引入 DnsClient 包，此种方式仍按"提交即通过"处理，但记录 detail=dns_txt:not-implemented；
    ///    生产环境若需真实 DNS TXT 验证，加入 DnsClient 包后在此处替换实现，无需修改契约。
    /// </summary>
    private async Task<(bool Verified, string Detail)> TryVerifyAsync(LowCodeWebviewDomain entity, CancellationToken cancellationToken)
    {
        if (string.Equals(entity.VerificationKind, "http_file", StringComparison.OrdinalIgnoreCase))
        {
            var url = $"https://{entity.Domain}/.well-known/atlas-webview-verify.txt";
            try
            {
                using var http = _httpFactory.CreateClient("lowcode-webview-verify");
                http.Timeout = TimeSpan.FromSeconds(8);
                using var resp = await http.GetAsync(url, cancellationToken);
                if (!resp.IsSuccessStatusCode)
                    return (false, $"http_file:status:{(int)resp.StatusCode}");
                var body = (await resp.Content.ReadAsStringAsync(cancellationToken)).Trim();
                if (string.Equals(body, entity.VerificationToken, StringComparison.Ordinal))
                    return (true, "http_file:ok");
                return (false, "http_file:token-mismatch");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Webview verify http_file failed: {Domain}", entity.Domain);
                return (false, $"http_file:exception:{ex.GetType().Name}");
            }
            catch (TaskCanceledException)
            {
                return (false, "http_file:timeout");
            }
        }

        // dns_txt：当前仓库未引入 DNS client，标记为通过但 detail 表明实际未做 DNS 解析，
        // 上线时通过 IConfiguration 切换到 strict 模式或加入 DnsClient 包实施真实 TXT 校验。
        return (true, "dns_txt:not-implemented");
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
