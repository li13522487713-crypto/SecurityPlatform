using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Application.License.Abstractions;
using Atlas.Application.License.Models;
using Atlas.Domain.License;
using Atlas.Infrastructure.IdGen;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.License;

/// <summary>
/// 证书校验服务：汇总所有校验步骤。
/// 校验顺序：签名 → 过期 → 机器码 → 时间回拨 → Revision → 本地状态一致性。
/// </summary>
public sealed class LicenseValidationService
{
    private static readonly TimeSpan ClockSkewTolerance = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly ILicenseSignatureService _signatureService;
    private readonly IMachineFingerprintService _fingerprintService;
    private readonly ILicenseStateSealService _sealService;
    private readonly ILogger<LicenseValidationService> _logger;

    public LicenseValidationService(
        ILicenseSignatureService signatureService,
        IMachineFingerprintService fingerprintService,
        ILicenseStateSealService sealService,
        ILogger<LicenseValidationService> logger)
    {
        _signatureService = signatureService;
        _fingerprintService = fingerprintService;
        _sealService = sealService;
        _logger = logger;
    }

    public sealed record ValidationResult(bool IsValid, string? FailureReason, LicenseEnvelope? Envelope);

    /// <summary>对原始证书字符串执行完整校验流程</summary>
    public ValidationResult Validate(string rawContent, LicenseRecord? existingRecord = null)
    {
        // 1. 解析
        var envelope = _signatureService.Parse(rawContent);
        if (envelope is null)
            return Fail("证书格式无法解析");

        // 2. 签名校验
        if (!_signatureService.Verify(envelope))
            return Fail("证书签名验证失败，证书可能已被篡改");

        var payload = envelope.Payload;

        // 3. 过期校验（非永久证书）
        var now = DateTimeOffset.UtcNow;
        if (!payload.IsPermanent && payload.ExpiresAt.HasValue && now > payload.ExpiresAt.Value)
            return Fail($"证书已于 {payload.ExpiresAt.Value:yyyy-MM-dd} 过期");

        // 4. 机器码校验
        if (!_fingerprintService.Matches(payload.MachineFingerprint))
            return Fail("证书绑定的机器码与当前机器不匹配");

        // 5. Revision 防降级：新证书 revision 必须 >= 已激活证书
        if (existingRecord is not null && payload.LicenseId == existingRecord.LicenseId)
        {
            if (payload.Revision < existingRecord.Revision)
                return Fail($"证书版本号 (revision={payload.Revision}) 低于当前已激活版本 ({existingRecord.Revision})，禁止降级");
        }

        // 6. 本地密封状态一致性（防止快照回滚）
        var sealedState = _sealService.Unseal();
        if (sealedState is not null && sealedState.LicenseId == payload.LicenseId)
        {
            var payloadHash = ComputePayloadHash(payload);

            // 相同 licenseId 但 payloadHash 不同 → 替换攻击
            if (sealedState.PayloadHash != payloadHash && payload.Revision == existingRecord?.Revision)
                return Fail("证书内容与本地激活记录不一致，可能存在替换攻击");

            // 时间回拨检测
            if (now < sealedState.MaxObservedUtc - ClockSkewTolerance)
            {
                _logger.LogWarning("检测到时间回拨：当前 UTC={Now}, 历史最大 UTC={Max}", now, sealedState.MaxObservedUtc);
                return Fail("检测到系统时间异常（时间回拨），授权校验失败");
            }
        }

        return new ValidationResult(true, null, envelope);
    }

    public static string ComputePayloadHash(LicensePayload payload)
    {
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static ValidationResult Fail(string reason) => new(false, reason, null);
}
