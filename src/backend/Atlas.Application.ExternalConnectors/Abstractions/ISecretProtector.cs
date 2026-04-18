namespace Atlas.Application.ExternalConnectors.Abstractions;

/// <summary>
/// 密钥加解密抽象。Application/Infrastructure.ExternalConnectors 不直接依赖 Atlas.Infrastructure 的
/// DataProtectionService，由 PlatformHost / AppHost 在 DI 装配时桥接现有实现，避免反向依赖与重依赖污染。
/// </summary>
public interface ISecretProtector
{
    string Encrypt(string plainText);

    string Decrypt(string cipherText);
}
