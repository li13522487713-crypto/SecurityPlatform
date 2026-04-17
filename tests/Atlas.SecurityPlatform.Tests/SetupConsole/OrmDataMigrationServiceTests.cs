using Atlas.Application.SetupConsole.Models;
using Atlas.Infrastructure.Services.SetupConsole;

namespace Atlas.SecurityPlatform.Tests.SetupConsole;

/// <summary>
/// ORM 跨库迁移引擎核心算法测试（M6）。
///
/// 不依赖真实 SqlSugar，覆盖：
/// - 指纹算法的稳定性、唯一性、空字段容错
/// - 与防重复机制的契约保证
/// </summary>
public sealed class OrmDataMigrationServiceTests
{
    [Fact]
    public void ComputeFingerprint_SameInputProducesSameHash()
    {
        var a = new DbConnectionConfig("SQLite", "SQLite", "raw", "Data Source=atlas.db", null);
        var b = new DbConnectionConfig("SQLite", "SQLite", "raw", "Data Source=atlas.db", null);

        var fingerprintA = OrmDataMigrationService.ComputeFingerprint(a);
        var fingerprintB = OrmDataMigrationService.ComputeFingerprint(b);

        Assert.Equal(fingerprintA, fingerprintB);
        Assert.Equal(64, fingerprintA.Length); // SHA256 hex
    }

    [Fact]
    public void ComputeFingerprint_DifferentDbTypeProducesDifferentHash()
    {
        var sqlite = new DbConnectionConfig("SQLite", "SQLite", "raw", "Data Source=atlas.db", null);
        var mysql = new DbConnectionConfig("MySql", "MySql", "raw", "Data Source=atlas.db", null);

        Assert.NotEqual(
            OrmDataMigrationService.ComputeFingerprint(sqlite),
            OrmDataMigrationService.ComputeFingerprint(mysql));
    }

    [Fact]
    public void ComputeFingerprint_DifferentConnectionStringProducesDifferentHash()
    {
        var a = new DbConnectionConfig("SQLite", "SQLite", "raw", "Data Source=a.db", null);
        var b = new DbConnectionConfig("SQLite", "SQLite", "raw", "Data Source=b.db", null);

        Assert.NotEqual(
            OrmDataMigrationService.ComputeFingerprint(a),
            OrmDataMigrationService.ComputeFingerprint(b));
    }

    [Fact]
    public void ComputeFingerprint_NullConnectionStringIsHandledAsEmpty()
    {
        var withNull = new DbConnectionConfig("SQLite", "SQLite", "raw", null, null);
        var withEmpty = new DbConnectionConfig("SQLite", "SQLite", "raw", string.Empty, null);

        Assert.Equal(
            OrmDataMigrationService.ComputeFingerprint(withNull),
            OrmDataMigrationService.ComputeFingerprint(withEmpty));
    }

    [Fact]
    public void ComputeFingerprint_NullConfigThrows()
    {
        Assert.Throws<ArgumentNullException>(() => OrmDataMigrationService.ComputeFingerprint(null!));
    }

    [Fact]
    public void ComputeFingerprint_OnlyDbTypeAndConnectionStringMatter()
    {
        // mode/visualConfig 不参与指纹（因为同一个目标可能用 raw / visual 两种方式连）
        var a = new DbConnectionConfig("SQLite", "SQLite", "raw", "Data Source=atlas.db", null);
        var b = new DbConnectionConfig("SQLite", "SQLite", "visual", "Data Source=atlas.db", new Dictionary<string, string>
        {
            ["host"] = "localhost"
        });

        Assert.Equal(
            OrmDataMigrationService.ComputeFingerprint(a),
            OrmDataMigrationService.ComputeFingerprint(b));
    }

    [Fact]
    public void ComputeFingerprint_DbTypeIsCaseSensitive()
    {
        var lower = new DbConnectionConfig("sqlite", "sqlite", "raw", "Data Source=atlas.db", null);
        var upper = new DbConnectionConfig("SQLite", "SQLite", "raw", "Data Source=atlas.db", null);

        Assert.NotEqual(
            OrmDataMigrationService.ComputeFingerprint(lower),
            OrmDataMigrationService.ComputeFingerprint(upper));
    }
}
