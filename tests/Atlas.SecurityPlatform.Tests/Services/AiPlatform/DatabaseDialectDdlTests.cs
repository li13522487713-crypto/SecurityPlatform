using Atlas.Application.AiPlatform.Models;
using Atlas.Infrastructure.Services.DatabaseStructure;

namespace Atlas.SecurityPlatform.Tests.Services.AiPlatform;

public sealed class DatabaseDialectDdlTests
{
    [Fact]
    public void MySqlDialect_Builds_Column_And_ForeignKey_Ddl()
    {
        var dialect = new MySqlDatabaseDialect();

        var alterSql = dialect.BuildAlterColumnSql(
            "sales_order",
            "atlas_demo",
            "total",
            new TableColumnDesignDto("total", "DECIMAL", Precision: 18, Scale: 2, Nullable: false, DefaultValue: "0"));
        var createFkSql = dialect.BuildCreateForeignKeySql(
            new CreateForeignKeyRequest(
                "atlas_demo",
                "sales_order",
                "fk_order_customer",
                ["customer_id"],
                "sales_customer",
                "atlas_demo",
                ["id"],
                "CASCADE",
                "NO ACTION"));

        Assert.Contains("MODIFY COLUMN", alterSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DECIMAL(18,2)", alterSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ADD CONSTRAINT", createFkSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FOREIGN KEY", createFkSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PostgreSqlDialect_Maps_DateTime_To_Timestamp_And_Drops_ForeignKey()
    {
        var dialect = new PostgreSqlDatabaseDialect();

        var alterSql = dialect.BuildAlterColumnSql(
            "sales_order",
            "atlas_demo",
            "created_at",
            new TableColumnDesignDto("created_at", "DATETIME", Nullable: false, DefaultValue: "CURRENT_TIMESTAMP"));
        var dropFkSql = dialect.BuildDropForeignKeySql(
            new DropForeignKeyRequest("atlas_demo", "sales_order", "fk_order_customer"));

        Assert.Contains("TIMESTAMP", alterSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DROP CONSTRAINT", dropFkSql, StringComparison.OrdinalIgnoreCase);
    }
}
