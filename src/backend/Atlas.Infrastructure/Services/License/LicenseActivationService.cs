using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Application.License.Abstractions;
using Atlas.Application.License.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.License;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.License;

/// <summary>
/// 证书激活服务：校验并持久化证书，激活后更新本地密封状态。
/// </summary>
public sealed class LicenseActivationService : ILicenseActivationService
{
    private readonly LicenseValidationService _validationService;
    private readonly ILicenseRepository _repository;
    private readonly ILicenseStateSealService _sealService;
    private readonly ILicenseService _licenseService;
    private readonly LicenseTenantAdminProvisionService _tenantAdminProvisionService;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly ILogger<LicenseActivationService> _logger;

    public LicenseActivationService(
        LicenseValidationService validationService,
        ILicenseRepository repository,
        ILicenseStateSealService sealService,
        ILicenseService licenseService,
        LicenseTenantAdminProvisionService tenantAdminProvisionService,
        IIdGeneratorAccessor idGenerator,
        ILogger<LicenseActivationService> logger)
    {
        _validationService = validationService;
        _repository = repository;
        _sealService = sealService;
        _licenseService = licenseService;
        _tenantAdminProvisionService = tenantAdminProvisionService;
        _idGenerator = idGenerator;
        _logger = logger;
    }

    public async Task<LicenseActivationResult> ActivateAsync(string rawLicenseContent, CancellationToken cancellationToken = default)
    {
        // 查询同 licenseId 的已有记录（用于 revision 防降级）
        LicenseRecord? existingRecord = null;

        var preParseEnvelope = ParseForIdOnly(rawLicenseContent);
        if (preParseEnvelope is not null)
        {
            existingRecord = await _repository.GetByLicenseIdAsync(preParseEnvelope.Payload.LicenseId, cancellationToken);
        }

        // 执行完整校验
        var result = _validationService.Validate(rawLicenseContent, existingRecord);
        if (!result.IsValid)
        {
            _logger.LogWarning("证书激活失败：{Reason}", result.FailureReason);
            return new LicenseActivationResult(false, result.FailureReason ?? "证书校验失败");
        }

        var envelope = result.Envelope!;
        var payload = envelope.Payload;
        var now = DateTimeOffset.UtcNow;
        var payloadHash = LicenseValidationService.ComputePayloadHash(payload);
        var effectiveTenantIdRaw = !string.IsNullOrWhiteSpace(payload.TenantId) ? payload.TenantId : payload.CustomerId;
        if (!Guid.TryParse(effectiveTenantIdRaw, out var tenantGuid))
        {
            _logger.LogWarning(
                "证书激活失败：证书未提供有效租户ID。LicenseId={LicenseId}, TenantId={TenantId}, CustomerId={CustomerId}",
                payload.LicenseId,
                payload.TenantId,
                payload.CustomerId);
            return new LicenseActivationResult(false, "证书缺少有效的租户ID（GUID），请联系颁发方重新签发证书");
        }

        // 加密存储原始证书内容
        var ciphertext = EncryptContent(rawLicenseContent);

        // 解析套餐
        var edition = Enum.TryParse<LicenseEdition>(payload.Edition, ignoreCase: true, out var parsed)
            ? parsed
            : LicenseEdition.Trial;

        var serializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var featuresJson = payload.Features.Count > 0
            ? JsonSerializer.Serialize(payload.Features, serializerOptions)
            : string.Empty;
        var limitsJson = payload.Limits.Count > 0
            ? JsonSerializer.Serialize(payload.Limits, serializerOptions)
            : string.Empty;

        var record = new LicenseRecord(
            ResolveRecordId(payload),
            payload.LicenseId,
            payload.Revision,
            edition,
            payload.IssuedAt,
            payload.ExpiresAt,
            payload.IsPermanent,
            payload.MachineFingerprint,
            payloadHash,
            ciphertext,
            featuresJson,
            limitsJson,
            now,
            effectiveTenantIdRaw,
            payload.TenantName);

        // 将旧的同 licenseId 记录标记失效（同 revision 重激活也需要失效旧 Active，避免双 Active）
        LicenseRecord? previousActiveRecord = null;
        if (existingRecord is not null
            && existingRecord.Status == LicenseStatus.Active
            && payload.Revision >= existingRecord.Revision)
        {
            existingRecord.MarkInvalid();
            previousActiveRecord = existingRecord;
        }

        var tenantId = new TenantId(tenantGuid);

        // 先完成管理员账号绑定，避免“授权已落库但管理员绑定冲突失败”导致的不一致状态。
        await _tenantAdminProvisionService.EnsureBootstrapAdminAsync(tenantId, cancellationToken);

        // 事务内完成“旧记录失效 + 新记录写入”，确保提交后再刷新内存授权状态。
        await _repository.SaveActivatedAsync(record, previousActiveRecord, cancellationToken);

        // 更新本地密封状态
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        _sealService.Seal(new LicenseSealedState
        {
            LicenseId = payload.LicenseId,
            PayloadHash = payloadHash,
            ActivationNonce = nonce,
            FirstActivatedAt = existingRecord?.ActivatedAt ?? now,
            LastValidatedAt = now,
            MaxObservedUtc = now
        });

        // 刷新内存中的授权状态
        await _licenseService.ReloadAsync(cancellationToken);

        _logger.LogInformation("证书激活成功：LicenseId={LicenseId}, Edition={Edition}, Revision={Revision}",
            payload.LicenseId, edition, payload.Revision);

        return new LicenseActivationResult(true, "证书激活成功");
    }

    private static Atlas.Application.License.Models.LicenseEnvelope? ParseForIdOnly(string rawContent)
    {
        try
        {
            var normalized = rawContent.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            var serializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            if (TryDeserializeEnvelope(normalized, serializerOptions, out var envelope))
            {
                return envelope;
            }

            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
            return TryDeserializeEnvelope(decoded, serializerOptions, out envelope) ? envelope : null;
        }
        catch { return null; }
    }

    private static bool TryDeserializeEnvelope(
        string content,
        JsonSerializerOptions serializerOptions,
        out Atlas.Application.License.Models.LicenseEnvelope? envelope)
    {
        try
        {
            envelope = JsonSerializer.Deserialize<Atlas.Application.License.Models.LicenseEnvelope>(content, serializerOptions);
            return envelope is not null;
        }
        catch
        {
            envelope = null;
            return false;
        }
    }

    private static string EncryptContent(string rawContent)
    {
        var plainBytes = Encoding.UTF8.GetBytes(rawContent);
        var encrypted = OperatingSystem.IsWindows()
            ? EncryptDpapi(plainBytes)
            : EncryptAesGcm(plainBytes);
        return Convert.ToBase64String(encrypted);
    }

    [SupportedOSPlatform("windows")]
    private static byte[] EncryptDpapi(byte[] plainBytes) =>
        // 机器级 DPAPI：密钥由 Windows LSA 管理，无法在其他机器上解密
        ProtectedData.Protect(plainBytes, null, DataProtectionScope.LocalMachine);

    private static byte[] EncryptAesGcm(byte[] plainBytes)
        => MachineBoundAesGcmHelper.Encrypt(plainBytes, "atlas-license-raw-v1");

    private long ResolveRecordId(LicensePayload payload)
    {
        try
        {
            return _idGenerator.NextId();
        }
        catch (BusinessException ex) when (ex.Code == ErrorCodes.ValidationError)
        {
            // 授权激活为匿名场景，可能不存在租户上下文；此时使用证书内标识生成回退 ID。
            var fallbackId = GenerateFallbackRecordId(payload);
            _logger.LogWarning(
                ex,
                "证书激活缺少租户上下文，使用回退ID。LicenseId={LicenseId}, Revision={Revision}, FallbackId={FallbackId}",
                payload.LicenseId,
                payload.Revision,
                fallbackId);
            return fallbackId;
        }
    }

    private static long GenerateFallbackRecordId(LicensePayload payload)
    {
        // 混入证书标识 + 时间 + 随机盐，避免同证书重复激活导致主键冲突。
        var seed = $"{payload.LicenseId:D}:{payload.Revision}:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}:{Guid.NewGuid():N}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var id = BitConverter.ToInt64(hash, 0) & long.MaxValue;
        return id == 0 ? 1 : id;
    }
}
