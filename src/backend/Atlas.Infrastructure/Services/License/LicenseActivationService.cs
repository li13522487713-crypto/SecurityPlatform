using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Application.License.Abstractions;
using Atlas.Application.License.Models;
using Atlas.Core.Abstractions;
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
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly ILogger<LicenseActivationService> _logger;

    public LicenseActivationService(
        LicenseValidationService validationService,
        ILicenseRepository repository,
        ILicenseStateSealService sealService,
        ILicenseService licenseService,
        IIdGeneratorAccessor idGenerator,
        ILogger<LicenseActivationService> logger)
    {
        _validationService = validationService;
        _repository = repository;
        _sealService = sealService;
        _licenseService = licenseService;
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
            _idGenerator.NextId(),
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
            now);

        // 将旧的同 licenseId 记录标记失效（同 revision 重激活也需要失效旧 Active，避免双 Active）
        LicenseRecord? previousActiveRecord = null;
        if (existingRecord is not null
            && existingRecord.Status == LicenseStatus.Active
            && payload.Revision >= existingRecord.Revision)
        {
            existingRecord.MarkInvalid();
            previousActiveRecord = existingRecord;
        }

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
}
