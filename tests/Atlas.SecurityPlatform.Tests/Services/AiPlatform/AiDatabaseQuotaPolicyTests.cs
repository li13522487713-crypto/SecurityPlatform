using Atlas.Core.Exceptions;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.AiPlatform;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Atlas.SecurityPlatform.Tests.Services.AiPlatform;

public sealed class AiDatabaseQuotaPolicyTests
{
    [Fact]
    public void EnsureFieldCount_OverLimit_ShouldThrow()
    {
        var policy = BuildPolicy(new AiDatabaseQuotaOptions { MaxFieldsPerTable = 3 });
        Assert.Throws<BusinessException>(() => policy.EnsureFieldCount(4));
    }

    [Fact]
    public void EnsureFieldCount_AtLimit_ShouldPass()
    {
        var policy = BuildPolicy(new AiDatabaseQuotaOptions { MaxFieldsPerTable = 5 });
        policy.EnsureFieldCount(5);
        policy.EnsureFieldCount(0);
    }

    [Fact]
    public void EnsureBulkInsertSize_OverLimit_ShouldThrow()
    {
        var policy = BuildPolicy(new AiDatabaseQuotaOptions { MaxBulkInsertRows = 100 });
        Assert.Throws<BusinessException>(() => policy.EnsureBulkInsertSize(101));
    }

    [Fact]
    public void EnsureBulkInsertSize_AtLimit_ShouldPass()
    {
        var policy = BuildPolicy(new AiDatabaseQuotaOptions { MaxBulkInsertRows = 100 });
        policy.EnsureBulkInsertSize(100);
        policy.EnsureBulkInsertSize(1);
    }

    private static AiDatabaseQuotaPolicy BuildPolicy(AiDatabaseQuotaOptions options)
    {
        // EnsureFieldCount / EnsureBulkInsertSize 是纯 options 校验，不触发仓储/客户端调用，
        // 因此可直接传 null（仓储基类被 sealed，无法用 NSubstitute mock）。
        return new AiDatabaseQuotaPolicy(
            Options.Create(options),
            databaseRepository: null!,
            recordRepository: null!,
            NullLogger<AiDatabaseQuotaPolicy>.Instance);
    }
}
