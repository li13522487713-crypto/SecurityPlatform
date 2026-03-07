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

        // 将旧的同 licenseId 记录标记失效（revision 更高时）
        if (existingRecord is not null && payload.Revision > existingRecord.Revision)
        {
            existingRecord.MarkInvalid();
            await _repository.UpdateAsync(existingRecord, cancellationToken);
        }

        await _repository.AddAsync(record, cancellationToken);

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
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(rawContent.Trim()));
            return JsonSerializer.Deserialize<Atlas.Application.License.Models.LicenseEnvelope>(decoded,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
        catch { return null; }
    }

    private static string EncryptContent(string rawContent)
    {
        // 对称加密存储（使用固定派生密钥，防止明文落库）
        var key = DeriveStorageKey();
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var plainBytes = Encoding.UTF8.GetBytes(rawContent);
        var ciphertext = new byte[plainBytes.Length];

        using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plainBytes, ciphertext, tag);

        // nonce(12) + tag(16) + ciphertext
        var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);
        return Convert.ToBase64String(result);
    }

    private static byte[] DeriveStorageKey()
    {
        const string seed = "atlas-license-storage-key-v1";
        return SHA256.HashData(Encoding.UTF8.GetBytes(seed + Environment.MachineName));
    }
}
