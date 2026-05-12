using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Core.Security;
using Microsoft.Extensions.Configuration;

namespace Atlas.Infrastructure.ExternalConnectors.Services;

public sealed class SecretProtectorService : ISecretProtector
{
    private const string DefaultDevMasterKey = "atlas-external-connectors-default-dev-key-change-in-prod";

    private readonly DataProtectionService _innerService;

    public SecretProtectorService(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var key = configuration["ExternalConnectors:DataProtectionKey"]
            ?? configuration["Security:SetupConsole:MigrationProtectorKey"]
            ?? DefaultDevMasterKey;
        _innerService = new DataProtectionService(key);
    }

    public string Encrypt(string plainText) => _innerService.Encrypt(plainText);

    public string Decrypt(string cipherText) => _innerService.Decrypt(cipherText);
}
