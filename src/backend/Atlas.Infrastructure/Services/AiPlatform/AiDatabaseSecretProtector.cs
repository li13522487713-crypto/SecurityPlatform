using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Services.DatabaseStructure;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiDatabaseSecretProtector : IAiDatabaseSecretProtector
{
    private readonly DatabaseEncryptionOptions _options;

    public AiDatabaseSecretProtector(IOptions<DatabaseEncryptionOptions> options)
    {
        _options = options.Value;
    }

    public string Encrypt(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return string.Empty;
        }

        return _options.Enabled
            ? TenantDbConnectionFactory.Encrypt(secret.Trim(), _options.Key)
            : secret.Trim();
    }

    public string Decrypt(string? encryptedSecret)
    {
        if (string.IsNullOrWhiteSpace(encryptedSecret))
        {
            return string.Empty;
        }

        return _options.Enabled
            ? TenantDbConnectionFactory.Decrypt(encryptedSecret.Trim(), _options.Key)
            : encryptedSecret.Trim();
    }

    public string MaskConnectionString(string? connectionString)
        => ConnectionStringMasker.Mask(connectionString ?? string.Empty);

    public string MaskSecret(string? secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            return string.Empty;
        }

        return secret.Length <= 4 ? "****" : $"{secret[..2]}****{secret[^2..]}";
    }
}
