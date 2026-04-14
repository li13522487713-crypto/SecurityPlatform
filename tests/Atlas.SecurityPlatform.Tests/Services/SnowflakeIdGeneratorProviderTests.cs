using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.IdGen;
using Microsoft.Extensions.Options;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class SnowflakeIdGeneratorProviderTests
{
    private static readonly TenantId TestTenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    [Fact]
    public void NextId_WhenAppMappingMissing_ShouldFallbackToDefaultAppMapping()
    {
        var provider = CreateProvider(new IdGeneratorMappingOptions
        {
            DefaultAppId = "SecurityPlatform",
            Mappings =
            [
                new IdGeneratorMapping(TestTenant.Value.ToString("D"), "SecurityPlatform", 1)
            ]
        });

        var id = provider.NextId(TestTenant, "dev-instance-001");

        Assert.True(id > 0);
    }

    [Fact]
    public void NextId_WhenNoDefaultMappingAndNoFallback_ShouldThrowValidationError()
    {
        var provider = CreateProvider(new IdGeneratorMappingOptions
        {
            DefaultAppId = "SecurityPlatform",
            Mappings =
            [
                new IdGeneratorMapping(TestTenant.Value.ToString("D"), "another-app", 2)
            ]
        });

        var exception = Assert.Throws<BusinessException>(() => provider.NextId(TestTenant, "dev-instance-001"));

        Assert.Equal(ErrorCodes.ValidationError, exception.Code);
        Assert.Contains("dev-instance-001", exception.Message, StringComparison.Ordinal);
    }

    private static SnowflakeIdGeneratorProvider CreateProvider(IdGeneratorMappingOptions options)
    {
        return new SnowflakeIdGeneratorProvider(Options.Create(options));
    }
}
