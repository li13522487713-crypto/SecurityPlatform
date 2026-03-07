namespace Atlas.Domain.License;

public enum LicenseStatus
{
    /// <summary>未激活，尚未导入任何证书</summary>
    None = 0,
    /// <summary>证书有效</summary>
    Active = 1,
    /// <summary>证书已过期</summary>
    Expired = 2,
    /// <summary>证书签名无效或被篡改</summary>
    Invalid = 3
}
