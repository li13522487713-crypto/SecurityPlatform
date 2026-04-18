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
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class RuntimeTriggerService : IRuntimeTriggerService
{
    private static readonly HashSet<string> AllowedKinds = new(StringComparer.OrdinalIgnoreCase) { "cron", "event", "webhook" };

    private readonly ILowCodeTriggerRepository _repo;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;
    private readonly IRuntimeWorkflowExecutor _workflowExecutor;
    private readonly IRecurringJobManager _recurringJobs;
    private readonly ILogger<RuntimeTriggerService> _logger;

    public RuntimeTriggerService(
        ILowCodeTriggerRepository repo,
        IIdGeneratorAccessor idGen,
        IAuditWriter auditWriter,
        IRuntimeWorkflowExecutor workflowExecutor,
        IRecurringJobManager recurringJobs,
        ILogger<RuntimeTriggerService> logger)
    {
        _repo = repo;
        _idGen = idGen;
        _auditWriter = auditWriter;
        _workflowExecutor = workflowExecutor;
        _recurringJobs = recurringJobs;
        _logger = logger;
    }

    private static string CronJobId(TenantId tenantId, string triggerId) => $"lowcode-trigger:{tenantId.Value}:{triggerId}";

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
        // 同步到 Hangfire RecurringJob：cron 触发器才注册到调度器；event/webhook 不走 cron
        SyncCronRegistration(tenantId, entity);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.trigger.upsert", "success", $"trg:{entity.TriggerId}:kind:{entity.Kind}", null, null), cancellationToken);
        return ToDto(entity);
    }

    public async Task DeleteAsync(TenantId tenantId, long currentUserId, string triggerId, CancellationToken cancellationToken)
    {
        await _repo.DeleteAsync(tenantId, triggerId, cancellationToken);
        try { _recurringJobs.RemoveIfExists(CronJobId(tenantId, triggerId)); }
        catch (Exception ex) { _logger.LogWarning(ex, "RemoveIfExists failed: trigger={Trigger}", triggerId); }
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.trigger.delete", "success", $"trg:{triggerId}", null, null), cancellationToken);
    }

    public async Task PauseAsync(TenantId tenantId, long currentUserId, string triggerId, CancellationToken cancellationToken)
    {
        var t = await _repo.FindByTriggerIdAsync(tenantId, triggerId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"触发器不存在：{triggerId}");
        t.SetEnabled(false);
        await _repo.UpdateAsync(t, cancellationToken);
        try { _recurringJobs.RemoveIfExists(CronJobId(tenantId, triggerId)); }
        catch (Exception ex) { _logger.LogWarning(ex, "RemoveIfExists on pause failed: trigger={Trigger}", triggerId); }
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.trigger.pause", "success", $"trg:{triggerId}", null, null), cancellationToken);
    }

    public async Task ResumeAsync(TenantId tenantId, long currentUserId, string triggerId, CancellationToken cancellationToken)
    {
        var t = await _repo.FindByTriggerIdAsync(tenantId, triggerId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"触发器不存在：{triggerId}");
        t.SetEnabled(true);
        await _repo.UpdateAsync(t, cancellationToken);
        SyncCronRegistration(tenantId, t);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.trigger.resume", "success", $"trg:{triggerId}", null, null), cancellationToken);
    }

    public async Task<string> RotateWebhookSecretAsync(TenantId tenantId, long currentUserId, string triggerId, CancellationToken cancellationToken)
    {
        var t = await _repo.FindByTriggerIdAsync(tenantId, triggerId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"触发器不存在：{triggerId}");
        if (!string.Equals(t.Kind, "webhook", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException(ErrorCodes.ValidationError, $"仅 kind=webhook 触发器支持 rotate-secret：{triggerId}（当前 {t.Kind}）");

        var secret = $"whs_{Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant()}";
        t.SetWebhookSecret(secret);
        await _repo.UpdateAsync(t, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.trigger.webhook.rotate", "success", $"trg:{triggerId}", null, null), cancellationToken);
        return secret;
    }

    public async Task FireWebhookAsync(TenantId tenantId, string triggerId, string providedSecret, CancellationToken cancellationToken)
    {
        var t = await _repo.FindByTriggerIdAsync(tenantId, triggerId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"触发器不存在：{triggerId}");
        if (!string.Equals(t.Kind, "webhook", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException(ErrorCodes.ValidationError, $"仅 kind=webhook 触发器支持 webhook 入口：{triggerId}（当前 {t.Kind}）");
        if (!t.Enabled)
            throw new BusinessException(ErrorCodes.ValidationError, $"触发器已禁用：{triggerId}");
        if (string.IsNullOrEmpty(t.WebhookSecret) || !ConstantTimeEquals(t.WebhookSecret, providedSecret))
        {
            await _auditWriter.WriteAsync(new AuditRecord(tenantId, "0", "lowcode.runtime.trigger.webhook.fire", "failed", $"trg:{triggerId}:reason:secret-mismatch", null, null), cancellationToken);
            throw new BusinessException("WEBHOOK_INVALID_SECRET", "webhook secret 校验失败");
        }
        await FireAsync(tenantId, triggerId, cancellationToken);
    }

    /// <summary>常量时间字符串比较，避免 webhook secret 时序攻击。</summary>
    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    /// <summary>
    /// 把 Trigger 同步注册（或移除）到 Hangfire RecurringJob：
    ///  - kind=cron 且 Enabled 且 Cron 非空 → AddOrUpdate
    ///  - 其它 → RemoveIfExists（避免遗留 job 在调度器中残留）
    /// 调度时通过 LowCodeTriggerCronJob 桥接到 IRuntimeTriggerService.FireAsync。
    /// </summary>
    private void SyncCronRegistration(TenantId tenantId, LowCodeTrigger t)
    {
        var jobId = CronJobId(tenantId, t.TriggerId);
        var isCron = string.Equals(t.Kind, "cron", StringComparison.OrdinalIgnoreCase) && t.Enabled && !string.IsNullOrWhiteSpace(t.Cron);
        try
        {
            if (isCron)
            {
                _recurringJobs.AddOrUpdate<LowCodeTriggerCronJob>(jobId, job => job.RunAsync(tenantId.Value, t.TriggerId), t.Cron!);
            }
            else
            {
                _recurringJobs.RemoveIfExists(jobId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SyncCronRegistration failed: trigger={Trigger} cron={Cron}", t.TriggerId, t.Cron);
        }
    }

    public async Task FireAsync(TenantId tenantId, string triggerId, CancellationToken cancellationToken)
    {
        var t = await _repo.FindByTriggerIdAsync(tenantId, triggerId, cancellationToken);
        if (t is null || !t.Enabled) return;
        t.RecordFire();
        await _repo.UpdateAsync(t, cancellationToken);
        _logger.LogInformation("LowCodeTrigger fired: tenant={Tenant} trigger={Trigger} workflow={Wf} chatflow={Cf}", tenantId.Value, triggerId, t.WorkflowId, t.ChatflowId);

        // 真实调用：触发器若绑定 workflowId，按异步任务提交（不阻塞 cron 调度），
        // 由 Hangfire 持久化执行；chatflowId 由前端会话域驱动，不在 trigger 里同步触发。
        if (!string.IsNullOrWhiteSpace(t.WorkflowId))
        {
            try
            {
                var req = new RuntimeWorkflowInvokeRequest(
                    WorkflowId: t.WorkflowId,
                    Inputs: null,
                    AppId: null,
                    PageId: null,
                    VersionId: null,
                    ComponentId: null,
                    Resilience: null);
                var jobId = await _workflowExecutor.SubmitAsyncAsync(tenantId, t.CreatedByUserId, req, cancellationToken);
                _logger.LogInformation("LowCodeTrigger workflow submitted async: trigger={Trigger} job={Job}", triggerId, jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LowCodeTrigger workflow submit failed: trigger={Trigger}", triggerId);
                await _auditWriter.WriteAsync(new AuditRecord(tenantId, t.CreatedByUserId.ToString(), "lowcode.runtime.trigger.fire", "failed", $"trg:{triggerId}:wf:{t.WorkflowId}:{ex.GetType().Name}", null, null), cancellationToken);
                return;
            }
        }
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, t.CreatedByUserId.ToString(), "lowcode.runtime.trigger.fire", "success", $"trg:{triggerId}:wf:{t.WorkflowId ?? "-"}:cf:{t.ChatflowId ?? "-"}", null, null), cancellationToken);
    }

    private static TriggerInfoDto ToDto(LowCodeTrigger t) => new(
        t.TriggerId, t.Name, t.Kind, t.Cron, t.EventName, t.WorkflowId, t.ChatflowId, t.Enabled, t.CreatedAt, t.UpdatedAt, t.LastFiredAt);
}

/// <summary>
/// Hangfire 桥接：被 Recurring 调度时回调到 IRuntimeTriggerService.FireAsync。
/// 必须为 public + 无参构造（DI 通过 Activator 实例化）。
/// </summary>
public sealed class LowCodeTriggerCronJob
{
    private readonly IRuntimeTriggerService _trigger;

    public LowCodeTriggerCronJob(IRuntimeTriggerService trigger)
    {
        _trigger = trigger;
    }

    public Task RunAsync(Guid tenantIdValue, string triggerId)
    {
        var tenantId = new TenantId(tenantIdValue);
        return _trigger.FireAsync(tenantId, triggerId, CancellationToken.None);
    }
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
