using Atlas.Application.License.Models;

namespace Atlas.Application.License.Abstractions;

/// <summary>
/// 证书激活服务：验证、解析并持久化证书。
/// </summary>
public interface ILicenseActivationService
{
    /// <summary>激活证书（首次导入或续签）</summary>
    Task<LicenseActivationResult> ActivateAsync(string rawLicenseContent, CancellationToken cancellationToken = default);
}
