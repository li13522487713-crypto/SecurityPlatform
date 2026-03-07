using Atlas.Application.License.Models;

namespace Atlas.Application.License.Abstractions;

/// <summary>
/// 证书签名验证服务：用内嵌的 ECDSA 公钥验证证书签名。
/// </summary>
public interface ILicenseSignatureService
{
    /// <summary>验证证书签名是否合法</summary>
    bool Verify(LicenseEnvelope envelope);

    /// <summary>解析原始字符串为证书信封</summary>
    LicenseEnvelope? Parse(string rawContent);
}
